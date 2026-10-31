using System;
using System.Collections.Generic;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// 目押しのリール制御（plan 5章）。押した位置 p から p, p-1, …, p-最大滑り のどこかで止める。
    /// 残りのリールをどの順・どの位置で押しても、成立していない役は揃わず、全押し方で揃えられる成立役は必ず揃う。
    /// そのうえで、揃う成立役の数の期待値（残りの押し方を一様とみなす）が最大になる停止位置を選ぶ（先読みあり）。
    /// 期待値はボーナス役（BIG・REG）を先に比べ、同じならほかの成立役で比べる。
    /// </summary>
    public sealed class SlotReelControl
    {
        private readonly SlotReels _reels;
        private readonly SlotPayouts _payouts;
        private readonly int _maxSlide;
        private readonly Dictionary<string, SlotControlPlan> _plans = new Dictionary<string, SlotControlPlan>();

        public SlotReelControl(SlotReels reels, SlotPayouts payouts, int maxSlide)
        {
            _reels = reels;
            _payouts = payouts;
            _maxSlide = maxSlide;
        }

        /// <summary>レバーオン。<paramref name="carriedBonus"/> は持ち越し中のボーナス役（なければ null）。</summary>
        public SlotSpin Start(SlotFlag flag, string carriedBonus = null)
        {
            var key = carriedBonus == null ? flag.Id : flag.Id + SlotFlag.RoleSeparator + carriedBonus;
            if (!_plans.TryGetValue(key, out var plan))
            {
                plan = CreatePlan(flag, carriedBonus);
                _plans.Add(key, plan);
            }

            return new SlotSpin(plan);
        }

        // 必ず揃える役は、全押し方で揃えられる最大の組（リプレイ・ベル）。レア役・ボーナスは取りこぼしありで空になる。
        private SlotControlPlan CreatePlan(SlotFlag flag, string carriedBonus)
        {
            var allowed = new List<string>(flag.Roles);
            if (carriedBonus != null && !allowed.Contains(carriedBonus))
            {
                allowed.Add(carriedBonus);
            }

            var roles = flag.Roles;
            var subsets = new List<int>();
            for (var mask = (1 << roles.Count) - 1; mask >= 0; mask--)
            {
                subsets.Add(mask);
            }

            subsets.Sort((a, b) => BitCount(b).CompareTo(BitCount(a)));
            foreach (var mask in subsets)
            {
                var required = new List<string>();
                for (var i = 0; i < roles.Count; i++)
                {
                    if ((mask & (1 << i)) != 0)
                    {
                        required.Add(roles[i]);
                    }
                }

                var plan = new SlotControlPlan(_reels, _payouts, _maxSlide, allowed, required);
                if (plan.IsSafe)
                {
                    return plan;
                }
            }

            throw new InvalidOperationException($"フラグ '{flag.Id}' で成立していない役を蹴飛ばせない配列になっている");
        }

        private static int BitCount(int value)
        {
            var count = 0;
            for (; value != 0; value &= value - 1)
            {
                count++;
            }

            return count;
        }
    }

    /// <summary>1フラグ分の停止位置の評価。状態はリールごとの停止位置（未停止は -1）。</summary>
    internal sealed class SlotControlPlan
    {
        private const int Base = SlotReels.StopCount + 1;

        private readonly SlotReels _reels;
        private readonly SlotPayouts _payouts;
        private readonly int _maxSlide;
        private readonly List<string> _allowed;
        private readonly List<string> _required;

        // 揃うボーナス役の数と、ほかの成立役の数の期待値。ボーナスを先に比べる（持ち越し中は小役より優先して引き込む）。
        // NaN は未計算、負の無限大は成立していない役か取りこぼしを避けられない状態。
        private readonly double[] _bonusValues = new double[Base * Base * Base];
        private readonly double[] _values = new double[Base * Base * Base];

        public SlotControlPlan(SlotReels reels, SlotPayouts payouts, int maxSlide, List<string> allowed, List<string> required)
        {
            _reels = reels;
            _payouts = payouts;
            _maxSlide = maxSlide;
            _allowed = allowed;
            _required = required;
            for (var i = 0; i < _values.Length; i++)
            {
                _values[i] = double.NaN;
            }

            IsSafe = !double.IsNegativeInfinity(Value(new[] { -1, -1, -1 }).Bonus);
        }

        private static bool IsBetter((double Bonus, double Other) value, (double Bonus, double Other) best) =>
            value.Bonus > best.Bonus || (value.Bonus == best.Bonus && value.Other > best.Other);

        public bool IsSafe { get; }

        /// <summary>未停止の状態 <paramref name="stops"/> で reel を pressPosition で押したときの停止位置。</summary>
        public int Choose(int[] stops, int reel, int pressPosition)
        {
            var best = (Bonus: double.NegativeInfinity, Other: double.NegativeInfinity);
            var bestStop = -1;
            for (var slide = 0; slide <= _maxSlide; slide++)
            {
                var stop = SlotReels.Wrap(pressPosition - slide);
                stops[reel] = stop;
                var value = Value(stops);
                if (IsBetter(value, best))
                {
                    best = value;
                    bestStop = stop;
                }
            }

            stops[reel] = -1;
            if (bestStop < 0)
            {
                throw new InvalidOperationException("成立していない役を蹴飛ばせる停止位置がない");
            }

            return bestStop;
        }

        private (double Bonus, double Other) Value(int[] stops)
        {
            var key = (stops[0] + 1) * Base * Base + (stops[1] + 1) * Base + stops[2] + 1;
            if (double.IsNaN(_values[key]))
            {
                (_bonusValues[key], _values[key]) = Evaluate(stops);
            }

            return (_bonusValues[key], _values[key]);
        }

        private (double Bonus, double Other) Evaluate(int[] stops)
        {
            var bonusSum = 0.0;
            var sum = 0.0;
            var count = 0;
            for (var reel = 0; reel < SlotReels.ReelCount; reel++)
            {
                if (stops[reel] >= 0)
                {
                    continue;
                }

                for (var press = 0; press < SlotReels.StopCount; press++)
                {
                    var best = (Bonus: double.NegativeInfinity, Other: double.NegativeInfinity);
                    for (var slide = 0; slide <= _maxSlide; slide++)
                    {
                        stops[reel] = SlotReels.Wrap(press - slide);
                        var value = Value(stops);
                        if (IsBetter(value, best))
                        {
                            best = value;
                        }
                    }

                    stops[reel] = -1;
                    if (double.IsNegativeInfinity(best.Bonus))
                    {
                        return best;
                    }

                    bonusSum += best.Bonus;
                    sum += best.Other;
                    count++;
                }
            }

            return count == 0 ? Score(stops) : (bonusSum / count, sum / count);
        }

        private (double Bonus, double Other) Score(int[] stops)
        {
            var invalid = (double.NegativeInfinity, double.NegativeInfinity);
            var aligned = SlotLines.Aligned(_reels, _payouts, stops);
            var bonus = 0;
            foreach (var role in aligned)
            {
                if (!_allowed.Contains(role))
                {
                    return invalid;
                }

                if (SlotFlag.IsBonusRole(role))
                {
                    bonus++;
                }
            }

            foreach (var role in _required)
            {
                if (!aligned.Contains(role))
                {
                    return invalid;
                }
            }

            return (bonus, aligned.Count - bonus);
        }
    }

    /// <summary>1ゲーム分のリール停止。</summary>
    public sealed class SlotSpin
    {
        private readonly SlotControlPlan _plan;
        private readonly int[] _stops = { -1, -1, -1 };

        internal SlotSpin(SlotControlPlan plan)
        {
            _plan = plan;
        }

        public IReadOnlyList<int> Stops => _stops;

        public bool IsStopped(int reel) => _stops[reel] >= 0;

        /// <summary>リールを押した位置（中段のコマ）で止め、滑ったあとの停止位置を返す。</summary>
        public int Stop(int reel, int pressPosition)
        {
            if (IsStopped(reel))
            {
                throw new InvalidOperationException($"リール {reel} は停止済み");
            }

            _stops[reel] = _plan.Choose(_stops, reel, SlotReels.Wrap(pressPosition));
            return _stops[reel];
        }
    }
}
