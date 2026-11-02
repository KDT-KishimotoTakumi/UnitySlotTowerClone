namespace UnitySlotTower.Core.Ui
{
    /// <summary>Title 以外で表示してよい文字（FR-003、plan 8章）。数字専用フォントの文字集合もこれに合わせる。</summary>
    public static class NumericText
    {
        public const string AllowedCharacters = "0123456789+-−×/%";

        /// <summary>許可されない最初の文字の位置。すべて許可されていれば -1。</summary>
        public static int FindInvalid(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return -1;
            }

            for (var i = 0; i < text.Length; i++)
            {
                if (AllowedCharacters.IndexOf(text[i]) < 0)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
