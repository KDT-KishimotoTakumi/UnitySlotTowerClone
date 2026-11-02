using System.IO;
using NUnit.Framework;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Tests.EditMode.Data
{
    public sealed class GameConfigTests
    {
        private const string ValidConfig =
            "key,value\ninitial_coins,30\nbet,3\nlife,10\nlife_bonus_per_life,3\ndeploy_limit,8\nretreat_refund_rate,0.5\n";

        [Test]
        public void Values_Are_Read_By_Key()
        {
            var config = GameConfig.Load(CsvParser.Parse("game_config.csv", ValidConfig));

            Assert.That((config.InitialCoins, config.Bet, config.Life, config.LifeBonusPerLife, config.DeployLimit,
                    config.RetreatRefundRate),
                Is.EqualTo((30, 3, 10, 3, 8, 0.5)));
        }

        [Test]
        public void Missing_Key_Is_Reported_With_Its_Name()
        {
            var text = ValidConfig.Replace("bet,3\n", string.Empty);

            var ex = Assert.Throws<CsvFormatException>(() => GameConfig.Load(CsvParser.Parse("game_config.csv", text)));
            Assert.That(ex.Error.Message, Does.Contain("bet"));
        }

        [Test]
        public void Invalid_Value_Reports_Its_Line()
        {
            var text = ValidConfig.Replace("life,10", "life,ten");

            var ex = Assert.Throws<CsvFormatException>(() => GameConfig.Load(CsvParser.Parse("game_config.csv", text)));
            Assert.That(ex.Error.Line, Is.EqualTo(4));
        }

        [Test]
        [Description("plan 5章 コイン経済の初期値")]
        public void Project_Table_Holds_Planned_Initial_Values()
        {
            var text = File.ReadAllText(Path.Combine(TablePaths.Root, "game_config.csv"));

            var config = GameConfig.Load(CsvParser.Parse("game_config.csv", text));

            Assert.That((config.InitialCoins, config.Bet, config.Life, config.LifeBonusPerLife, config.DeployLimit,
                    config.RetreatRefundRate),
                Is.EqualTo((30, 3, 10, 3, 8, 0.5)));
        }
    }
}
