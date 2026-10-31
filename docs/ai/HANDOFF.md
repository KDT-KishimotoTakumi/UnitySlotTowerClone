# Resume state

## 現在の作業

更新: 2026-10-31 Asia/Tokyo / 状態: M0・M1・M2 完了、M3 の T301〜T308 完了（T309 は後回し）、M4 の T401 完了（Title と難易度選択。結果は tasks.md の各行）。次は T402

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（2026-10-26 承認。変えたら再承認）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md)、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md)（完了タスクは `[x]` と行末に結果・API）。
- 承認: 2026-10-29 にユーザーが「M0から実装を始めて」と依頼。M3 以降も実装依頼の範囲内と解釈してよいが、spec 変更は別途承認が要る（T108 で先バレ対象を BIG・REG に変えたときは承認済み）。T208 は 2026-10-30 にユーザーが手動確認済み。
- ブランチ: `feature/001-m0-foundation`（main に直接コミットしない）。
- 画面比: 3:4（基準 1440×1920、初期ウィンドウ 960×1280。plan 2章）。プレイヤービルドで戦闘マスがマゼンタ（シェーダー欠落、Editor 外の既存問題で未調査）。
- 次の一手: T402（リザルトのオーバーレイ、リトライ＝同じ設定で Game を再読み込み、タイトルへ戻る。PlayMode テストで初期化を確認: AC-004）。設定は `Flow/GameSession.SelectedSetting` に残っているので、リトライは `SceneManager.LoadScene(GameSession.GameScene)` で同じ設定になる。T401 で決めたこと: 難易度画面の初期選択は常に設定1、前回の設定は覚えない。T309（5ウェーブ通しの手動プレイとCSV調整）は 2026-10-31 にユーザーが「バランスはまた後で調整する」として後回し（未完了のまま残す）。T309 で確認したい点: ウェーブの長さ（各約1分で plan の目安より短い）、ボスの見た目と範囲攻撃の演出（今はなし）、W3〜W5 の見た目、タンクの色（青灰）が高台マスと見分けにくい件、操作バーの強化欄（数字と段階の点が小さい）。
- T308 で決めたこと（plan 3章に ▶ のキーがなかったため）: ▶ は「配置操作中でないときの Enter」と操作バーの ▶。撤退は配置中の味方をクリック／Tab で選び X か撤退アイコン。準備中は `BattleController.autoStart`=false で ▶ を待つ（PlayMode テストは `StartWave()` を呼んでから戦闘を確かめる）。
- M2 の要点: Core/Battle に地図・味方/敵の表・召喚・配置可否と範囲・ウェーブ・ブロック・攻撃/回復/強化・ライフとドロップ・配置操作（`UnitDeployment`）。Runtime/Battle に `BattleFieldView`・`BattleController`（W1〜W5、`StartWave`/`TogglePause`）・`UnitPlacementInput`（選択・配置・強化・撤退・▶・一時停止の入力、`SelectedUnit`）/`UnitPlacementView`・`BattleActorsView`・操作バー（`UnitBarIcon`・`UnitUpgradeIcon`・`OperationBarStatus`）。シーンへの追加は Editor `Setup/*Setup`（実行済み）。
- 最終検証（T401）: EditMode 388/388、PlayMode 14/14、Console エラー0、ハーネス 8/8、Editor 画像 `Artifacts/Automation/Editor/t401-title.png`・`t401-difficulty.png` を目視。手動プレイは未実施（T309）。
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
- Editor 非フォーカスだと Play のフレームが進まない。自動で動かすときは code execute で `Application.runInBackground = true`（Play 中だけ）。`code execute` では `UnityEngine.Object` と書く。
- Editor を開いたままシーンファイルを外から書き換えると、再読み込みの確認が出たり固まったりすることがある（2026-10-30 に一度フリーズ、原因は未確定）。シーンの変更はなるべく MCP で行う。
- 留意点（M0 由来）: Shift+数字は SelectUnit と Upgrade の両方に反応しうるので Shift 押下中は SelectUnit を無視。Esc は Cancel と Pause を配置操作中かで振り分ける（plan 3章）。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
