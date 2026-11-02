using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Core.Tutorial
{
    /// <summary>文字なし説明（FR-004、T403）の手順。並びは同時に条件を満たしたときの優先順。</summary>
    public enum TutorialStep
    {
        /// <summary>マスを選ぶ（配置）。配置操作の途中なので最優先。</summary>
        Place,
        /// <summary>向きを選ぶ。</summary>
        Facing,
        /// <summary>操作バーのアイコンで味方を選ぶ（召喚）。</summary>
        Summon,
        /// <summary>▶ で戦闘を始める。</summary>
        Start,
        /// <summary>レバーとストップでスロットを回す。</summary>
        Slot,
        /// <summary>ボーナス中は上限まで払い出しが続く。</summary>
        Bonus,
        /// <summary>種類ごとの強化。</summary>
        Upgrade,
        /// <summary>選んだ味方の撤退。</summary>
        Retreat,
    }

    /// <summary>説明を出すかを決めるための、その時点のゲームの様子。</summary>
    public readonly struct TutorialSituation
    {
        public TutorialSituation(int wave, WavePhase wavePhase, DeployPhase deployPhase, int unitsOnField,
            bool unitSelected, bool slotInBonus)
        {
            Wave = wave;
            WavePhase = wavePhase;
            DeployPhase = deployPhase;
            UnitsOnField = unitsOnField;
            UnitSelected = unitSelected;
            SlotInBonus = slotInBonus;
        }

        public int Wave { get; }
        public WavePhase WavePhase { get; }
        public DeployPhase DeployPhase { get; }
        public int UnitsOnField { get; }

        /// <summary>撤退の対象として配置中の味方を選んでいる。</summary>
        public bool UnitSelected { get; }

        public bool SlotInBonus { get; }
    }

    /// <summary>
    /// 文字なし説明の見た手順（初回だけ表示、FR-004、plan 8章）と、次に出す手順の判定。
    /// 出す場面: 召喚＝W1 の準備開始、配置・向き＝初めてその操作に入ったとき、▶＝準備中に初めて味方を置いたとき、
    /// スロット＝初めての戦闘、ボーナス＝初めてのボーナス、強化＝W2 の準備開始、撤退＝初めて配置中の味方を選んだとき。
    /// 準備中か戦闘中だけ出す（クリア後・ゲームオーバー後は出さない）。
    /// </summary>
    public sealed class TutorialProgress
    {
        private const char Separator = ',';
        private static readonly TutorialStep[] Steps = (TutorialStep[])Enum.GetValues(typeof(TutorialStep));

        private readonly HashSet<TutorialStep> _seen = new HashSet<TutorialStep>();

        public static TutorialProgress AllSeen()
        {
            var progress = new TutorialProgress();
            foreach (var step in Steps)
            {
                progress.MarkSeen(step);
            }

            return progress;
        }

        /// <summary><see cref="Serialize"/> の文字列から戻す。知らない名前や空の要素は無視する。</summary>
        public static TutorialProgress Parse(string text)
        {
            var progress = new TutorialProgress();
            if (string.IsNullOrEmpty(text))
            {
                return progress;
            }

            foreach (var part in text.Split(Separator))
            {
                // 数字（"3" など）も Enum.TryParse は受け付けるので、名前と一致するものだけ使う。
                var name = part.Trim();
                if (Enum.TryParse(name, out TutorialStep step) && step.ToString() == name)
                {
                    progress.MarkSeen(step);
                }
            }

            return progress;
        }

        public bool HasSeen(TutorialStep step) => _seen.Contains(step);

        public void MarkSeen(TutorialStep step) => _seen.Add(step);

        public bool IsComplete => _seen.Count == Steps.Length;

        /// <summary>見た手順の名前をカンマ区切りで返す（並びは手順の順）。</summary>
        public string Serialize()
        {
            var names = new List<string>(_seen.Count);
            foreach (var step in Steps)
            {
                if (_seen.Contains(step))
                {
                    names.Add(step.ToString());
                }
            }

            return string.Join(Separator.ToString(), names);
        }

        /// <summary>まだ見ていない手順のうち、いま条件を満たす最初のもの。なければ null。</summary>
        public TutorialStep? Next(in TutorialSituation situation)
        {
            if (situation.WavePhase != WavePhase.Preparing && situation.WavePhase != WavePhase.Fighting)
            {
                return null;
            }

            foreach (var step in Steps)
            {
                if (!_seen.Contains(step) && Applies(step, situation))
                {
                    return step;
                }
            }

            return null;
        }

        private static bool Applies(TutorialStep step, in TutorialSituation s)
        {
            var idle = s.DeployPhase == DeployPhase.Idle;
            var preparing = s.WavePhase == WavePhase.Preparing;
            switch (step)
            {
                case TutorialStep.Place:
                    return s.DeployPhase == DeployPhase.ChoosingCell;
                case TutorialStep.Facing:
                    return s.DeployPhase == DeployPhase.ChoosingFacing;
                case TutorialStep.Summon:
                    return idle && preparing && s.Wave == 1 && s.UnitsOnField == 0;
                case TutorialStep.Start:
                    return idle && preparing && s.UnitsOnField > 0;
                case TutorialStep.Slot:
                    return idle && s.WavePhase == WavePhase.Fighting;
                case TutorialStep.Bonus:
                    return idle && s.SlotInBonus;
                case TutorialStep.Upgrade:
                    return idle && preparing && s.Wave >= 2;
                case TutorialStep.Retreat:
                    return idle && s.UnitSelected;
                default:
                    return false;
            }
        }
    }
}
