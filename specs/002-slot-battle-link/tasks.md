# タスク: スロットと戦闘の連動強化

対象: [spec.md](spec.md)（2026-10-31 承認）/ [plan.md](plan.md)
書式は 001 の [tasks.md](../001-slot-tower-defense/tasks.md) と同じ。完了時は `[x]` にし、行末に実際の結果を書く。

- [x] T001 (FR-100, FR-131, AC-100) 図柄・役・リール配列・抽選表を6役に置き換え、旧役（リプレイ・チェリーの段の特例を含む）をコードとテストから外す。理論機械割を目標に合わせる — 検証: EditMode（全フラグ×全押し方の安全性、理論機械割、大量シミュレーション） — 結果: 2026-10-31 理論値 99.95/104.06/108.00/111.97/118.09/125.07%、コインとハズレは出目が成立役どおり、バフ役・ボーナスは約58〜60%揃う
- [x] T002 (FR-110, AC-110) バッファーを味方の表・Core・表示・操作バー・テストから外す — 検証: EditMode（CSV 検証、`Buff_Role_Is_Rejected`）、Editor 画像 — 結果: 2026-10-31 操作バーは4アイコン（`s002-buffs.png`）
- [x] T003 (FR-111〜FR-114, AC-111, AC-112) `BattleBuffs`（攻撃力上昇・攻撃速度上昇・HP回復）と `WaveRun.ApplySlotRole` — 検証: EditMode `BattleBuffTests`・`RegSwarmTests` — 結果: 2026-10-31 green
- [x] T004 (FR-101〜FR-103, AC-101) `reg_swarms.csv`・`RegSwarmTable`・`Battlefield.AddSpawns`、準備中の群れをためて戦闘開始で出す — 検証: EditMode `RegSwarmTests`、PlayMode `SlotBattleLinkTests` — 結果: 2026-10-31 green
- [x] T005 (FR-121, AC-114) `SlotNotice`・`SlotMachine.AnnouncesBonus`/`IsBonusPending`/`RolesAligned` — 検証: EditMode `SlotNoticeTests`・`SlotMachineTests` — 結果: 2026-10-31 green
- [x] T006 (FR-115, FR-120〜FR-124, AC-113) `SlotCelebration`・`SlotSounds`・当たり確定ランプ・`BuffAuraView`・`BuffGauges`、`BattleController` で役を戦闘に渡す。`Tools/UnitySlotTower/Setup/Apply Slot Battle Link` で Game シーンに反映 — 検証: PlayMode（演出中の進行、BIG と REG の色、4アイコンとゲージ）、Editor 画像 — 結果: 2026-10-31 PlayMode 25/25。画像 `s002-Big-sc.png`（白の点滅と粒）・`s002-Reg-sc.png`（赤のフラッシュと揺れ）・`s002-aura5.png`（タンクと剣士に赤青の輪。輪を 0.95→1.25 に拡大）
- [x] T007 (plan 6章) 全体の検証 — 結果: 2026-10-31 dotnet 383/383、Unity EditMode 432/432・PlayMode 25/25、Console エラー0、ハーネスは HANDOFF に記録
- [ ] T008 (AC-113) 手動プレイで演出の派手さと見分け、バフの体感、REG の群れの難しさを確かめる — 検証: ユーザーの手動プレイ
