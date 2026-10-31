using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>配置できるマスの種類（plan 5章: 近距離職は地上、遠距離職は高台）。</summary>
    public enum UnitTile
    {
        Ground,
        High,
    }

    /// <summary>
    /// 戦闘での働き（plan 5章）。近距離はブロック中の敵を優先して攻撃し、遠距離だけが空中の敵も攻撃できる。
    /// 回復は範囲内で HP 割合が最も低い味方を攻撃力ぶん回復し、強化は範囲内の他の味方の攻撃力に倍率をかける。
    /// </summary>
    public enum UnitRole
    {
        Melee,
        Ranged,
        Heal,
        Buff,
    }

    /// <summary>味方1種の基本ステータス（強化前）。</summary>
    public sealed class UnitDefinition
    {
        internal UnitDefinition(string id, int maxHp, int atk, int def, double attackInterval, int block, UnitTile tile,
            int cost, double cooldown, UnitRole role, double buffRate)
        {
            Role = role;
            BuffRate = buffRate;
            Id = id;
            MaxHp = maxHp;
            Atk = atk;
            Def = def;
            AttackInterval = attackInterval;
            Block = block;
            Tile = tile;
            Cost = cost;
            Cooldown = cooldown;
        }

        public string Id { get; }
        public int MaxHp { get; }
        public int Atk { get; }
        public int Def { get; }

        /// <summary>攻撃間隔（秒）。</summary>
        public double AttackInterval { get; }

        public int Block { get; }
        public UnitTile Tile { get; }

        /// <summary>召喚コスト（コイン）。</summary>
        public int Cost { get; }

        /// <summary>召喚後、同じ種類を再び召喚できるまでの待ち時間（秒）。</summary>
        public double Cooldown { get; }

        public UnitRole Role { get; }

        /// <summary>役割が強化のとき、範囲内の味方の攻撃力にかける倍率。他の役割では使わない。</summary>
        public double BuffRate { get; }
    }

    /// <summary>units.csv の味方一覧。</summary>
    public sealed class UnitTable
    {
        public const string FileName = "units.csv";

        private readonly Dictionary<string, UnitDefinition> _units = new Dictionary<string, UnitDefinition>();
        private readonly List<UnitDefinition> _all = new List<UnitDefinition>();
        private readonly string _fileName;

        private UnitTable(string fileName)
        {
            _fileName = fileName;
        }

        /// <summary>CSV の行順。</summary>
        public IReadOnlyList<UnitDefinition> All => _all;

        public bool Contains(string id) => _units.ContainsKey(id);

        public UnitDefinition Get(string id)
        {
            if (_units.TryGetValue(id, out var unit))
            {
                return unit;
            }

            throw new CsvFormatException(new CsvError(_fileName, 0, 0, $"味方 '{id}' がない"));
        }

        public static UnitTable Load(CsvTable table)
        {
            var units = new UnitTable(table.FileName);
            foreach (var row in table.Rows)
            {
                var id = row.Get("id");
                if (units._units.ContainsKey(id))
                {
                    throw row.Error("id", $"味方 '{id}' が重複している");
                }

                var unit = new UnitDefinition(id, row.GetInt("max_hp"), row.GetInt("atk"), row.GetInt("def"),
                    row.GetDouble("attack_interval"), row.GetInt("block"), ParseTile(row), row.GetInt("cost"),
                    row.GetDouble("cooldown"), ParseRole(row), row.GetDouble("buff_rate"));
                units._units.Add(id, unit);
                units._all.Add(unit);
            }

            return units;
        }

        private static UnitRole ParseRole(CsvRow row)
        {
            switch (row.Get("role"))
            {
                case "melee":
                    return UnitRole.Melee;
                case "ranged":
                    return UnitRole.Ranged;
                case "heal":
                    return UnitRole.Heal;
                case "buff":
                    return UnitRole.Buff;
                default:
                    throw row.Error("role", $"役割は melee・ranged・heal・buff のどれか: '{row.Get("role")}'");
            }
        }

        private static UnitTile ParseTile(CsvRow row)
        {
            switch (row.Get("tile"))
            {
                case "ground":
                    return UnitTile.Ground;
                case "high":
                    return UnitTile.High;
                default:
                    throw row.Error("tile", $"配置マスは ground か high: '{row.Get("tile")}'");
            }
        }
    }
}
