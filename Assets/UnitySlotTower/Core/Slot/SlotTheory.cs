using System;
using System.Collections.Generic;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>押し方のモデル（plan 5章）。</summary>
    public enum SlotPlayStyle
    {
        /// <summary>適当打ち。押し順6通りと各リールの押し位置を一様ランダムに選ぶ。機械割の目標はこの値。</summary>
        Random,

        /// <summary>完全攻略。毎ゲーム、揃うボーナス役→払い出しの順に最大になる押し方を選ぶ（参考値）。</summary>
        Perfect,
    }

    /// <summary>理論値。ゲーム数はボーナス中を含む全ゲーム。</summary>
    public sealed class SlotTheoryResult
    {
        /// <summary>機械割（払い出し / 投入）。</summary>
        public double PayoutRate { get; internal set; }

        /// <summary>役 → 1ゲームあたりの揃う回数。</summary>
        public IReadOnlyDictionary<string, double> RoleRates { get; internal set; }

        /// <summary>1ゲームあたりの BIG・REG の開始回数。</summary>
        public double BigRate => Rate(SlotFlag.BigRole);

        public double RegRate => Rate(SlotFlag.RegRole);

        public double Rate(string role) => RoleRates.TryGetValue(role, out var rate) ? rate : 0;
    }

    /// <summary>
    /// テーブルから設定ごとの理論機械割を計算する（plan 5章、AC-040）。
    /// 1ゲームの出目の分布はフラグ×持ち越しごとに押し方を数え上げ、確変残りゲーム数×持ち越しの状態遷移の定常分布で平均する。
    /// ボーナスは上限までの払い出しを1回の遷移にまとめる。
    /// </summary>
    public static class SlotTheory
    {
        /// <summary>設定1〜6の機械割の目標（plan 5章、適当打ち）。</summary>
        public static readonly double[] TargetPayoutRates = { 1.00, 1.04, 1.08, 1.12, 1.18, 1.25 };

        public static SlotTheoryResult Calculate(SlotReels reels, SlotPayouts payouts, SlotProbabilities probabilities, SlotRules rules,
            int bet, int setting, SlotPlayStyle style)
        {
            return new Calculator(reels, payouts, probabilities, rules, style).Run(bet, setting);
        }

        /// <summary>設定1〜6の理論値（添字は設定-1）。出目の分布は設定によらないので1回だけ数え上げる。</summary>
        public static SlotTheoryResult[] CalculateAllSettings(SlotReels reels, SlotPayouts payouts, SlotProbabilities probabilities,
            SlotRules rules, int bet, SlotPlayStyle style)
        {
            var calculator = new Calculator(reels, payouts, probabilities, rules, style);
            var results = new SlotTheoryResult[SlotProbabilities.SettingCount];
            for (var setting = 1; setting <= results.Length; setting++)
            {
                results[setting - 1] = calculator.Run(bet, setting);
            }

            return results;
        }

        private sealed class Outcome
        {
            public double Probability;
            public int Payout;
            public string Bonus;
            public double[] Reward;
        }

        private sealed class Calculator
        {
            // 報酬ベクトルの添字。役ごとの揃った数は RoleIndex から払い出し表の順。
            private const int PayoutIndex = 0;
            private const int GameIndex = 1;
            private const int ReplayIndex = 2;
            private const int RoleIndex = 3;

            private static readonly int[][] PressOrders =
            {
                new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 },
            };

            private static readonly string[] Carried = { null, SlotFlag.BigRole, SlotFlag.RegRole };

            private readonly SlotReels _reels;
            private readonly SlotPayouts _payouts;
            private readonly SlotProbabilities _probabilities;
            private readonly SlotRules _rules;
            private readonly SlotReelControl _control;
            private int _setting;
            private readonly SlotPlayStyle _style;
            private readonly int _rewardLength;
            private readonly Dictionary<string, List<Outcome>> _outcomes = new Dictionary<string, List<Outcome>>();
            private readonly Dictionary<string, double[]> _bonusRewards = new Dictionary<string, double[]>();

            public Calculator(SlotReels reels, SlotPayouts payouts, SlotProbabilities probabilities, SlotRules rules,
                SlotPlayStyle style)
            {
                _reels = reels;
                _payouts = payouts;
                _probabilities = probabilities;
                _rules = rules;
                _control = new SlotReelControl(reels, payouts, rules.MaxSlide);
                _style = style;
                _rewardLength = RoleIndex + payouts.All.Count;
            }

            // 出目の分布（_outcomes）は設定によらず使い回し、ボーナスの報酬は設定ごとに計算し直す。
            public SlotTheoryResult Run(int bet, int setting)
            {
                _setting = setting;
                _bonusRewards.Clear();

                // 状態 = 確変残りゲーム数 k（0 は通常）× 持ち越し。遷移と1遷移あたりの報酬の期待値。
                var kakuhenGames = _rules.KakuhenGames;
                var count = (kakuhenGames + 1) * Carried.Length;
                var transitions = new double[count, count];
                var rewards = new double[count][];
                var bonusStarts = new Dictionary<string, double>[count];
                for (var k = 0; k <= kakuhenGames; k++)
                {
                    for (var c = 0; c < Carried.Length; c++)
                    {
                        var from = StateIndex(k, c);
                        rewards[from] = new double[_rewardLength];
                        bonusStarts[from] = new Dictionary<string, double>();
                        Step(k, c, transitions, rewards[from], bonusStarts[from]);
                    }
                }

                // ボーナスの報酬は、通常・持ち越しなしから到達する状態だけで計算する（使わないボーナス表は要らない）。
                var reachable = Reachable(transitions, StateIndex(0, 0));
                for (var s = 0; s < count; s++)
                {
                    if (!reachable[s])
                    {
                        continue;
                    }

                    foreach (var start in bonusStarts[s])
                    {
                        Add(rewards[s], BonusReward(start.Key), start.Value);
                    }
                }

                var stationary = Stationary(transitions, reachable, StateIndex(0, 0));
                var total = new double[_rewardLength];
                for (var s = 0; s < count; s++)
                {
                    if (reachable[s])
                    {
                        Add(total, rewards[s], stationary[s]);
                    }
                }

                var roleRates = new Dictionary<string, double>();
                for (var i = 0; i < _payouts.All.Count; i++)
                {
                    roleRates[_payouts.All[i].Role] = total[RoleIndex + i] / total[GameIndex];
                }

                return new SlotTheoryResult
                {
                    PayoutRate = total[PayoutIndex] / (bet * (total[GameIndex] - total[ReplayIndex])),
                    RoleRates = roleRates,
                };
            }

            private static int StateIndex(int kakuhenLeft, int carried) => kakuhenLeft * Carried.Length + carried;

            // SlotMachine.TryLever〜Finish と同じ規則で1ゲーム（ボーナスを揃えたらボーナス終了まで）進める。
            // 揃えたボーナスは確率を bonusStarts に足し、ボーナス中の報酬はあとで加える。
            private void Step(int k, int c, double[,] transitions, double[] reward, Dictionary<string, double> bonusStarts)
            {
                var from = StateIndex(k, c);
                var state = k > 0 ? SlotState.Kakuhen : SlotState.Normal;
                var carried = Carried[c];
                var nextK = k > 0 ? k - 1 : 0;
                foreach (var drawn in _probabilities.Flags)
                {
                    var probability = _probabilities.Probability(drawn, state, _setting);
                    if (probability <= 0)
                    {
                        continue;
                    }

                    var flag = carried != null ? _probabilities.WithoutBonus(drawn) : drawn;
                    foreach (var outcome in Outcomes(flag, carried))
                    {
                        var p = probability * outcome.Probability;
                        Add(reward, outcome.Reward, p);
                        if (outcome.Bonus != null)
                        {
                            bonusStarts[outcome.Bonus] = (bonusStarts.TryGetValue(outcome.Bonus, out var started) ? started : 0) + p;
                            var entry = outcome.Bonus == SlotFlag.BigRole ? _rules.KakuhenEntryRate : 0;
                            transitions[from, StateIndex(_rules.KakuhenGames, 0)] += p * entry;
                            transitions[from, StateIndex(nextK, 0)] += p * (1 - entry);
                            continue;
                        }

                        var nextCarried = c;
                        foreach (var role in flag.Roles)
                        {
                            if (SlotFlag.IsBonusRole(role))
                            {
                                nextCarried = Array.IndexOf(Carried, role);
                            }
                        }

                        transitions[from, StateIndex(nextK, nextCarried)] += p;
                    }
                }
            }

            // ボーナス開始から終了までの報酬の期待値（揃うボーナスだけ計算する）。expected[b] は累計 b 枚から終了までの分。
            private double[] BonusReward(string bonus)
            {
                if (_bonusRewards.TryGetValue(bonus, out var cached))
                {
                    return cached;
                }

                var limit = bonus == SlotFlag.BigRole ? _rules.BigPayoutLimit : _rules.RegPayoutLimit;
                var outcomes = new List<Outcome>();
                foreach (var flag in _probabilities.Flags)
                {
                    var probability = _probabilities.Probability(flag, SlotState.Bonus, _setting);
                    if (probability <= 0)
                    {
                        continue;
                    }

                    foreach (var outcome in Outcomes(flag, null))
                    {
                        if (outcome.Bonus != null)
                        {
                            throw new InvalidOperationException($"ボーナス中にボーナス役が揃う: '{flag.Id}'");
                        }

                        outcomes.Add(new Outcome { Probability = probability * outcome.Probability, Payout = outcome.Payout, Reward = outcome.Reward });
                    }
                }

                var expected = new double[limit][];
                for (var b = limit - 1; b >= 0; b--)
                {
                    var sum = new double[_rewardLength];
                    var stay = 0.0;
                    foreach (var outcome in outcomes)
                    {
                        Add(sum, outcome.Reward, outcome.Probability);
                        if (outcome.Payout == 0)
                        {
                            stay += outcome.Probability;
                        }
                        else if (b + outcome.Payout < limit)
                        {
                            Add(sum, expected[b + outcome.Payout], outcome.Probability);
                        }
                    }

                    if (stay >= 1 - 1e-12)
                    {
                        throw new InvalidOperationException("ボーナス中に払い出しがなく、ボーナスが終わらない");
                    }

                    for (var i = 0; i < sum.Length; i++)
                    {
                        sum[i] /= 1 - stay;
                    }

                    expected[b] = sum;
                }

                var reward = limit > 0 ? expected[0] : new double[_rewardLength];
                _bonusRewards.Add(bonus, reward);
                return reward;
            }

            // フラグ×持ち越しの1ゲームの出目の分布。適当打ちは全押し方を数え上げ、完全攻略は最良の押し方1つ。
            private List<Outcome> Outcomes(SlotFlag flag, string carried)
            {
                var key = flag.Id + "|" + carried;
                if (_outcomes.TryGetValue(key, out var cached))
                {
                    return cached;
                }

                var weights = new Dictionary<string, (int Weight, List<string> Aligned)>();
                List<string> best = null;
                var bestScore = (Bonus: -1, Payout: -1);
                var presses = new int[SlotReels.ReelCount];
                const int pressCount = SlotReels.StopCount * SlotReels.StopCount * SlotReels.StopCount;
                foreach (var order in PressOrders)
                {
                    for (var n = 0; n < pressCount; n++)
                    {
                        presses[0] = n % SlotReels.StopCount;
                        presses[1] = n / SlotReels.StopCount % SlotReels.StopCount;
                        presses[2] = n / (SlotReels.StopCount * SlotReels.StopCount);
                        var spin = _control.Start(flag, carried);
                        foreach (var reel in order)
                        {
                            spin.Stop(reel, presses[reel]);
                        }

                        var aligned = SlotLines.Aligned(_reels, _payouts, spin.Stops);
                        if (_style == SlotPlayStyle.Perfect)
                        {
                            var score = Score(aligned);
                            if (score.Bonus > bestScore.Bonus || (score.Bonus == bestScore.Bonus && score.Payout > bestScore.Payout))
                            {
                                bestScore = score;
                                best = aligned;
                            }

                            continue;
                        }

                        var id = string.Join("+", aligned);
                        weights[id] = weights.TryGetValue(id, out var entry) ? (entry.Weight + 1, entry.Aligned) : (1, aligned);
                    }
                }

                var outcomes = new List<Outcome>();
                if (_style == SlotPlayStyle.Perfect)
                {
                    outcomes.Add(CreateOutcome(best, 1));
                }
                else
                {
                    foreach (var entry in weights.Values)
                    {
                        outcomes.Add(CreateOutcome(entry.Aligned, (double)entry.Weight / (PressOrders.Length * pressCount)));
                    }
                }

                _outcomes.Add(key, outcomes);
                return outcomes;
            }

            private (int Bonus, int Payout) Score(List<string> aligned)
            {
                var bonus = 0;
                var payout = 0;
                foreach (var role in aligned)
                {
                    bonus += SlotFlag.IsBonusRole(role) ? 1 : 0;
                    payout += _payouts.Get(role).Payout;
                }

                return (bonus, payout);
            }

            private Outcome CreateOutcome(List<string> aligned, double probability)
            {
                var outcome = new Outcome { Probability = probability, Reward = new double[_rewardLength] };
                outcome.Reward[GameIndex] = 1;
                foreach (var role in aligned)
                {
                    outcome.Payout += _payouts.Get(role).Payout;
                    outcome.Reward[RoleIndex + IndexOfRole(role)] = 1;
                    if (role == SlotMachine.ReplayRole)
                    {
                        outcome.Reward[ReplayIndex] = 1;
                    }

                    if (SlotFlag.IsBonusRole(role))
                    {
                        outcome.Bonus = role;
                    }
                }

                outcome.Reward[PayoutIndex] = outcome.Payout;
                return outcome;
            }

            private int IndexOfRole(string role)
            {
                for (var i = 0; i < _payouts.All.Count; i++)
                {
                    if (_payouts.All[i].Role == role)
                    {
                        return i;
                    }
                }

                throw new ArgumentException($"払い出し表にない役: '{role}'", nameof(role));
            }

            private static void Add(double[] target, double[] values, double scale)
            {
                for (var i = 0; i < target.Length; i++)
                {
                    target[i] += values[i] * scale;
                }
            }

            private static bool[] Reachable(double[,] transitions, int start)
            {
                var count = transitions.GetLength(0);
                var reachable = new bool[count];
                var pending = new Stack<int>();
                reachable[start] = true;
                pending.Push(start);
                while (pending.Count > 0)
                {
                    var from = pending.Pop();
                    for (var to = 0; to < count; to++)
                    {
                        if (!reachable[to] && transitions[from, to] > 0)
                        {
                            reachable[to] = true;
                            pending.Push(to);
                        }
                    }
                }

                return reachable;
            }

            // π P = π、Σπ = 1 をガウスの消去法（部分ピボット）で解く。start の釣り合いの式を Σπ = 1 に、
            // 到達しない状態は π = 0 に置き換える。
            private static double[] Stationary(double[,] transitions, bool[] reachable, int start)
            {
                var n = transitions.GetLength(0);
                var a = new double[n, n + 1];
                for (var row = 0; row < n; row++)
                {
                    for (var col = 0; col < n; col++)
                    {
                        a[row, col] = row == start ? 1
                            : !reachable[row] ? (row == col ? 1 : 0)
                            : transitions[col, row] - (row == col ? 1 : 0);
                    }

                    a[row, n] = row == start ? 1 : 0;
                }

                for (var col = 0; col < n; col++)
                {
                    var pivot = col;
                    for (var row = col + 1; row < n; row++)
                    {
                        if (Math.Abs(a[row, col]) > Math.Abs(a[pivot, col]))
                        {
                            pivot = row;
                        }
                    }

                    if (Math.Abs(a[pivot, col]) < 1e-15)
                    {
                        throw new InvalidOperationException("状態遷移の定常分布が一意に決まらない");
                    }

                    for (var j = col; j <= n; j++)
                    {
                        (a[col, j], a[pivot, j]) = (a[pivot, j], a[col, j]);
                    }

                    for (var row = 0; row < n; row++)
                    {
                        if (row == col || a[row, col] == 0)
                        {
                            continue;
                        }

                        var factor = a[row, col] / a[col, col];
                        for (var j = col; j <= n; j++)
                        {
                            a[row, j] -= factor * a[col, j];
                        }
                    }
                }

                var result = new double[n];
                for (var row = 0; row < n; row++)
                {
                    result[row] = a[row, n] / a[row, row];
                }

                return result;
            }
        }
    }
}
