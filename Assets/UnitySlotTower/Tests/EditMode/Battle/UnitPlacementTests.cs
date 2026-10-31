using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("AC-021: 職ごとの配置可能マス")]
    public sealed class UnitPlacementTests
    {
        // (0,0) 出現口 (1,0) 地上 (2,0) ゴール / (0,1) 高台 (1,1) 通行不可 (2,1) 地上
        private static readonly BattleMap Map = BattleMap.Load("m", "S1,G,E\nH,X,G\n",
            CsvParser.Parse(BattleMap.PathsFileName, "map_id,path_id,is_air,step,x,y\nm,m_g1,0,1,0,0\nm,m_g1,0,2,1,0\nm,m_g1,0,3,2,0\n"));

        private static readonly UnitTable Units = UnitTable.Load(CsvParser.Parse(UnitTable.FileName,
            "id,max_hp,atk,def,attack_interval,block,tile,cost,cooldown,role,buff_rate\n" +
            "sword,1200,400,150,1,2,ground,12,12,melee,1\n" +
            "archer,700,350,50,1,0,high,14,15,ranged,1\n"));

        private static GridPoint[] PlaceableCells(string unitId)
        {
            var unit = Units.Get(unitId);
            return Enumerable.Range(0, 3).SelectMany(x => Enumerable.Range(0, 2).Select(y => new GridPoint(x, y)))
                .Where(cell => UnitPlacement.CanPlace(Map, cell, unit)).ToArray();
        }

        [Test]
        public void Ground_Unit_Can_Be_Placed_Only_On_Ground_Tiles()
        {
            Assert.That(PlaceableCells("sword"), Is.EquivalentTo(new[] { new GridPoint(1, 0), new GridPoint(2, 1) }));
        }

        [Test]
        public void High_Unit_Can_Be_Placed_Only_On_High_Ground_Tiles()
        {
            Assert.That(PlaceableCells("archer"), Is.EquivalentTo(new[] { new GridPoint(0, 1) }));
        }

        [Test]
        public void Cell_Outside_The_Map_Is_Not_Placeable()
        {
            Assert.That(UnitPlacement.CanPlace(Map, new GridPoint(3, 0), Units.Get("sword")), Is.False);
        }
    }
}
