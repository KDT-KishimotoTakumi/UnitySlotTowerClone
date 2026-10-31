using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Editor.Slot
{
    /// <summary>
    /// slot_*.csv から設定1〜6の理論機械割を計算して Console に出す（plan 5章、AC-040）。
    /// 目標は適当打ちの値。完全攻略は参考値。
    /// </summary>
    public static class SlotTheoryReport
    {
        public const string TableFolder = "Assets/UnitySlotTower/Data/Tables";

        public const string ReportPath = "Artifacts/Slot/slot-theory.txt";

        /// <summary>Console に出し、自動化から読めるように <see cref="ReportPath"/> にも書く。</summary>
        [MenuItem("Tools/UnitySlotTower/Slot/Theoretical Payout Rate")]
        private static void Report()
        {
            var report = Build();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report);
            Debug.Log(report);
        }

        public static string Build()
        {
            var reels = SlotReels.Load(Table(SlotReels.FileName));
            var payouts = SlotPayouts.Load(Table(SlotPayouts.FileName));
            var probabilities = SlotProbabilities.Load(Table(SlotProbabilities.FileName), payouts);
            var rules = SlotRules.Load(Table(SlotRules.FileName));
            var bet = GameConfig.Load(Table(GameConfig.FileName)).Bet;

            var text = new StringBuilder("[SlotTheory] setting: random (target) / perfect / BIG 1/x / REG 1/x (random)\n");
            var randoms = SlotTheory.CalculateAllSettings(reels, payouts, probabilities, rules, bet, SlotPlayStyle.Random);
            var perfects = SlotTheory.CalculateAllSettings(reels, payouts, probabilities, rules, bet, SlotPlayStyle.Perfect);
            for (var setting = 1; setting <= SlotProbabilities.SettingCount; setting++)
            {
                var random = randoms[setting - 1];
                var perfect = perfects[setting - 1];
                text.AppendLine(
                    $"{setting}: {random.PayoutRate:P2} ({SlotTheory.TargetPayoutRates[setting - 1]:P0}) / {perfect.PayoutRate:P2} / " +
                    $"{1 / random.BigRate:F1} / {1 / random.RegRate:F1}");
            }

            return text.ToString();
        }

        private static CsvTable Table(string fileName) =>
            CsvParser.Parse(fileName, File.ReadAllText(Path.Combine(TableFolder, fileName)));
    }
}
