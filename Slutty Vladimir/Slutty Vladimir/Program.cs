﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Drawing.Printing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using LeagueSharp.Common.Data;
using Color = System.Drawing.Color;
using SharpDX;

namespace Slutty_Vladimir
{
    internal class Program
    {
        public const string ChampName = "Vladimir";
        public const string Menuname = "Slutty Vladimir";
        public static Menu Config;
        public static Orbwalking.Orbwalker Orbwalker;
        public static Spell Q, W, E, R;

        private static readonly Obj_AI_Hero Player = ObjectManager.Player;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += OnLoad;
        }

        private static void OnLoad(EventArgs args)
        {
            if (Player.ChampionName != ChampName)
                return;

            Q = new Spell(SpellSlot.Q, 650);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E, 610);
            R = new Spell(SpellSlot.R, 700);

            R.SetSkillshot(0.25f, 175, 700, false, SkillshotType.SkillshotCircle);

            
            Config = new Menu(Menuname, Menuname, true);
            Config.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));

            var targetSelectorMenu = new Menu("Target Selector", "Target Selector");
            TargetSelector.AddToMenu(targetSelectorMenu);
            Config.AddSubMenu(targetSelectorMenu);

            Orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalking"));
            Config.AddSubMenu(new Menu("Drawings", "Drawings"));
            Config.SubMenu("Drawings").AddItem(new MenuItem("qDraw", "Q Drawing").SetValue(true));
            Config.SubMenu("Drawings").AddItem(new MenuItem("eDraw", "E Drawing").SetValue(true));
            Config.SubMenu("Drawings").AddItem(new MenuItem("eDraw", "R Drawing").SetValue(true));

            Config.AddSubMenu(new Menu("Combo", "Combo"));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseQ", "Use Q").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseE", "Use E").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("useEP", "Use E If HP% is above").SetValue(new Slider(50, 1)));
            Config.SubMenu("Combo").AddItem(new MenuItem("useR", "Use R").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("useRc", "Only R when targets").SetValue(new Slider(3, 1, 5)));

            Config.AddSubMenu(new Menu("LaneClear", "LaneClear"));
            Config.SubMenu("LaneClear").AddItem(new MenuItem("useQlc", "Use Q to last hit in laneclear").SetValue(true));
            Config.SubMenu("LaneClear").AddItem(new MenuItem("useQ2L", "Use Q to lane clear").SetValue(true));
            Config.SubMenu("LaneClear").AddItem(new MenuItem("useE2L", "Use E to lane clear").SetValue(true));
            Config.SubMenu("LaneClear").AddItem(new MenuItem("useESlider", "Min minions for E").SetValue(new Slider(3, 1, 20)));
            Config.SubMenu("LaneClear").AddItem(new MenuItem("useEPL", "Use E If HP% is above").SetValue(new Slider(50, 1)));
            Config.AddToMainMenu();
            Config.AddSubMenu(new Menu("KillSteal", "KillSteal"));
            Config.SubMenu("KillSteal").AddItem(new MenuItem("useQ2KS", "Use Q for ks").SetValue(true));
            Config.SubMenu("KillSteal").AddItem(new MenuItem("useE2KS", "Use E for ks").SetValue(true));

            Config.AddSubMenu(new Menu("Pool1", "Pool"));
            Config.SubMenu("Pool").AddItem(new MenuItem("useW", "Use W").SetValue(true));
            Config.SubMenu("Pool").AddItem(new MenuItem("useWHP", "Hp for W").SetValue(new Slider(50, 1)));
            Config.SubMenu("Pool").AddItem(new MenuItem("useWGapCloser", "Auto W when Gap Closer")).SetValue(true);

            Config.AddSubMenu(new Menu("AutoE", "AutoE"));
            Config.SubMenu("AutoE")
    .AddItem(
        new MenuItem("AutoE", "Automatic stack E", true).SetValue(new KeyBind("T".ToCharArray()[0],
            KeyBindType.Toggle)));
            Config.SubMenu("AutoE")
                .AddItem(new MenuItem("MinHPEStack", "Minimum automatic stack HP"))
                .SetValue(new Slider(20));

            Config.AddToMainMenu();
            Drawing.OnDraw += Drawing_OnDraw;
            Game.OnUpdate += Game_OnUpdate;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
        }
        private static void Game_OnUpdate(EventArgs args)
        {
            if (Player.IsDead)
                return;
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
            {
                Combo();
            }

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed)
            {
                Mixed();
            }

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
            {
                LaneClear();

            }
            AutoPool();
            KillSteal();
            AutoEs();

        }
        private static void Drawing_OnDraw(EventArgs args)
        {
            if (Player.IsDead)
                return;
            if (Config.Item("qDraw").GetValue<bool>() && Q.Level > 0)
            {
                Render.Circle.DrawCircle(Player.Position, Q.Range, Color.Green);
            }
            if (Config.Item("eDraw").GetValue<bool>() && E.Level > 0)
            {
                Render.Circle.DrawCircle(Player.Position, E.Range, Color.Gold);
            }
        }
        private static void Combo()
        {
            Obj_AI_Hero target = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);
            if (Config.Item("UseQ").GetValue<bool>()
                && Q.IsReady()
                && target.IsValidTarget(Q.Range))
            {
                Q.CastOnUnit(target);
            }
            if (Config.Item("UseE").GetValue<bool>()
                && E.IsReady()
                && target.IsValidTarget(E.Range)
                && Player.HealthPercent > Config.Item("useEP").GetValue<Slider>().Value)
            {
                E.Cast();
            }
            if (Config.Item("useR").GetValue<bool>()          
                && R.IsReady()
                && target.IsValidTarget(R.Range)
                && (Player.GetSpellDamage(target, SpellSlot.Q) + Player.GetSpellDamage(target, SpellSlot.E)) <
                target.Health)
            {
                R.Cast(target);
            }

        }

        private static void LaneClear()
        {
            var minionCount = MinionManager.GetMinions(Player.Position, Q.Range, MinionTypes.All, MinionTeam.NotAlly);
            {
                foreach (var minion in minionCount)
                {
                    if (Config.Item("useE2L").GetValue<bool>() 
                        && E.IsReady()
                        && minionCount.Count >= Config.Item("useESlider").GetValue<Slider>().Value
                        && Player.HealthPercent > Config.Item("useEPL").GetValue<Slider>().Value)
                    {
                        E.Cast();
                    }
                    if (Config.Item("useQlc").GetValue<bool>() 
                        && (Q.GetDamage(minion) > minion.Health) 
                        && Q.IsReady())
                    {
                        Q.CastOnUnit(minion);
                    }
                    if (Config.Item("useQ2L").GetValue<bool>()
                        && Q.IsReady())
                    {
                        Q.CastOnUnit(minion);
                    }

                }
            }
        }

        private static void Mixed()
        {
            
        }

        private static void KillSteal()
        {
            Obj_AI_Hero target = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);
            var qSpell = Config.Item("useQ2KS").GetValue<bool>();
            var eSpell = Config.Item("useE2KS").GetValue<bool>();
            if (qSpell
                && Q.GetDamage(target) > target.Health 
                && target.IsValidTarget(Q.Range))
            {
                Q.CastOnUnit(target);
            }
            if (eSpell 
                && E.GetDamage(target) > target.Health 
                && target.IsValidTarget(E.Range))
            {
                E.Cast();
            }  
        }

        private static void AutoPool()
        {
            Obj_AI_Hero target = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);
            if (Config.Item("useW").GetValue<bool>()
                && Player.HealthPercent < (Config.Item("useWHP").GetValue<Slider>().Value)
                && target.IsValidTarget(Q.Range))
            {
                W.Cast();
            }      
        }

        private static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (Config.Item("useWGapCloser").GetValue<bool>()
                && W.IsReady() 
                && gapcloser.Sender.Distance(Player) < W.Range 
                && Player.CountEnemiesInRange(E.Range) >= 1)
            {
                W.Cast(Player);
            }
        }

        private static void AutoEs()
        {
            if (Player.IsRecalling() || Player.InFountain())
                return;
            var stackHp = Config.Item("MinHPEStack").GetValue<Slider>().Value;

            if ( E.IsReady()
                && (Player.Health/Player.MaxHealth)*100 >= stackHp)
            {
                E.Cast();
            }
                
        }

    }
}