using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;

namespace SkinChanger
{
    internal class Program
    {
        public static List<ModelUnit> PlayerList = new List<ModelUnit>();
        public static ModelUnit Player;
        public static Menu Config;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static async void Game_OnGameLoad(EventArgs args)
        {
            var skins = Enumerable.Range(0, 53).Select(n => n.ToString()).ToArray();


            Config = new Menu("SkinChanger", "SkinChanger", true);
            //画英雄相关菜单.
           DrawChampionMenu();
            Config.AddToMainMenu();
            //画眼菜单.
            var wardMenu = Config.AddSubMenu(new Menu("Wards", "Wards"));
            wardMenu.AddItem(new MenuItem("Ward", "Reskin Wards").SetValue(true));
            wardMenu.AddItem(new MenuItem("WardOwn", "Reskin Only Own Wards").SetValue(true));
            wardMenu.AddItem(new MenuItem("WardIndex", "Ward Index").SetValue(new StringList(skins, 34))).ValueChanged
                += Program_ValueChanged;

            //画宠物菜单.
            var minions = Config.AddSubMenu(new Menu("Minions", "Minions"));
            //settings.AddItem(new MenuItem("Pets", "Reskin Pets").SetValue(true));
            minions.AddItem(new MenuItem("Minions", "Reskin Minions").SetValue(false));
            var mSkin =
                minions.AddItem(
                    new MenuItem("MinionType", "Minion Skin").SetValue(
                        new StringList(ModelManager.MinionSkins.Keys.ToArray())));
            minions.Item("Minions").ValueChanged +=
                delegate(object sender, OnValueChangeEventArgs eventArgs)
                {
                    ModelManager.ChangeMinionModels(
                        mSkin.GetValue<StringList>().SelectedValue, !eventArgs.GetNewValue<bool>());
                };
            mSkin.ValueChanged +=
                (sender, eventArgs) =>
                {
                    ModelManager.ChangeMinionModels(eventArgs.GetNewValue<StringList>().SelectedValue);
                };

            Game.OnInput += Game_OnInput;
        }

        private static void DrawChampionMenu()
        {
            var champs = Config.AddSubMenu(new Menu("Champions", "Champions"));
            champs.AddItem(new MenuItem("Champions", "Reskin Champions").SetValue(true));
            //var allies = champs.AddSubMenu(new Menu("Allies", "Allies"));
            //var enemies = champs.AddSubMenu(new Menu("Enemies", "Enemies"));
            foreach (var hero in HeroManager.AllHeroes.Where(h => !h.ChampionName.Equals("Ezreal")))
            {
                var champMenu = new Menu(hero.ChampionName, hero.ChampionName + hero.Team);
                var modelUnit = new ModelUnit(hero);

                PlayerList.Add(modelUnit);

                if (hero.IsMe)
                {
                    Player = modelUnit;
                }
                Task task1= Task.Run(() =>
                {
                    DrawChampionSkin(champs, champMenu, modelUnit, hero);
                });
                //task1.Start();
                //异步画出皮肤菜单.
                Console.WriteLine("返回当前操作英雄:{0}", hero.ChampionName);
            }
        }

        private static async void DrawChampionSkin(Menu champs, Menu champMenu, ModelUnit modelUnit, Obj_AI_Hero hero)
        {
            Console.WriteLine("当前操作英雄:{0}", hero.ChampionName);
            var skinList= ModelManager.GetSkins(hero.ChampionName);
            Console.WriteLine("任务完成");
            //var skinList = ModelManager.GetSkins(hero.ChampionName);
            foreach (Dictionary<string, object> skin in skinList)
            {
                var skinName = skin["name"].ToString().Equals("default")
                    ? hero.ChampionName
                    : skin["name"].ToString();
                var skinId = (int) skin["num"];
                Console.WriteLine("开始绘制皮肤:{0}", skinName);
                var changeSkin = champMenu.AddItem(new MenuItem(skinName, skinName).SetValue(false));
                if (!hero.IsMe)
                {
                    changeSkin.DontSave();
                }
                if (hero.BaseSkinId.Equals(skinId))
                {
                    changeSkin.SetValue(true);
                }
                //如果当前选项为打开状态，且用户模型不等同于当前模型的
                if (changeSkin.IsActive()
                    && !hero.CharData.BaseSkinName.Equals(skinName)
                    && hero.IsMe)
                {
                    //设置之前设置过的菜单项目为false
                    champMenu.Items.Find(h => h.DisplayName.Equals(hero.CharData.BaseSkinName)).SetValue(false);
                    changeSkin.SetValue(true);
                    //初始设置皮肤.
                    modelUnit.SetModel(hero.CharData.BaseSkinName, skinId);
                }

                //否则设置菜单项目为TRUE;
                var hero1 = hero;
                changeSkin.ValueChanged += (s, e) =>
                {
                    if (e.GetNewValue<bool>())
                    {
                        champMenu.Items.ForEach(
                            p =>
                            {
                                if (p.IsActive() && p.Name != skinName)
                                {
                                    p.SetValue(false);
                                }
                            });
                        modelUnit.SetModel(hero1.ChampionName, skinId);
                    }
                };
            }
            var rootMenu = hero.IsAlly ? champs.SubMenu("allies") : champs.SubMenu("enemies");
            rootMenu.AddSubMenu(champMenu);
        }


        private static void Program_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            foreach (var ward in
                ObjectManager.Get<Obj_AI_Base>()
                    .Where(o => o.CharData.BaseSkinName.Contains("ward"))
                    .Where(
                        ward =>
                            !Config.Item("WardOwn").IsActive() ||
                            ward.Buffs.Any(b => b.SourceName.Equals(ObjectManager.Player.ChampionName))))
            {
                ward.SetSkin(ward.CharData.BaseSkinName, Convert.ToInt32(e.GetNewValue<StringList>().SelectedValue));
            }
        }

        private static void Game_OnInput(GameInputEventArgs args)
        {
            if (!args.Input.StartsWith("/"))
            {
                return;
            }

            if (args.Input.StartsWith("/model"))
            {
                args.Process = false;
                var model = args.Input.Replace("/model ", string.Empty).GetValidModel();

                if (!model.IsValidModel())
                {
                    return;
                }

                Player.SetModel(model);
                return;
            }

            if (args.Input.StartsWith("/skin"))
            {
                args.Process = false;
                try
                {
                    var skin = Convert.ToInt32(args.Input.Replace("/skin ", string.Empty));
                    Player.SetModel(Player.Unit.CharData.BaseSkinName, skin);
                }
                catch {}
            }
        }
    }
}