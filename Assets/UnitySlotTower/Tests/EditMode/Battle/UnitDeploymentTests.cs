using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Economy;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("FR-002, FR-023: 味方を選び、マスと向きを決めて置く操作")]
    public sealed class UnitDeploymentTests
    {
        // フィクスチャの地図（4×2）。地上マスは (1,0)(2,0)、高台マスは (1,1)(3,1)、(2,1) は通行不可。
        //   S1 G G E
        //   S2 H X H
        private CoinWallet _wallet;
        private Battlefield _field;

        private UnitSummoner _summoner;

        private UnitDeployment Create(int coins = 100, string waveRows = "")
        {
            _wallet = new CoinWallet(coins);
            var units = BattleFixtures.LoadUnits();
            _field = BattleFixtures.CreateField(waveRows);
            _summoner = new UnitSummoner(units, _wallet, 8, 0.5);
            return new UnitDeployment(BattleFixtures.LoadMap(), units, BattleFixtures.LoadRanges(), _summoner, _field,
                new UnitUpgrades(BattleFixtures.LoadUpgrades(), _wallet));
        }

        [Test]
        public void Selecting_A_Unit_Starts_Choosing_A_Cell()
        {
            var deployment = Create();

            deployment.Select("tank");

            Assert.That((deployment.Phase, deployment.Selected?.Id), Is.EqualTo((DeployPhase.ChoosingCell, "tank")));
        }

        [Test]
        public void Selection_Is_Rejected_When_Coins_Are_Short()
        {
            var deployment = Create(coins: 10);

            var result = deployment.Select("tank");

            Assert.That((result, deployment.Phase), Is.EqualTo((SummonResult.NotEnoughCoins, DeployPhase.Idle)));
        }

        [Test]
        public void Selection_Does_Not_Spend_Coins()
        {
            var deployment = Create(coins: 30);

            deployment.Select("tank");

            Assert.That(_wallet.Coins, Is.EqualTo(30));
        }

        [Test]
        public void Cursor_Starts_At_The_Map_Center()
        {
            var deployment = Create();

            deployment.Select("tank");

            Assert.That(deployment.Cursor, Is.EqualTo(new GridPoint(2, 1)));
        }

        [Test]
        public void Cursor_Moves_With_Arrows_And_Stays_Inside_The_Map()
        {
            var deployment = Create();
            deployment.Select("tank");

            deployment.MoveCursor(1, 0);
            deployment.MoveCursor(1, 0);
            deployment.MoveCursor(0, -1);

            Assert.That(deployment.Cursor, Is.EqualTo(new GridPoint(3, 0)));
        }

        [Test]
        public void Cursor_Ignores_Cells_Outside_The_Map()
        {
            var deployment = Create();
            deployment.Select("tank");

            deployment.SetCursor(new GridPoint(4, 0));

            Assert.That(deployment.Cursor, Is.EqualTo(new GridPoint(2, 1)));
        }

        [Test]
        public void A_Ground_Unit_Can_Choose_A_Ground_Cell()
        {
            var deployment = Create();
            deployment.Select("tank");
            deployment.SetCursor(new GridPoint(1, 0));

            Assert.That((deployment.ChooseCell(), deployment.Phase), Is.EqualTo((true, DeployPhase.ChoosingFacing)));
        }

        [Test]
        public void A_Ground_Unit_Cannot_Choose_A_High_Ground_Cell()
        {
            var deployment = Create();
            deployment.Select("tank");
            deployment.SetCursor(new GridPoint(1, 1));

            Assert.That((deployment.ChooseCell(), deployment.Phase), Is.EqualTo((false, DeployPhase.ChoosingCell)));
        }

        [Test]
        public void A_Cell_With_Another_Unit_Cannot_Be_Chosen()
        {
            var deployment = Create();
            _field.AddUnit(BattleFixtures.LoadUnits().Get("sword"), new GridPoint(1, 0), Facing.Left);
            deployment.Select("tank");
            deployment.SetCursor(new GridPoint(1, 0));

            Assert.That(deployment.ChooseCell(), Is.False);
        }

        [Test]
        public void Cancel_While_Choosing_A_Cell_Deselects()
        {
            var deployment = Create();
            deployment.Select("tank");

            deployment.Cancel();

            Assert.That((deployment.Phase, deployment.Selected), Is.EqualTo((DeployPhase.Idle, (UnitDefinition)null)));
        }

        private UnitDeployment CreateChoosingFacing(int coins = 100)
        {
            var deployment = Create(coins);
            deployment.Select("tank");
            deployment.SetCursor(new GridPoint(2, 0));
            deployment.ChooseCell();
            return deployment;
        }

        [Test]
        public void Arrows_Choose_The_Facing_Without_Moving_The_Cursor()
        {
            var deployment = CreateChoosingFacing();

            deployment.MoveCursor(0, -1);

            Assert.That((deployment.Facing, deployment.Cursor), Is.EqualTo((Facing.Up, new GridPoint(2, 0))));
        }

        [Test]
        public void Preview_Range_Follows_The_Facing()
        {
            var deployment = CreateChoosingFacing();

            deployment.SetFacing(Facing.Left);

            // tank の範囲は自マス＋前1。
            Assert.That(deployment.PreviewRange, Is.EquivalentTo(new[] { new GridPoint(2, 0), new GridPoint(1, 0) }));
        }

        [Test]
        public void Confirm_Places_The_Unit_At_The_Cell_With_The_Facing()
        {
            var deployment = CreateChoosingFacing();
            deployment.SetFacing(Facing.Left);

            deployment.Confirm();

            var unit = _field.Units[0];
            Assert.That((unit.Definition.Id, unit.Cell, unit.Facing), Is.EqualTo(("tank", new GridPoint(2, 0), Facing.Left)));
        }

        [Test]
        public void Confirm_Spends_The_Cost_And_Ends_The_Operation()
        {
            var deployment = CreateChoosingFacing(coins: 30);

            var result = deployment.Confirm();

            Assert.That((result, _wallet.Coins, deployment.Phase), Is.EqualTo((SummonResult.Summoned, 15, DeployPhase.Idle)));
        }

        [Test]
        public void Confirm_Fails_When_Coins_Ran_Short_Meanwhile()
        {
            var deployment = CreateChoosingFacing(coins: 30);
            _wallet.TrySpend(20);

            var result = deployment.Confirm();

            Assert.That((result, _field.Units.Count, deployment.Phase),
                Is.EqualTo((SummonResult.NotEnoughCoins, 0, DeployPhase.Idle)));
        }

        [Test]
        public void Confirm_Does_Nothing_While_Choosing_A_Cell()
        {
            var deployment = Create();
            deployment.Select("tank");

            deployment.Confirm();

            Assert.That((_field.Units.Count, deployment.Phase), Is.EqualTo((0, DeployPhase.ChoosingCell)));
        }

        [Test]
        public void Cancel_While_Choosing_The_Facing_Returns_To_Choosing_A_Cell()
        {
            var deployment = CreateChoosingFacing(coins: 30);

            deployment.Cancel();

            Assert.That((deployment.Phase, deployment.Cursor, _wallet.Coins),
                Is.EqualTo((DeployPhase.ChoosingCell, new GridPoint(2, 0), 30)));
        }

        [Test]
        public void Selecting_Again_While_Choosing_The_Facing_Restarts_From_The_Cell_Facing_Right()
        {
            var deployment = CreateChoosingFacing();
            deployment.SetFacing(Facing.Up);

            deployment.Select("sword");

            Assert.That((deployment.Phase, deployment.Facing, deployment.Cursor),
                Is.EqualTo((DeployPhase.ChoosingCell, Facing.Right, new GridPoint(2, 0))));
        }

        [Test]
        public void The_Next_Selection_Starts_From_The_Last_Cursor_And_Facing_Right()
        {
            var deployment = CreateChoosingFacing();
            deployment.SetFacing(Facing.Up);
            deployment.Confirm();

            deployment.Select("sword");
            deployment.SetCursor(new GridPoint(1, 0));
            deployment.ChooseCell();

            Assert.That((deployment.Cursor, deployment.Facing), Is.EqualTo((new GridPoint(1, 0), Facing.Right)));
        }

        [Test]
        [Description("FR-025")]
        public void Retreat_Removes_The_Unit_And_Refunds_Half_The_Cost()
        {
            var deployment = CreateChoosingFacing(coins: 30);
            deployment.Confirm();

            deployment.Retreat(_field.Units[0]);

            // 30 − 15 + floor(15 × 0.5)
            Assert.That((_field.Units.Count, _wallet.Coins), Is.EqualTo((0, 22)));
        }

        [Test]
        [Description("FR-021: 倒された味方は配置数から外れる（返還はない）")]
        public void A_Fallen_Unit_Frees_A_Deploy_Slot_Without_Refund()
        {
            var deployment = Create(coins: 30, waveRows: "1,0,brute,S1,g,0\n");
            deployment.Select("sword");
            deployment.SetCursor(new GridPoint(1, 0));
            deployment.ChooseCell();
            deployment.Confirm();
            var coinsAfterSummon = _wallet.Coins;

            _field.Advance(100);

            Assert.That((_field.Units.Count, _summoner.DeployedCount, _wallet.Coins), Is.EqualTo((0, 0, coinsAfterSummon)));
        }
    }
}
