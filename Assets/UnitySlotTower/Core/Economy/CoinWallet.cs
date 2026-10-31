using System;

namespace UnitySlotTower.Core.Economy
{
    /// <summary>所持コイン。スロットと召喚で共有する。</summary>
    public sealed class CoinWallet
    {
        public CoinWallet(int coins)
        {
            if (coins < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(coins), coins, "所持コインが負");
            }

            Coins = coins;
        }

        public int Coins { get; private set; }

        /// <summary>足りれば支払って true、足りなければ何もせず false。</summary>
        public bool TrySpend(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "支払いが負");
            }

            if (Coins < amount)
            {
                return false;
            }

            Coins -= amount;
            return true;
        }

        public void Add(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "加算が負");
            }

            Coins += amount;
        }
    }
}
