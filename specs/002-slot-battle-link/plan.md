# 計画: スロットと戦闘の連動強化

対象: [spec.md](spec.md)（2026-10-31 承認）。spec の「未決事項」で plan に任された値と見た目をここで決める。値はすべてデータテーブルで変えられる（FR-130）。

## 1. リールと役（FR-100, FR-131）

- 図柄（`SlotSymbol` の順）: 7・BAR・攻撃力・攻撃速度・HP回復・コイン。CSV の ID は `seven, bar, attack, speed, heal, coin`。
- 役（`slot_payouts.csv`）:

| 役 | 図柄 | 払い出し |
| --- | --- | --- |
| attack | 攻撃力×3 | 3 |
| speed | 攻撃速度×3 | 3 |
| heal | HP回復×3 | 3 |
| coin | コイン×3 | 8 |
| big | 7・7・7 | 0（BIG 消化で 240 枚） |
| reg | 7・7・BAR | 0（REG 消化で 96 枚＋敵の群れ） |

- リール配列（`slot_reels.csv`、各リール 21 コマ）: コイン6・攻撃力3・攻撃速度3・HP回復3・7が3・BARが3。コインはどの押し位置からも引き込めるので必ず揃い、ハズレは必ず何も揃わない。バフ役・ボーナスは取りこぼしあり（適当打ちで約 6 割揃う）。
- 抽選（`slot_probabilities.csv`）: 通常・確変ともバフ役は各 1/16、コインは設定1〜6で 1/4.26・1/4.01・1/3.8・1/3.61・1/3.35・1/3.09。BIG・REG の確率は 001 と同じ。重複当選は `big+attack`・`big+speed`（1/3000、確変 1/1500）と `reg+heal`（1/4000、確変 1/2000）。ボーナス中はコイン 1/1.3、バフ役各 1/20。
- 理論機械割（適当打ち）: 99.95 / 104.06 / 108.00 / 111.97 / 118.09 / 125.07%（目標 100/104/108/112/118/125% との差は 0.005 以内）。
- 旧役（リプレイ・ベル・スイカ・チェリー・中段チェリー）と、チェリーの段の特例・リプレイの再遊技はコードから外す。機械割は毎ゲームの掛け金で割る。

## 2. バフ（FR-110〜FR-115）

- 値（`slot_rules.csv`）: `attack_buff_rate` 1.5、`attack_buff_seconds` 20、`speed_buff_interval_rate` 0.6（攻撃間隔×0.6）、`speed_buff_seconds` 20、`heal_buff_rate` 0.3。
- `Core/Battle/BattleBuffs` が残り時間（1/60 秒のステップ）を持ち、`WaveRun` がウェーブをまたいで使う。`WaveRun.Advance` の戦闘中のステップだけ減る（準備中・一時停止中は `Advance` が進まない）。
- 攻撃力上昇は `Battlefield` の攻撃力（強化後の値）に倍率をかける（ヒーラーの回復量も同じ値）。攻撃速度上昇は攻撃のたびに次の間隔へ倍率をかける。配置中・効果中に置いた味方の区別はなく、効果中はフィールドの全員に効く。
- HP回復は揃った瞬間に配置中の全員へ `round(最大HP×0.3)` を足す（最大 HP まで）。
- 表示: フィールドの味方の足元に、攻撃力は赤、攻撃速度は青の脈打つ輪（両方なら2重）。HP回復は味方ごとに緑の光の柱。操作バーの左から5番目（旧バッファーの場所）に、赤い上向き三角と青い二重三角の付いた縦ゲージ2本（残り時間）。

## 3. REG の群れ（FR-101〜FR-103）

- 表 `reg_swarms.csv`（`wave, enemy_id, count, interval`）。行ごとに count 体を interval 秒おきに出す。出現口はそのウェーブのマップで地上・空中が合う経路を順番に使う。

| ウェーブ | 群れ |
| --- | --- |
| 1 | 近距離5（0.8秒おき） |
| 2 | 近距離6、遠距離2 |
| 3 | 近距離6、遠距離2、空中2 |
| 4 | 近距離8、遠距離3、空中2 |
| 5 | 近距離10、遠距離4、空中3 |

- `Battlefield.AddSpawns` が今からの時刻で出現を足し、`IsCleared` はまだ出ていない群れも数える。準備中の REG は `WaveRun.PendingSwarms` にためて ▶ で全部出す。最後のウェーブの後（Completed）とゲームオーバー後は何もしない。

## 4. 演出（FR-120〜FR-124）

- `Runtime/Slot/SlotCelebration`: 画面全体のフラッシュ（クリックを受けない）、筐体の揺れ、光の粒、自作の効果音（`SlotSounds`）。
  - 先バレ: 金の2回フラッシュ＋光の粒＋上昇音（今のチャイムに足す）。
  - 当たり確定（第3停止後）: 虹色のフラッシュ＋揺れ＋色とりどりの粒＋長い上昇音。筐体の「当たり確定ランプ」（旧リプレイランプ、桃色）がボーナスを揃えるまで点滅する。
  - BIG 揃い: 金と白の速い点滅＋大量の粒＋ファンファーレ。
  - REG 揃い: 赤の3回点滅＋強い揺れ＋サイレン（粒なし）。
- 判定は Core: `SlotNotice.ShouldAnnounce(flag)`（成立フラグにボーナス役があるゲームだけ）、`SlotMachine.AnnouncesBonus`・`IsBonusPending`。持ち越し中のゲームはボーナス役が成立しないので告知し直さない。
- `SlotCabinet` は揃った役・当たり確定を、リールが見た目でも止まってからイベントで知らせ（`RolesAligned`・`BonusAnnounced`・`PremonitionStarted`）、`BattleController` が役を `WaveRun.ApplySlotRole` に渡す。

## 5. シーンとデータの移行

- Editor メニュー `Tools/UnitySlotTower/Setup/Apply Slot Battle Link`（一回限り）で Game シーンを直す: Unit Icon 5・Upgrade Icon 5 を削除し説明の対象から外す、リールの図柄の子を差し替える、リプレイランプを当たり確定ランプにする（`FormerlySerializedAs("replayLamp")`）、バフのゲージ・バフの表示・演出を付ける、`reg_swarms.csv` を戦闘に渡す、味方の見た目からバッファーを外す。
- 入力の `SelectUnit5`・`Upgrade5` は残す（押しても味方がいないので何もしない）。

## 6. 検証

- EditMode: 6役の表・全フラグ×全押し方の安全性・理論機械割と大量シミュレーション（既存テストを新役に置き換え）、バフ（`BattleBuffTests`）、群れ（`RegSwarmTests`）、告知（`SlotNoticeTests`、`SlotMachineTests`）、CSV 検証。
- PlayMode: `SlotBattleLinkTests`（群れの出現、演出中も戦闘とスロットが進む、BIG と REG の色、操作バーの4アイコンとゲージ）。
- Editor 画像: リールの新図柄、操作バー、バフの輪、演出（BIG・REG・当たり確定）。
- 手動プレイ（未）: 演出の派手さと見分け、バフの体感、REG の群れの難しさ。
