using System;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// 1ゲームの最短時間（ウェイト、plan 5章）。前のレバーオンから待ち時間が経つまで次のゲームを始めない。
    /// 待ち中に押されたレバーは受け付けておき、時間が来たら始める。
    /// </summary>
    public sealed class SlotWait
    {
        private readonly double _seconds;
        private double? _lastStart;

        public SlotWait(double seconds)
        {
            _seconds = seconds;
        }

        /// <summary>受け付けたが、まだ始めていないレバーがある。</summary>
        public bool IsPending { get; private set; }

        /// <summary>レバーを受け付ける。</summary>
        public void Request() => IsPending = true;

        /// <summary>受け付けたレバーを <paramref name="now"/> に始めてよければ受け付けを消して true。</summary>
        public bool Consume(double now)
        {
            if (!IsPending || Remaining(now) > 0)
            {
                return false;
            }

            IsPending = false;
            return true;
        }

        /// <summary>レバーオンでゲームを始めた時刻を記録する（所持コイン不足で始まらなかったときは呼ばない）。</summary>
        public void Started(double now) => _lastStart = now;

        /// <summary>次のゲームを始められるまでの残り秒数。</summary>
        public double Remaining(double now) =>
            _lastStart.HasValue ? Math.Max(0.0, _seconds - (now - _lastStart.Value)) : 0.0;
    }
}
