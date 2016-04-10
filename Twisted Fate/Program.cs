#region
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;
#endregion

namespace TwistedFate
{
    internal class Program
    {
        private static Menu _config;

        private static Spell _q;
        private const float Qangle = 28*(float) Math.PI/180;
        private static Orbwalking.Orbwalker _sow;
        private static Vector2 _pingLocation;
        private static int _lastPingT = 0;
        private static Obj_AI_Hero _player;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Ping(Vector2 position)
        {
            if (Utils.TickCount - _lastPingT < 30*1000) 
            {
                return;
            }
            
            _lastPingT = Utils.TickCount;
            _pingLocation = position;
            SimplePing();

            Utility.DelayAction.Add(150, SimplePing);
            Utility.DelayAction.Add(300, SimplePing);
            Utility.DelayAction.Add(400, SimplePing);
            Utility.DelayAction.Add(800, SimplePing);
        }

        private static void SimplePing()
        {
            Game.ShowPing(PingCategory.Fallback, _pingLocation, true);
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            if (ObjectManager.Player.ChampionName != "TwistedFate") return;
            _player = ObjectManager.Player;
            _q = new Spell(SpellSlot.Q, 1450);
            _q.SetSkillshot(0.25f, 40f, 1000f, false, SkillshotType.SkillshotLine);

            //Make the menu
            _config = new Menu("Twisted Fate", "TwistedFate", true);

            var targetSelectorMenu = new Menu("Target Selector", "Target Selector");
            TargetSelector.AddToMenu(targetSelectorMenu);
            _config.AddSubMenu(targetSelectorMenu);

            var sowMenu = new Menu("Orbwalking", "Orbwalking");
            _sow = new Orbwalking.Orbwalker(sowMenu);
            _config.AddSubMenu(sowMenu);

            /* Q */
            var q = new Menu("Q - Wildcards", "Q");
            {
                /*
        /// <summary>
        /// The target is immobile.
        /// </summary>
        Immobile = 8,

        /// <summary>
        /// The unit is dashing.
        /// </summary>
        Dashing = 7,

        /// <summary>
        /// Very high probability of hitting the target.
        /// </summary>
        VeryHigh = 6,

        /// <summary>
        /// High probability of hitting the target.
        /// </summary>
        High = 5,

        /// <summary>
        /// Medium probability of hitting the target.
        /// </summary>
        Medium = 4,

        /// <summary>
        /// Low probability of hitting the target.
        /// </summary>
        Low = 3,

        /// <summary>
        /// Impossible to hit the target.
        /// </summary>
        Impossible = 2,

        /// <summary>
        /// The target is out of range.
        /// </summary>
        OutOfRange = 1,

        /// <summary>
        /// The target is blocked by other units.
        /// </summary>
        Collision = 0
         */
                q.AddItem(new MenuItem("QCastLevel", "Auto-Q Level").SetValue(new Slider(3, 1, 5)).SetTooltip("More High More Precision"));
                _config.AddSubMenu(q);
            }

            /* W */
            var w = new Menu("W - Pick a card", "W");
            {
                w.AddItem(
                    new MenuItem("SelectYellow", "Select Yellow").SetValue(new KeyBind("W".ToCharArray()[0],
                        KeyBindType.Press)));
                w.AddItem(
                    new MenuItem("SelectBlue", "Select Blue").SetValue(new KeyBind("E".ToCharArray()[0],
                        KeyBindType.Press)));
                w.AddItem(
                    new MenuItem("SelectRed", "Select Red").SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press)));
                _config.AddSubMenu(w);
            }

            var r = new Menu("R - Destiny", "R");
            {
                r.AddItem(new MenuItem("AutoY", "Select yellow card after R").SetValue(true));
                _config.AddSubMenu(r);
            }

            var misc = new Menu("Misc", "Misc");
            {
                misc.AddItem(new MenuItem("PingLH", "Ping low health enemies (Only local)").SetValue(true));
                _config.AddSubMenu(misc);
            }

            //Damage after combo:
            var dmgAfterComboItem = new MenuItem("DamageAfterCombo", "Draw damage after combo").SetValue(true);
            Utility.HpBarDamageIndicator.DamageToUnit = ComboDamage;
            Utility.HpBarDamageIndicator.Enabled = dmgAfterComboItem.GetValue<bool>();
            dmgAfterComboItem.ValueChanged += delegate(object sender, OnValueChangeEventArgs eventArgs)
            {
                Utility.HpBarDamageIndicator.Enabled = eventArgs.GetNewValue<bool>();
            };

