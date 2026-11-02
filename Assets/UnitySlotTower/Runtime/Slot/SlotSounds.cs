using UnityEngine;

namespace UnitySlotTower.Slot
{
    /// <summary>素材を選ぶまで（T501）の仮の効果音。サイン波を合成して作る。</summary>
    internal static class SlotSounds
    {
        private const int Rate = 44100;

        /// <summary>音を順に鳴らす上昇音（先バレ・当たり確定）。最後の音は長めに伸ばす。</summary>
        public static AudioClip Arpeggio(string name, float[] notes, float noteSeconds)
        {
            var noteSamples = (int)(Rate * noteSeconds);
            var tailSamples = noteSamples * 4;
            var data = new float[noteSamples * (notes.Length - 1) + tailSamples];
            for (var n = 0; n < notes.Length; n++)
            {
                var length = n == notes.Length - 1 ? tailSamples : noteSamples * 2;
                AddTone(data, n * noteSamples, length, notes[n], 0.25f);
            }

            return Create(name, data);
        }

        /// <summary>BIG のファンファーレ。ド・ミ・ソの上昇のあと、和音を伸ばす。</summary>
        public static AudioClip Fanfare()
        {
            var step = (int)(Rate * 0.12f);
            var data = new float[step * 3 + Rate];
            var notes = new[] { 523.25f, 659.25f, 783.99f };
            for (var n = 0; n < notes.Length; n++)
            {
                AddTone(data, n * step, step, notes[n], 0.3f);
            }

            foreach (var note in new[] { 523.25f, 659.25f, 783.99f, 1046.5f })
            {
                AddTone(data, step * 3, Rate, note, 0.15f);
            }

            return Create("Big Fanfare", data);
        }

        /// <summary>REG の警告。低い2音を3回くり返すサイレン。</summary>
        public static AudioClip Siren()
        {
            var step = (int)(Rate * 0.22f);
            var data = new float[step * 6];
            for (var n = 0; n < 6; n++)
            {
                AddTone(data, n * step, step, n % 2 == 0 ? 440f : 330f, 0.3f, square: true);
            }

            return Create("Reg Siren", data);
        }

        private static void AddTone(float[] data, int start, int length, float frequency, float volume, bool square = false)
        {
            for (var i = 0; i < length && start + i < data.Length; i++)
            {
                var t = (float)i / Rate;
                var envelope = Mathf.Min(1f, i / (Rate * 0.005f)) * (1f - (float)i / length);
                var wave = Mathf.Sin(2f * Mathf.PI * frequency * t);
                if (square)
                {
                    wave = Mathf.Sign(wave) * 0.5f;
                }

                data[start + i] += wave * envelope * volume;
            }
        }

        private static AudioClip Create(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
