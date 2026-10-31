using System;
using System.IO;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    /// <summary>プロジェクトの slot_*.csv の台を適当打ちで大量に回し、理論値と比べる（AC-040）。</summary>
    public sealed class SlotSimulationTests
    {
        private const int Games = 1_000_000;
        private const int Batches = 20;

        private static readonly int[][] PressOrders =
        {
            new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 },
        };

        private static CsvTable ProjectTable(string fileName) =>
            CsvParser.Parse(fileName, File.ReadAllText(Path.Combine(TablePaths.Root, fileName)));

        private sealed class Tables
        {
            public readonly SlotReels Reels = SlotReels.Load(ProjectTable(SlotReels.FileName));
            public readonly SlotPayouts Payouts;
            public readonly SlotProbabilities Probabilities;
            public readonly SlotPremonition Premonition;
            public readonly SlotRules Rules = SlotRules.Load(ProjectTable(SlotRules.FileName));
            public readonly int Bet = GameConfig.Load(ProjectTable(GameConfig.FileName)).Bet;

            public Tables()
            {
                Payouts = SlotPayouts.Load(ProjectTable(SlotPayouts.FileName));
                Probabilities = SlotProbabilities.Load(ProjectTable(SlotProbabilities.FileName), Payouts);
                Premonition = SlotPremonition.Load(ProjectTable(SlotPremonition.FileName), Payouts);
            }

            private SlotTheoryResult[] _theory;

            public SlotTheoryResult Theory(int setting)
            {
                _theory ??= SlotTheory.CalculateAllSettings(Reels, Payouts, Probabilities, Rules, Bet, SlotPlayStyle.Random);
                return _theory[setting - 1];
            }
        }

        // 理論値と設定ごとのシミュレーションは重いので、この実行のテストで使い回す（CSV の変更を拾うよう実行ごとに作り直す）。
        private Tables _tables;
        private Measured?[] _simulations;

        [OneTimeSetUp]
        public void LoadTables()
        {
            _tables = new Tables();
            _simulations = new Measured?[SlotProbabilities.SettingCount];
        }

        private Measured Simulation(int setting)
        {
            _simulations[setting - 1] ??= Simulate(_tables, setting, (ulong)setting);
            return _simulations[setting - 1].Value;
        }

        /// <summary>機械割と BIG・REG の開始回数（1ゲームあたり）の、全体の値とバッチ平均の標準誤差。</summary>
        private struct Measured
        {
            public double PayoutRate, PayoutRateError, BigRate, BigRateError, RegRate, RegRateError;
        }

        private static Measured Simulate(Tables tables, int setting, ulong seed)
        {
            var wallet = new CoinWallet(int.MaxValue / 2);
            var control = new SlotReelControl(tables.Reels, tables.Payouts, tables.Rules.MaxSlide);
            var machine = new SlotMachine(tables.Reels, tables.Payouts, tables.Probabilities, tables.Premonition, control, tables.Rules,
                wallet, tables.Bet, setting, new SeededRandom(seed));
            var player = new SeededRandom(seed + 1);
            var payoutRates = new double[Batches];
            var bigRates = new double[Batches];
            var regRates = new double[Batches];
            long totalPaid = 0, totalBet = 0, totalBig = 0, totalReg = 0;
            for (var batch = 0; batch < Batches; batch++)
            {
                long paid = 0, bet = 0, big = 0, reg = 0;
                for (var game = 0; game < Games / Batches; game++)
                {
                    var wasBonus = machine.State == SlotState.Bonus;
                    var free = machine.IsReplay;
                    var before = wallet.Coins;
                    Assert.That(machine.TryLever(), Is.True);
                    var order = PressOrders[player.NextInt(0, PressOrders.Length)];
                    foreach (var reel in order)
                    {
                        machine.Stop(reel, player.NextInt(0, SlotReels.StopCount));
                    }

                    var gameBet = free ? 0 : tables.Bet;
                    bet += gameBet;
                    paid += wallet.Coins - before + gameBet;
                    if (!wasBonus && machine.State == SlotState.Bonus)
                    {
                        if (machine.ActiveBonus == SlotFlag.BigRole)
                        {
                            big++;
                        }
                        else
                        {
                            reg++;
                        }
                    }
                }

                const double batchGames = Games / Batches;
                payoutRates[batch] = (double)paid / bet;
                bigRates[batch] = big / batchGames;
                regRates[batch] = reg / batchGames;
                totalPaid += paid;
                totalBet += bet;
                totalBig += big;
                totalReg += reg;
            }

            return new Measured
            {
                PayoutRate = (double)totalPaid / totalBet,
                PayoutRateError = StandardError(payoutRates),
                BigRate = (double)totalBig / Games,
                BigRateError = StandardError(bigRates),
                RegRate = (double)totalReg / Games,
                RegRateError = StandardError(regRates),
            };
        }

        private static double StandardError(double[] values)
        {
            var mean = 0.0;
            foreach (var value in values)
            {
                mean += value / values.Length;
            }

            var variance = 0.0;
            foreach (var value in values)
            {
                variance += (value - mean) * (value - mean) / (values.Length - 1);
            }

            return Math.Sqrt(variance / values.Length);
        }

        [Test]
        [Description("AC-040: 固定シードで100万ゲーム適当打ちした機械割が、理論値から標準誤差の4倍以内（誤差は2%未満）")]
        public void Simulated_Payout_Rate_Matches_The_Theory([Range(1, 6)] int setting)
        {
            var measured = Simulation(setting);
            var theory = _tables.Theory(setting);

            Assert.That((measured.PayoutRateError < 0.02, Math.Abs(measured.PayoutRate - theory.PayoutRate) <= 4 * measured.PayoutRateError),
                Is.EqualTo((true, true)), $"実測 {measured.PayoutRate:F4} ± {measured.PayoutRateError:F4}、理論 {theory.PayoutRate:F4}");
        }

        [Test]
        [Description("AC-040: 固定シードで100万ゲーム適当打ちした BIG・REG の開始回数が、理論値から標準誤差の4倍以内")]
        public void Simulated_Bonus_Rates_Match_The_Theory([Range(1, 6)] int setting)
        {
            var measured = Simulation(setting);
            var theory = _tables.Theory(setting);

            Assert.That((Math.Abs(measured.BigRate - theory.BigRate) <= 4 * measured.BigRateError,
                    Math.Abs(measured.RegRate - theory.RegRate) <= 4 * measured.RegRateError), Is.EqualTo((true, true)),
                $"BIG 実測 {measured.BigRate:F6} ± {measured.BigRateError:F6} 理論 {theory.BigRate:F6}、" +
                $"REG 実測 {measured.RegRate:F6} ± {measured.RegRateError:F6} 理論 {theory.RegRate:F6}");
        }

        [Test]
        [Description("FR-045, plan 5章: 適当打ちの理論機械割が設定ごとの目標（100/104/108/112/118/125%）から ±0.5ポイント以内")]
        public void Theoretical_Payout_Rate_Meets_The_Target([Range(1, 6)] int setting)
        {
            var rate = _tables.Theory(setting).PayoutRate;

            Assert.That(rate, Is.EqualTo(SlotTheory.TargetPayoutRates[setting - 1]).Within(0.005));
        }
    }
}
