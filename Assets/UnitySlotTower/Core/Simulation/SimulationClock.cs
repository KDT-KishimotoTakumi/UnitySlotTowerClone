namespace UnitySlotTower.Core.Simulation
{
    /// <summary>
    /// 実時間を 1/60 秒の固定ステップに変換する。一時停止中は時間をためず、ステップも進めない。
    /// </summary>
    public sealed class SimulationClock
    {
        public const int StepsPerSecond = 60;
        public const double StepSeconds = 1.0 / StepsPerSecond;

        // 1/60 を足し続けたときの丸め誤差でステップを取りこぼさないための許容幅。
        private const double Epsilon = 1e-9;

        private double _accumulator;

        public bool IsPaused { get; set; }
        public long Tick { get; private set; }
        public double ElapsedSeconds => Tick * StepSeconds;

        /// <summary>経過した実時間を加え、今回進めるべきステップ数を返す（<see cref="Tick"/> も進む）。</summary>
        public int Advance(double deltaSeconds)
        {
            if (IsPaused || deltaSeconds <= 0)
            {
                return 0;
            }

            _accumulator += deltaSeconds;
            var steps = 0;
            while (_accumulator + Epsilon >= StepSeconds)
            {
                _accumulator -= StepSeconds;
                steps++;
            }

            if (_accumulator < 0)
            {
                _accumulator = 0;
            }

            Tick += steps;
            return steps;
        }
    }
}
