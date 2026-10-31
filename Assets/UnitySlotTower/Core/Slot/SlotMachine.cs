using System;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// 1台のスロットのゲーム進行（plan 5章）。レバーで掛け金を投入して内部抽選し、3本止めたら揃った役を払い出す。
    /// リプレイが揃うと次のゲームは掛け金なしで回せる。
    /// </summary>
    public sealed class SlotMachine
    {
        public const string ReplayRole = "replay";

        private readonly SlotReels _reels;
        private readonly SlotPayouts _payouts;
        private readonly SlotProbabilities _probabilities;
        private readonly SlotPremonition _premonition;
        private readonly SlotReelControl _control;
        private readonly SlotRules _rules;
        private readonly CoinWallet _wallet;
        private readonly IRandom _random;
        private readonly int _bet;
        private readonly int _setting;

        public SlotMachine(SlotReels reels, SlotPayouts payouts, SlotProbabilities probabilities, SlotPremonition premonition,
            SlotReelControl control, SlotRules rules, CoinWallet wallet, int bet, int setting, IRandom random)
        {
            _reels = reels;
            _payouts = payouts;
            _probabilities = probabilities;
            _premonition = premonition;
            _control = control;
            _rules = rules;
            _wallet = wallet;
            _bet = bet;
            _setting = setting;
            _random = random;
        }

        /// <summary>レバーオン。回転中か、リプレイでなく所持コインが掛け金未満なら何もせず false（FR-040）。</summary>
        public bool TryLever()
        {
            if (Spin != null || (!IsReplay && !_wallet.TrySpend(_bet)))
            {
                return false;
            }

            IsReplay = false;
            Flag = _probabilities.Draw(_random, State, _setting);
            if (CarriedBonus != null)
            {
                Flag = _probabilities.WithoutBonus(Flag);
            }

            Premonition = _premonition.Draw(_random, Flag);

            Spin = _control.Start(Flag, CarriedBonus);
            return true;
        }

        /// <summary>リールを押した位置で止める。3本目を止めたら揃った役を払い出してゲームを終える。停止位置を返す。</summary>
        public int Stop(int reel, int pressPosition)
        {
            if (Spin == null)
            {
                throw new InvalidOperationException("回転中ではない");
            }

            var stop = Spin.Stop(reel, pressPosition);
            for (var i = 0; i < SlotReels.ReelCount; i++)
            {
                if (!Spin.IsStopped(i))
                {
                    return stop;
                }
            }

            Finish();
            return stop;
        }

        // 払い出しは役ごとに1ゲーム1回（plan 5章）。ボーナス役が揃えばボーナス開始、揃わなかったボーナス役は持ち越す。
        // ボーナス中のゲームの払い出しは累計し、上限に達したゲームで終える（plan 5章）。
        private void Finish()
        {
            var isBonusGame = State == SlotState.Bonus;
            var isKakuhenGame = State == SlotState.Kakuhen;
            var payout = 0;
            var aligned = SlotLines.Aligned(_reels, _payouts, Spin.Stops);
            foreach (var role in aligned)
            {
                payout += _payouts.Get(role).Payout;
                IsReplay |= role == ReplayRole;
                if (SlotFlag.IsBonusRole(role))
                {
                    State = SlotState.Bonus;
                    ActiveBonus = role;
                    CarriedBonus = null;
                }
            }

            foreach (var role in Flag.Roles)
            {
                if (SlotFlag.IsBonusRole(role) && !aligned.Contains(role))
                {
                    CarriedBonus = role;
                }
            }

            // 確変中に始めたゲームを数える。ボーナスを揃えたゲームも1ゲームとし、ボーナス中は数えない（plan 5章）。
            if (isKakuhenGame)
            {
                KakuhenGamesLeft--;
                if (KakuhenGamesLeft == 0 && State == SlotState.Kakuhen)
                {
                    State = SlotState.Normal;
                }
            }

            if (isBonusGame)
            {
                BonusPayout += payout;
                if (BonusPayout >= BonusPayoutLimit(ActiveBonus))
                {
                    // BIG 終了時は突入率で確変を抽選し、当たればゲーム数を数え直す（plan 5章）。
                    if (ActiveBonus == SlotFlag.BigRole && _random.NextDouble() < _rules.KakuhenEntryRate)
                    {
                        KakuhenGamesLeft = _rules.KakuhenGames;
                    }

                    State = KakuhenGamesLeft > 0 ? SlotState.Kakuhen : SlotState.Normal;
                    ActiveBonus = null;
                    BonusPayout = 0;
                }
            }

            _wallet.Add(payout);
            Spin = null;
        }

        private int BonusPayoutLimit(string bonus) => bonus == SlotFlag.BigRole ? _rules.BigPayoutLimit : _rules.RegPayoutLimit;

        /// <summary>消化中のボーナスで払い出した枚数の累計（ボーナス中でなければ 0）。</summary>
        public int BonusPayout { get; private set; }

        /// <summary>確変の残りゲーム数（確変でなければ 0）。</summary>
        public int KakuhenGamesLeft { get; private set; }

        /// <summary>前のゲームでリプレイが揃い、次のレバーで掛け金を取らない。</summary>
        public bool IsReplay { get; private set; }

        /// <summary>抽選に使う状態。ボーナス役を揃えると Bonus になる。</summary>
        public SlotState State { get; private set; } = SlotState.Normal;

        /// <summary>消化中のボーナス役（BIG・REG。ボーナス中でなければ null）。</summary>
        public string ActiveBonus { get; private set; }

        /// <summary>成立したが揃えていないボーナス役（BIG・REG）。揃えるまで次ゲーム以降に持ち越す（なければ null）。</summary>
        public string CarriedBonus { get; private set; }

        /// <summary>今のゲームのレバーオンで先バレが起きた（FR-044）。</summary>
        public bool Premonition { get; private set; }

        /// <summary>今のゲームの成立フラグ（レバーオン前は null）。</summary>
        public SlotFlag Flag { get; private set; }

        /// <summary>回転中のゲーム（止まっていれば null）。</summary>
        public SlotSpin Spin { get; private set; }
    }
}
