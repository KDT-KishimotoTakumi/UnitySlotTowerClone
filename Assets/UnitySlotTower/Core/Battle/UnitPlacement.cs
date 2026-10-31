namespace UnitySlotTower.Core.Battle
{
    /// <summary>配置マスの制限（FR-023）。近距離職は地上マス、遠距離職は高台マスにだけ置ける。</summary>
    public static class UnitPlacement
    {
        /// <summary>マスの種類だけを見る（ほかの味方が居るかは配置側で判定する）。</summary>
        public static bool CanPlace(BattleMap map, GridPoint cell, UnitDefinition unit)
        {
            if (!map.Contains(cell))
            {
                return false;
            }

            var tile = map.TileAt(cell);
            return unit.Tile == UnitTile.Ground ? tile == MapTile.Ground : tile == MapTile.HighGround;
        }
    }
}
