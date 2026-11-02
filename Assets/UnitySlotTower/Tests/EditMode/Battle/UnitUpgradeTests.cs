using System.IO;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("FR-026: 種類ごとの段階的な強化と支払い")]
    public sealed class UnitUpgradeTests
    {
        // 表は BattleFixtures.Upgrades（sword: 3段階、archer: 2段階、tank は行がない）。
        private static UnitUpgradeTable LoadTable(string csv = BattleFixtures.Upgrades) =>
            BattleFixtures.LoadUpgrades(csv);

        [Test]
        public void Upgrade_Pays_The_Next_Level_Cost()
        {
            var wallet = new CoinWallet(100);
            var upgrades = new UnitUpgrades(LoadTable(), wallet);

            var result = upgrades.TryUpgrade("sword");

            Assert.That((result, upgrades.Level("sword"), wallet.Coins), Is.EqualTo((UpgradeResult.Upgraded, 2, 80)));
        }

        [Test]
        public void Next_Cost_Is_The_Cost_Of_The_Next_Level()
        {
            var upgrades = new UnitUpgrades(LoadTable(), new CoinWallet(100));
            upgrades.TryUpgrade("sword");

            Assert.That(upgrades.NextCost("sword"), Is.EqualTo(35));
        }

        [Test]
        public void Upgrade_Is_Rejected_When_Coins_Are_Short()
        {
            var wallet = new CoinWallet(19);
            var upgrades = new UnitUpgrades(LoadTable(), wallet);

            var result = upgrades.TryUpgrade("sword");

            Assert.That((result, upgrades.Level("sword"), wallet.Coins),
                Is.EqualTo((UpgradeResult.NotEnoughCoins, 1, 19)));
        }

        [Test]
        public void Upgrade_Is_Rejected_At_The_Max_Level()
        {
            var wallet = new CoinWallet(100);
            var upgrades = new UnitUpgrades(LoadTable(), wallet);
            upgrades.TryUpgrade("archer");

            var result = upgrades.TryUpgrade("archer");

            Assert.That((result, upgrades.Level("archer"), wallet.Coins, upgrades.NextCost("archer")),
                Is.EqualTo((UpgradeResult.MaxLevel, 2, 70, (int?)null)));
        }

        [Test]
        public void Unit_Without_Rows_Cannot_Be_Upgraded()
        {
            var upgrades = new UnitUpgrades(LoadTable(), new CoinWallet(100));

            Assert.That((upgrades.TryUpgrade("tank"), upgrades.Current("tank").AtkMult),
                Is.EqualTo((UpgradeResult.MaxLevel, 1.0)));
        }

        [Test]
        public void Upgrade_Changes_Only_That_Unit_Type()
        {
            var upgrades = new UnitUpgrades(LoadTable(), new CoinWallet(100));

            upgrades.TryUpgrade("sword");

            Assert.That((upgrades.Current("sword").HpMult, upgrades.Level("archer")), Is.EqualTo((1.5, 1)));
        }

        [Test]
        public void Missing_Level_Is_Rejected()
        {
            Assert.Throws<CsvFormatException>(() =>
                LoadTable("unit_id,level,hp_mult,atk_mult,def_mult,cost\nsword,1,1,1,1,0\nsword,3,2,1.5,3,35\n"));
        }

        [Test]
        public void Unknown_Unit_Is_Rejected()
        {
            Assert.Throws<CsvFormatException>(() =>
                LoadTable("unit_id,level,hp_mult,atk_mult,def_mult,cost\nghost,1,1,1,1,0\n"));
        }

        [Test]
        [Description("plan 5章: 段階1〜5、強化費用（段階2〜5）20 / 35 / 55 / 80")]
        public void Project_Table_Has_Five_Levels_With_Planned_Costs_For_Every_Unit()
        {
            var units = UnitTable.Load(CsvParser.Parse(UnitTable.FileName,
                File.ReadAllText(Path.Combine(TablePaths.Root, UnitTable.FileName))));
            var table = UnitUpgradeTable.Load(CsvParser.Parse(UnitUpgradeTable.FileName,
                File.ReadAllText(Path.Combine(TablePaths.Root, UnitUpgradeTable.FileName))), units);

            foreach (var unit in units.All)
            {
                Assert.That(new[]
                    {
                        table.MaxLevel(unit.Id), table.Get(unit.Id, 2).Cost, table.Get(unit.Id, 3).Cost,
                        table.Get(unit.Id, 4).Cost, table.Get(unit.Id, 5).Cost,
                    },
                    Is.EqualTo(new[] { 5, 20, 35, 55, 80 }), unit.Id);
            }
        }
    }

    /// <summary>
    /// AC-022: 強化が配置中と今後の同種個体の両方に反映される（plan 5章）。剣士の段階2は HP 1200→1800・攻撃 400→500・防御 50→100、
    /// 弓の段階2は攻撃 350→700、ヒーラーの段階2は攻撃（回復量）200→300。
    /// </summary>
    [Description("FR-026, AC-022: 強化の反映")]
    public sealed class UnitUpgradeEffectTests
    {
        private CoinWallet _wallet;
        private Battlefield _field;

        private UnitDeployment Create(string waveRows = "", int coins = 1000)
        {
            _wallet = new CoinWallet(coins);
            var units = BattleFixtures.LoadUnits();
            _field = BattleFixtures.CreateField(waveRows);
            return new UnitDeployment(BattleFixtures.LoadMap(), units, BattleFixtures.LoadRanges(),
                new UnitSummoner(units, _wallet, 8, 0.5), _field, new UnitUpgrades(BattleFixtures.LoadUpgrades(), _wallet));
        }

        private BattleUnit Deploy(UnitDeployment deployment, string unitId, int x, int y)
        {
            deployment.Select(unitId);
            deployment.SetCursor(new GridPoint(x, y));
            deployment.ChooseCell();
            deployment.Confirm();
            return _field.Units[_field.Units.Count - 1];
        }

        [Test]
        public void Upgrade_Raises_The_Stats_Of_Deployed_Units_Of_That_Type()
        {
            var deployment = Create();
            var sword = Deploy(deployment, "sword", 1, 0);

            deployment.Upgrade("sword");

            Assert.That((sword.MaxHp, sword.Atk, sword.Def), Is.EqualTo((1800, 500, 100)));
        }

        [Test]
        public void Upgrade_Leaves_Other_Unit_Types_Unchanged()
        {
            var deployment = Create();
            var archer = Deploy(deployment, "archer", 1, 1);

            deployment.Upgrade("sword");

            Assert.That((archer.MaxHp, archer.Atk), Is.EqualTo((700, 350)));
        }

        [Test]
        public void Upgrade_Applies_To_Units_Placed_Afterwards_With_Full_Hp()
        {
            var deployment = Create();
            deployment.Upgrade("sword");

            var sword = Deploy(deployment, "sword", 1, 0);

            Assert.That((sword.MaxHp, sword.Hp, sword.Atk), Is.EqualTo((1800, 1800, 500)));
        }

        [Test]
        public void Upgrade_Pays_From_The_Shared_Wallet()
        {
            var deployment = Create(coins: 100);

            var result = deployment.Upgrade("sword");

            Assert.That((result, _wallet.Coins), Is.EqualTo((UpgradeResult.Upgraded, 80)));
        }

        [Test]
        [Description("plan 5章: 最大HPが増えた分は現在HPにも加算")]
        public void Max_Hp_Increase_Is_Added_To_The_Current_Hp()
        {
            // 近距離の敵（攻撃200）に1回殴られて 1200 − (200 − 50) = 1050。
            var deployment = Create("1,0,melee,S1,g,0\n");
            var sword = Deploy(deployment, "sword", 1, 0);
            _field.Advance(40);

            deployment.Upgrade("sword");

            Assert.That(sword.Hp, Is.EqualTo(1050 + 600));
        }

        [Test]
        public void Upgraded_Defense_Reduces_Damage_Taken()
        {
            var deployment = Create("1,0,melee,S1,g,0\n");
            deployment.Upgrade("sword");
            var sword = Deploy(deployment, "sword", 1, 0);

            _field.Advance(40);

            Assert.That(sword.Hp, Is.EqualTo(1800 - (200 - 100)));
        }

        [Test]
        public void Upgraded_Attack_Raises_Damage_Dealt()
        {
            var deployment = Create("1,0,melee,S1,g,0\n");
            deployment.Upgrade("archer");
            Deploy(deployment, "archer", 1, 1);

            _field.Advance(45);

            Assert.That(_field.Enemies[0].Hp, Is.EqualTo(1000 - (700 - 50)));
        }

        [Test]
        [Description("plan 5章: 回復量は強化後の攻撃力で、強化後の最大HPを超えない")]
        public void Healer_Heals_Up_To_The_Upgraded_Max_Hp()
        {
            // 剣士は 1050/1200 で強化して 1650/1800。強化したヒーラーの回復 300 は 1800 で止まる。
            var deployment = Create("1,0,melee,S1,g,0\n");
            var sword = Deploy(deployment, "sword", 1, 0);
            _field.Advance(40);
            deployment.Upgrade("sword");
            deployment.Upgrade("healer");
            Deploy(deployment, "healer", 1, 1);

            _field.Advance(1);

            Assert.That(sword.Hp, Is.EqualTo(1800));
        }

        [Test]
        public void Upgraded_Healer_Heals_By_Its_Upgraded_Attack()
        {
            // 近距離の敵に2回殴られて 1200 − 2 × 150 = 900（2回目は1回目の 1.5 秒後）。
            var deployment = Create("1,0,melee,S1,g,0\n");
            var sword = Deploy(deployment, "sword", 1, 0);
            _field.Advance(130);
            deployment.Upgrade("healer");
            Deploy(deployment, "healer", 1, 1);

            _field.Advance(1);

            Assert.That(sword.Hp, Is.EqualTo(900 + 300));
        }
    }
}
