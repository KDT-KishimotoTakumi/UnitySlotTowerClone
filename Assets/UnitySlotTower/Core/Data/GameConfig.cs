namespace UnitySlotTower.Core.Data
{
    /// <summary>game_config.csv の値（plan 5章 コイン経済）。</summary>
    public sealed class GameConfig
    {
        public const string FileName = "game_config.csv";

        public int InitialCoins { get; private set; }
        public int Bet { get; private set; }
        public int Life { get; private set; }
        public int LifeBonusPerLife { get; private set; }
        public int DeployLimit { get; private set; }
        public double RetreatRefundRate { get; private set; }

        public static GameConfig Load(CsvTable table)
        {
            var values = new KeyValueTable(table);
            return new GameConfig
            {
                InitialCoins = values.GetInt("initial_coins"),
                Bet = values.GetInt("bet"),
                Life = values.GetInt("life"),
                LifeBonusPerLife = values.GetInt("life_bonus_per_life"),
                DeployLimit = values.GetInt("deploy_limit"),
                RetreatRefundRate = values.GetDouble("retreat_refund_rate"),
            };
        }
    }
}
