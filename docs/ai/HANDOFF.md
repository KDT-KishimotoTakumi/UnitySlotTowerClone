# Resume state

## 現在の作業

更新: 2026-10-31 Asia/Tokyo / 状態: spec 002（スロットと戦闘の連動強化）の実装完了。手動プレイ（002 T008）は未実施。001 は M3 の T309、M4 の T402〜T406 が未完了（T403 は途中）

- 対象: [spec 002](../../specs/002-slot-battle-link/spec.md)・[plan](../../specs/002-slot-battle-link/plan.md)・[tasks](../../specs/002-slot-battle-link/tasks.md)（2026-10-31 承認）。001 の [spec](../../specs/001-slot-tower-defense/spec.md)・[plan](../../specs/001-slot-tower-defense/plan.md) の該当項目には 002 で変えた印を付けた。[001 tasks](../../specs/001-slot-tower-defense/tasks.md) の完了行は当時の記録のまま。
- ブランチ: `feature/002-slot-battle-link`（main から。プッシュ済み。この PC に gh CLI がないため PR は未作成。本文案はコミット bbdccc3 の内容に沿う）。コミットとPRは日本語。
- 002 の要点: 6役（attack・speed・heal・coin・big・reg）、バッファー削除で味方4種、`BattleBuffs`（`slot_rules.csv` の倍率と秒数）、`reg_swarms.csv` の群れ、`SlotNotice` の当たり確定告知、`SlotCelebration`（BIG 白金・REG 赤）。シーンへの反映は Editor `Setup/SlotBattleLinkSetup`（実行済み）。入力の SelectUnit5・Upgrade5 は残っている（何もしない）。
- 次の一手: ユーザーの手動プレイ（002 T008）の結果を待つ。その後は 001 の T403（文字なし説明。バフと REG の説明の追加は 002 で範囲外にした別作業）か T402。
- 最終検証（002）: dotnet 383/383、EditMode 432/432、PlayMode 25/25、Console エラー0、ハーネス 8/8。画像 `Artifacts/Automation/Editor/s002-Big-sc.png`・`s002-Reg-sc.png`・`s002-aura5.png` を目視。
- 画面比: 3:4（基準 1440×1920、初期ウィンドウ 960×1280）。プレイヤービルドで戦闘マスがマゼンタ（既存問題で未調査）。
### M1 の成果（詳細は tasks.md 各行と plan 5章）
- Core/Slot: 表（SlotReels・SlotPayouts・SlotRules・SlotProbabilities・SlotPremonition）、SlotLines、SlotReelControl（フラグ＋持ち越しごとに 23^3 状態をメモ化評価）、SlotMachine（持ち越し・ボーナス上限・確変・先バレ）、SlotTheory、SlotWait。Core/Economy/CoinWallet。
- Runtime/Slot: SlotCabinet（CSV を TextAsset で読む。`setting` 既定1）、SlotReelView（`symbolsPerSecond` 16。ユーザー確認で 28 から変更）。Editor: `Setup/SlotCabinetSetup`、`Slot/SlotTheoryReport`。
- 理論機械割（適当打ち、ユーザー決定）: 100.04〜125.09%。ルール値は `slot_rules.csv`（BIG 240・REG 96・確変100G・突入率1・ウェイト2秒・最大4コマ滑り）。
- 最終検証（2026-10-30）: EditMode 207/207、Console エラー0、ハーネス 8/8、ユーザーの手動目押しと先バレの光・音を確認。PlayMode テストはまだ0件。

### Unity 自動化の注意
- Unity 接続: 停止済み（8080 閉）。Editor は開いたまま。
- テスト: refresh → 約20秒待つ → Console → `editor tests --mode EditMode --async` を1回だけ → 同じ job_id で `poll-test` をループ。1スクリプトにまとめ、開始出力から job_id を取りこぼさない（二重送信の前例あり）。EditMode 全体は約75秒。
- PlayMode テスト: 仮想デバイスは `InputSystem.AddDevice` で足し、`editorInputBehaviorInPlayMode`・`backgroundBehavior` を一時的に変えて非フォーカスでも入力を通す（TearDown で戻す）。シーンは `LoadSceneAsync` の完了を待ってから探す（`LoadScene` 直後だと前のテストの古いオブジェクトを拾う）。実機のマウス・キーボードは SetUp で無効化し TearDown で戻す（実機のポインター位置の変化が向きに反映されてしまうため）。
- CLI はテスト失敗・poll 中・`editor refresh` 成功時でも exit 1 を返すことがあるので `try {} catch {}` で包み、中身の JSON で判断する。
- `editor console` が0件しか返さない不調があった。ログ確認はファイル出力も併用する。`--stacktrace` を付けてもスタックが返らないことがある（`Logs/` はこのセッションの権限で読めなかった）。
- 演出のフラッシュは Screen Space Overlay の Canvas なので `camera screenshot` に写らない。Play 中に code execute で `ScreenCapture.CaptureScreenshot` を使う。
- Editor 非フォーカスだと Play のフレームが進まない。自動で動かすときは code execute で `Application.runInBackground = true`（Play 中だけ）。`code execute` では `UnityEngine.Object` と書く。
- Editor を開いたままシーンファイルを外から書き換えると、再読み込みの確認が出たり固まったりすることがある（2026-10-30 に一度フリーズ、原因は未確定）。シーンの変更はなるべく MCP で行う。
- 留意点（M0 由来）: Shift+数字は SelectUnit と Upgrade の両方に反応しうるので Shift 押下中は SelectUnit を無視。Esc は Cancel と Pause を配置操作中かで振り分ける（plan 3章）。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
