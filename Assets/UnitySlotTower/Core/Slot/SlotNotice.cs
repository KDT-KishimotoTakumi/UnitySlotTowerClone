namespace UnitySlotTower.Core.Slot
{
    /// <summary>当たり確定の告知（spec 002 FR-121）。</summary>
    public static class SlotNotice
    {
        /// <summary>
        /// そのゲームの成立フラグにボーナス役があれば、第3停止後に告知する（先バレの有無や BIG・REG によらない）。
        /// 持ち越し中のゲームはボーナス役を成立させない（<see cref="SlotProbabilities.WithoutBonus"/>）ので告知しない。
        /// </summary>
        public static bool ShouldAnnounce(SlotFlag flag)
        {
            if (flag == null)
            {
                return false;
            }

            foreach (var role in flag.Roles)
            {
                if (SlotFlag.IsBonusRole(role))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
