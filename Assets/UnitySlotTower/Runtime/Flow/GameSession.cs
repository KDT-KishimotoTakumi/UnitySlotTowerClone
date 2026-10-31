using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// シーンをまたいで持つ1プレイ分の選択（plan 4章）。今は難易度（設定1〜6）だけ。セーブはしない（FR-005）。
    /// </summary>
    public static class GameSession
    {
        public const string TitleScene = "Title";
        public const string GameScene = "Game";
        public const int MinSetting = 1;
        public const int MaxSetting = 6;

        /// <summary>難易度選択で選んだ設定。Game シーンを直接開いたときは null（筐体の既定値を使う）。</summary>
        public static int? SelectedSetting { get; private set; }

        // ドメインの再読み込みを切っているので、Play のたびに戻す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => SelectedSetting = null;

        /// <summary>設定を選んで Game シーンを読み込む。</summary>
        public static void StartGame(int setting)
        {
            if (setting < MinSetting || setting > MaxSetting)
            {
                throw new ArgumentOutOfRangeException(nameof(setting), setting, "設定は1〜6");
            }

            SelectedSetting = setting;
            SceneManager.LoadScene(GameScene);
        }
    }
}