            /*Drawing*/
            var drawings = new Menu("Drawings", "Drawings");
            {
                drawings.AddItem(
                    new MenuItem("Qcircle", "Q Range").SetValue(new Circle(true, Color.FromArgb(100, 255, 0, 255))));
                drawings.AddItem(
                    new MenuItem("Rcircle", "R Range").SetValue(new Circle(true, Color.FromArgb(100, 255, 255, 255))));
                drawings.AddItem(
                    new MenuItem("Rcircle2", "R Range (minimap)").SetValue(new Circle(true,
                        Color.FromArgb(255, 255, 255, 255))));
                drawings.AddItem(dmgAfterComboItem);
                _config.AddSubMenu(drawings);
            }
            _config.AddToMainMenu();

            Game.OnUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Drawing.OnEndScene += DrawingOnOnEndScene;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Hero_OnProcessSpellCast;
            Orbwalking.BeforeAttack += OrbwalkingOnBeforeAttack;
        }

        private static void OrbwalkingOnBeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            if (args.Target is Obj_AI_Hero)
                args.Process = CardSelector.Status != SelectStatus.Selecting &&
                               Utils.TickCount - CardSelector.LastWSent > 300;
        }

        /// <summary>
        /// 释放大招自动黄牌
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private static void Obj_AI_Hero_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe)
            {
                return;
            }

            if (args.SData.Name.Equals("Gate", StringComparison.InvariantCultureIgnoreCase) && _config.Item("AutoY").GetValue<bool>())
            {
                CardSelector.StartSelecting(Cards.Yellow);
            }
        }

        private static void DrawingOnOnEndScene(EventArgs args)
        {
            var rCircle2 = _config.Item("Rcircle2").GetValue<Circle>();
            if (rCircle2.Active)
            {
                Utility.DrawCircle(ObjectManager.Player.Position, 5500, rCircle2.Color, 1, 23, true);
            }
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            var qCircle = _config.Item("Qcircle").GetValue<Circle>();
            var rCircle = _config.Item("Rcircle").GetValue<Circle>();

            if (qCircle.Active)
            {
                Render.Circle.DrawCircle(ObjectManager.Player.Position, _q.Range, qCircle.Color);
            }

            if (rCircle.Active)
            {
                Render.Circle.DrawCircle(ObjectManager.Player.Position, 5500, rCircle.Color);
            }
        }


        private static int CountHits(Vector2 position, List<Vector2> points, List<int> hitBoxes)
        {
            var result = 0;

            var startPoint = ObjectManager.Player.ServerPosition.To2D();
            var originalDirection = _q.Range*(position - startPoint).Normalized();
            var originalEndPoint = startPoint + originalDirection;

            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];

                for (var k = 0; k < 3; k++)
                {
                    var endPoint = new Vector2();
                    if (k == 0) endPoint = originalEndPoint;
                    if (k == 1) endPoint = startPoint + originalDirection.Rotated(Qangle);
                    if (k == 2) endPoint = startPoint + originalDirection.Rotated(-Qangle);

                    if (point.Distance(startPoint, endPoint, true, true) <
                        (_q.Width + hitBoxes[i])*(_q.Width + hitBoxes[i]))
                    {
                        result++;
                        break;
                    }
                }
            }

            return result;
        }

        private static void CastQ(Obj_AI_Base unit, Vector2 unitPosition, int minTargets = 0)
        {
            var points = new List<Vector2>();
            var hitBoxes = new List<int>();

            var startPoint = ObjectManager.Player.ServerPosition.To2D();
            var originalDirection = _q.Range*(unitPosition - startPoint).Normalized();

            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (enemy.IsValidTarget() && enemy.NetworkId != unit.NetworkId)
                {
                    var pos = _q.GetPrediction(enemy);
                    if (pos.Hitchance >= HitChance.Medium)
                    {
                        points.Add(pos.UnitPosition.To2D());
                        hitBoxes.Add((int) enemy.BoundingRadius);
                    }
                }
            }

            var posiblePositions = new List<Vector2>();

            for (var i = 0; i < 3; i++)
            {
                if (i == 0) posiblePositions.Add(unitPosition + originalDirection.Rotated(0));
                if (i == 1) posiblePositions.Add(startPoint + originalDirection.Rotated(Qangle));
                if (i == 2) posiblePositions.Add(startPoint + originalDirection.Rotated(-Qangle));
            }


            if (startPoint.Distance(unitPosition) < 900)
            {
                for (var i = 0; i < 3; i++)
                {
                    var pos = posiblePositions[i];
                    var direction = (pos - startPoint).Normalized().Perpendicular();
                    var k = (2/3*(unit.BoundingRadius + _q.Width));
                    posiblePositions.Add(startPoint - k*direction);
                    posiblePositions.Add(startPoint + k*direction);
                }
            }

            var bestPosition = new Vector2();
            var bestHit = -1;

            foreach (var position in posiblePositions)
            {
                var hits = CountHits(position, points, hitBoxes);
                if (hits > bestHit)
                {
                    bestPosition = position;
                    bestHit = hits;
                }
            }

            if (bestHit + 1 <= minTargets)
                return;

            _q.Cast(bestPosition.To3D(), true);
        }

        private static float ComboDamage(Obj_AI_Hero hero)
        {
            var dmg = 0d;
            dmg += _player.GetSpellDamage(hero, SpellSlot.Q)*2;
            dmg += _player.GetSpellDamage(hero, SpellSlot.W);
            dmg += _player.GetSpellDamage(hero, SpellSlot.Q);

            if (ObjectManager.Player.GetSpellSlot("SummonerIgnite") != SpellSlot.Unknown)
            {
                dmg += ObjectManager.Player.GetSummonerSpellDamage(hero, Damage.SummonerSpell.Ignite);
            }

            return (float) dmg;
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            if (_config.Item("PingLH").GetValue<bool>())
                foreach (
                    var enemy in
                        ObjectManager.Get<Obj_AI_Hero>()
                            .Where(
                                h =>
                                    ObjectManager.Player.Spellbook.CanUseSpell(SpellSlot.R) == SpellState.Ready &&
                                    h.IsValidTarget() && ComboDamage(h) > h.Health))
                {
                    Ping(enemy.Position.To2D());
                    //提示有人头在大招范围内可以抢了。
                    var drawPosition = Drawing.WorldToScreen(_player.Position);
                    var msg = "YOU CAN KSssssssssssssssssss.";
                    Drawing.DrawText(drawPosition[0] - (msg.Length) * 5, drawPosition[1] -200, System.Drawing.Color.Yellow, msg);
                }
            //释放Q技能逻辑
            SmartQLogic();
            //W技能逻辑
            SmartWLogic();
        }
        /// <summary>
        /// 释放Q技能逻辑
        /// </summary>
        private static void SmartQLogic()
        {
            if (!_q.IsReady())
                return;
            //Auto Q
            var qLevel = _config.Item("QCastLevel").GetValue<Slider>().Value;
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(unit => unit.IsValidTarget(_q.Range*2)))
            {
                var qTarget = TargetSelector.GetTarget(_q.Range*2, TargetSelector.DamageType.Magical);
                if (!qTarget.IsValidTarget())
                    continue;
                var qPrediction = _q.GetPrediction(qTarget);
                //抢人头.
                if (qTarget.Health < _q.GetDamage(qTarget))
                {
                    if (qPrediction.Hitchance >= HitChance.VeryHigh)
                    {
                        CastQ(qTarget, qPrediction.UnitPosition.To2D());
                        //_q.Cast(qPrediction.CastPosition);
                    }
                }
                else if((int)qPrediction.Hitchance >= qLevel + 3
                    && HeroManager.Player.ManaPercent > 60)
                {
                    CastQ(qTarget, qPrediction.UnitPosition.To2D());
                    //_q.Cast(qPrediction.CastPosition);
                }
                else if(qPrediction.Hitchance >= HitChance.Dashing)
                {
                    if (_sow.ActiveMode == Orbwalking.OrbwalkingMode.Combo
                        || HeroManager.Player.ManaPercent > 30
                        )
                    {
                        CastQ(qTarget, qPrediction.UnitPosition.To2D());
                    }
                }


            }
        }
        /// <summary>
        /// 释放W技能逻辑.
        /// </summary>
        private static void SmartWLogic()
        {
            //==================黄牌逻辑
            if (_config.Item("SelectYellow").GetValue<KeyBind>().Active)
            {
                CardSelector.StartSelecting(Cards.Yellow);
            }
            else if(_config.Item("SelectBlue").GetValue<KeyBind>().Active)
            {
                CardSelector.StartSelecting(Cards.Blue);
            }
            else if (_config.Item("SelectRed").GetValue<KeyBind>().Active)
            {
                CardSelector.StartSelecting(Cards.Red);
            }
            else
            {
                switch (_sow.ActiveMode)
                {
                    case Orbwalking.OrbwalkingMode.Combo:
                        var tmpTarget = TargetSelector.GetTarget(_player.AttackRange*2,
                            TargetSelector.DamageType.Physical);
                        if (tmpTarget.IsValid)
                        {
                            CardSelector.StartSelecting(Cards.Yellow);
                        }
                        
                        break;
                    case Orbwalking.OrbwalkingMode.LaneClear:
                        
                    case Orbwalking.OrbwalkingMode.Mixed:
                        CardSelector.StartSelecting(HeroManager.Player.ManaPercent < 80 ? Cards.Blue : Cards.Red);
                        break;
                }
            }  
        }
    }
}
