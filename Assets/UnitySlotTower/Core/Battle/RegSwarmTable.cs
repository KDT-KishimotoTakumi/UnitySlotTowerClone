using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>
    /// reg_swarms.csv: REG が揃ったときに出る敵の群れ（spec 002 FR-101、FR-130）。行ごとに、その敵を count 体、
    /// interval 秒おきに出す。出現口はそのウェーブのマップの経路（地上・空中が合うもの）を順番に使う。
    /// </summary>
    public sealed class RegSwarmTable
    {
        public const string FileName = "reg_swarms.csv";

        private readonly List<(int Wave, EnemyDefinition Enemy, int Count, double Interval, CsvRow Row)> _rows =
            new List<(int, EnemyDefinition, int, double, CsvRow)>();

        private RegSwarmTable()
        {
        }

        public static RegSwarmTable Load(CsvTable table, EnemyTable enemies)
        {
            var result = new RegSwarmTable();
            foreach (var row in table.Rows)
            {
                var enemyId = row.Get("enemy_id");
                if (!enemies.Contains(enemyId))
                {
                    throw row.Error("enemy_id", $"敵 '{enemyId}' がない");
                }

                result._rows.Add((row.GetInt("wave"), enemies.Get(enemyId), row.GetInt("count"), row.GetDouble("interval"), row));
            }

            return result;
        }

        /// <summary>
        /// そのウェーブの群れ1回分の出現（時刻は REG が揃って出始めてからの秒数）。マップに合う経路がない敵はファイル名・行・列つきの
        /// <see cref="CsvFormatException"/> にする。
        /// </summary>
        public IReadOnlyList<WaveSpawn> Create(int wave, BattleMap map)
        {
            var spawns = new List<WaveSpawn>();
            var groundTurn = 0;
            var airTurn = 0;
            foreach (var (rowWave, enemy, count, interval, row) in _rows)
            {
                if (rowWave != wave)
                {
                    continue;
                }

                var paths = new List<MapPath>();
                foreach (var path in map.Paths)
                {
                    if (path.IsAir == enemy.IsAir)
                    {
                        paths.Add(path);
                    }
                }

                if (paths.Count == 0)
                {
                    throw row.Error("enemy_id", $"マップ '{map.Id}' に{(enemy.IsAir ? "空中" : "地上")}の経路がない");
                }

                for (var i = 0; i < count; i++)
                {
                    var turn = enemy.IsAir ? airTurn++ : groundTurn++;
                    var spawn = new WaveSpawn(i * interval, enemy, paths[turn % paths.Count], false);

                    // 時刻順に並べ、同時刻は行順を保つ。
                    var at = spawns.Count;
                    while (at > 0 && spawns[at - 1].Time > spawn.Time)
                    {
                        at--;
                    }

                    spawns.Insert(at, spawn);
                }
            }

            return spawns;
        }
    }
}
