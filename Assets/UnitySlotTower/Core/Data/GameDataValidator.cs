using System.Collections.Generic;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Core.Data
{
    /// <summary>
    /// 全データテーブル（plan 6章）の型・範囲・ID参照・確率合計・経路の連続性を検証する。
    /// files のキーは Data/Tables からの相対パス（区切りは '/'）。
    /// </summary>
    public static class GameDataValidator
    {
        public const int MapCount = 5;

        private static readonly string[] Settings =
            { "setting1", "setting2", "setting3", "setting4", "setting5", "setting6" };

        private static readonly string[] Symbols = { "seven", "bar", "bell", "watermelon", "cherry", "replay" };

        private static readonly ColumnRule[] GameConfigKeys =
        {
            ColumnRule.Int("initial_coins", 0, 100000),
            ColumnRule.Int("bet", 1, 100),
            ColumnRule.Int("life", 1, 1000),
            ColumnRule.Int("life_bonus_per_life", 0, 1000),
            ColumnRule.Int("deploy_limit", 1, 100),
            ColumnRule.Float("retreat_refund_rate", 0, 1),
        };

        private static readonly ColumnRule[] SlotRuleKeys =
        {
            ColumnRule.Int("big_payout_limit", 1, 10000),
            ColumnRule.Int("reg_payout_limit", 1, 10000),
            ColumnRule.Int("kakuhen_games", 0, 10000),
            ColumnRule.Float("kakuhen_entry_rate", 0, 1),
            ColumnRule.Float("wait_seconds", 0, 60),
            ColumnRule.Int("max_slide", 0, 20),
        };

        private static readonly TableSchema[] Schemas =
        {
            new TableSchema("units.csv", new[] { "id" },
                ColumnRule.Id("id"),
                ColumnRule.Int("max_hp", 1, 1000000),
                ColumnRule.Int("atk", 0, 1000000),
                ColumnRule.Int("def", 0, 1000000),
                ColumnRule.Float("attack_interval", 0.01, 60),
                ColumnRule.Int("block", 0, 10),
                ColumnRule.Enum("tile", "ground", "high"),
                ColumnRule.Int("cost", 0, 10000),
                ColumnRule.Float("cooldown", 0, 600),
                ColumnRule.Enum("role", "melee", "ranged", "heal", "buff"),
                ColumnRule.Float("buff_rate", 1, 10)),
            new TableSchema("unit_ranges.csv", new[] { "unit_id", "dx", "dy" },
                ColumnRule.Id("unit_id"),
                ColumnRule.Int("dx", -9, 9),
                ColumnRule.Int("dy", -9, 9)),
            new TableSchema("unit_upgrades.csv", new[] { "unit_id", "level" },
                ColumnRule.Id("unit_id"),
                ColumnRule.Int("level", 1, 5),
                ColumnRule.Float("hp_mult", 0, 100),
                ColumnRule.Float("atk_mult", 0, 100),
                ColumnRule.Float("def_mult", 0, 100),
                ColumnRule.Int("cost", 0, 100000)),
            new TableSchema("enemies.csv", new[] { "id" },
                ColumnRule.Id("id"),
                ColumnRule.Int("max_hp", 1, 10000000),
                ColumnRule.Int("atk", 0, 1000000),
                ColumnRule.Int("def", 0, 1000000),
                ColumnRule.Float("attack_interval", 0.01, 60),
                ColumnRule.Float("move_speed", 0, 100),
                ColumnRule.Float("range", 0, 20),
                ColumnRule.Bool("is_air"),
                ColumnRule.Int("block_cost", 1, 10),
                ColumnRule.Int("life_damage", 0, 1000),
                ColumnRule.Probability("drop_rate"),
                ColumnRule.Int("drop_min", 0, 10000),
                ColumnRule.Int("drop_max", 0, 10000),
                ColumnRule.Int("area_atk", 0, 1000000),
                ColumnRule.Float("area_radius", 0, 20),
                ColumnRule.Float("area_interval", 0, 60)),
            new TableSchema("map_paths.csv", new[] { "map_id", "path_id", "step" },
                ColumnRule.Id("map_id"),
                ColumnRule.Id("path_id"),
                ColumnRule.Bool("is_air"),
                ColumnRule.Int("step", 1, 1000),
                ColumnRule.Int("x", 0, 99),
                ColumnRule.Int("y", 0, 99)),
            new TableSchema("waves.csv", null,
                ColumnRule.Int("wave", 1, MapCount),
                ColumnRule.Float("time", 0, 3600),
                ColumnRule.Id("enemy_id"),
                ColumnRule.Id("spawn"),
                ColumnRule.Id("path_id"),
                ColumnRule.Bool("is_boss")),
            new TableSchema("slot_reels.csv", new[] { "index" },
                ColumnRule.Int("index", 0, 20),
                ColumnRule.Enum("reel1", Symbols),
                ColumnRule.Enum("reel2", Symbols),
                ColumnRule.Enum("reel3", Symbols)),
            new TableSchema("slot_payouts.csv", new[] { "role" },
                ColumnRule.Id("role"),
                ColumnRule.Int("payout", 0, 1000),
                ColumnRule.Enum("symbol1", WithAny(Symbols)),
                ColumnRule.Enum("symbol2", WithAny(Symbols)),
                ColumnRule.Enum("symbol3", WithAny(Symbols))),
            new TableSchema("slot_probabilities.csv", new[] { "role", "state" },
                ColumnRule.Id("role"),
                ColumnRule.Enum("state", "normal", "kakuhen", "bonus"),
                ColumnRule.Probability("setting1"),
                ColumnRule.Probability("setting2"),
                ColumnRule.Probability("setting3"),
                ColumnRule.Probability("setting4"),
                ColumnRule.Probability("setting5"),
                ColumnRule.Probability("setting6")),
            new TableSchema("slot_premonition.csv", new[] { "role" },
                ColumnRule.Id("role"),
                ColumnRule.Probability("rate")),
        };

        private static readonly HashSet<string> MapTiles = new HashSet<string>
            { "G", "H", "X", "E", "S1", "S2", "S3", "S4" };

        public static ValidationReport ValidateAll(IReadOnlyDictionary<string, string> files)
        {
            var report = new ValidationReport();
            var tables = new Dictionary<string, CsvTable>();

            ValidateKeyValueFile(files, GameConfig.FileName, GameConfigKeys, report);
            ValidateKeyValueFile(files, "slot_rules.csv", SlotRuleKeys, report);

            foreach (var schema in Schemas)
            {
                var table = Parse(files, schema.FileName, report);
                if (table != null)
                {
                    tables.Add(schema.FileName, table);
                    TableValidator.ValidateColumns(table, schema, report);
                }
            }

            var maps = new List<string>();
            for (var i = 1; i <= MapCount; i++)
            {
                var name = $"w{i}";
                if (ValidateMap(files, $"maps/{name}.csv", report))
                {
                    maps.Add(name);
                }
            }

            if (tables.Count != Schemas.Length)
            {
                return report;
            }

            TableValidator.ValidateReference(tables["unit_ranges.csv"], "unit_id", tables["units.csv"], "id", report);
            TableValidator.ValidateReference(tables["unit_upgrades.csv"], "unit_id", tables["units.csv"], "id", report);
            TableValidator.ValidateReference(tables["waves.csv"], "enemy_id", tables["enemies.csv"], "id", report);
            TableValidator.ValidateReference(tables["waves.csv"], "path_id", tables["map_paths.csv"], "path_id", report);
            // 重複当選は役を '+' でつなぐ（例 big+center_cherry）。
            TableValidator.ValidateListReference(tables["slot_probabilities.csv"], "role", '+',
                tables["slot_payouts.csv"], "role", report);
            // ハズレは払い出し表にないが、先バレの「ガセ」として発生率を持てる（plan 5章）。
            TableValidator.ValidateReference(tables["slot_premonition.csv"], "role", tables["slot_payouts.csv"], "role",
                report, "hazure");
            ValidateMapIds(tables["map_paths.csv"], maps, report);

            TableValidator.ValidateProbabilitySum(tables["slot_probabilities.csv"], "state", Settings, report);
            TableValidator.ValidatePathContinuity(tables["map_paths.csv"], report);
            var battleMaps = ValidateMapPaths(files, tables["map_paths.csv"], maps, report);
            ValidateWaves(tables["waves.csv"], tables["enemies.csv"], battleMaps, report);
            return report;
        }

        private static void ValidateKeyValueFile(IReadOnlyDictionary<string, string> files, string fileName,
            IReadOnlyList<ColumnRule> keys, ValidationReport report)
        {
            var table = Parse(files, fileName, report);
            if (table != null)
            {
                TableValidator.ValidateKeyValues(table, keys, report);
            }
        }

        private static CsvTable Parse(IReadOnlyDictionary<string, string> files, string fileName, ValidationReport report)
        {
            if (!files.TryGetValue(fileName, out var text))
            {
                report.Add(new CsvError(fileName, 0, 0, "ファイルがない"));
                return null;
            }

            try
            {
                return CsvParser.Parse(fileName, text);
            }
            catch (CsvFormatException ex)
            {
                report.Add(ex.Error);
                return null;
            }
        }

        /// <summary>マップは見出しなしのグリッド。長方形で、既知のマス記号だけを使う。</summary>
        private static bool ValidateMap(IReadOnlyDictionary<string, string> files, string fileName, ValidationReport report)
        {
            if (!files.TryGetValue(fileName, out var text))
            {
                report.Add(new CsvError(fileName, 0, 0, "ファイルがない"));
                return false;
            }

            List<CsvRecord> records;
            try
            {
                records = CsvParser.ParseRecords(fileName, text);
            }
            catch (CsvFormatException ex)
            {
                report.Add(ex.Error);
                return false;
            }

            if (records.Count == 0)
            {
                report.Add(new CsvError(fileName, 0, 0, "マスがない"));
                return false;
            }

            var width = records[0].Cells.Count;
            foreach (var record in records)
            {
                if (record.Cells.Count != width)
                {
                    report.Add(new CsvError(fileName, record.Line, 0, $"列数 {record.Cells.Count} が 1 行目の {width} と違う"));
                }

                for (var column = 0; column < record.Cells.Count; column++)
                {
                    if (!MapTiles.Contains(record.Cells[column]))
                    {
                        report.Add(new CsvError(fileName, record.Line, column + 1, $"未知のマス '{record.Cells[column]}'"));
                    }
                }
            }

            return true;
        }

        private static void ValidateMapIds(CsvTable paths, List<string> maps, ValidationReport report)
        {
            foreach (var row in paths.Rows)
            {
                if (!maps.Contains(row.Get("map_id")))
                {
                    report.Add(row.Error("map_id", $"マップ 'maps/{row.Get("map_id")}.csv' がない").Error);
                }
            }
        }

        /// <summary>
        /// 各マップの経路の有無・範囲・通行可否・出現口からゴールまでを <see cref="BattleMap"/> で確かめる。
        /// 全ウェーブがマップを使うので、経路のないマップもエラーにする。
        /// </summary>
        private static Dictionary<string, BattleMap> ValidateMapPaths(IReadOnlyDictionary<string, string> files,
            CsvTable paths, List<string> maps, ValidationReport report)
        {
            var loaded = new Dictionary<string, BattleMap>();
            foreach (var map in maps)
            {
                try
                {
                    loaded.Add(map, BattleMap.Load(map, files[BattleMap.MapFileName(map)], paths));
                }
                catch (CsvFormatException ex)
                {
                    report.Add(ex.Error);
                }
            }

            return loaded;
        }

        /// <summary>ウェーブ n の行が、マップ w{n} の経路・出現口・空中フラグと合うかを <see cref="WaveSchedule"/> で確かめる。</summary>
        private static void ValidateWaves(CsvTable waves, CsvTable enemyTable, Dictionary<string, BattleMap> maps,
            ValidationReport report)
        {
            EnemyTable enemies;
            try
            {
                enemies = EnemyTable.Load(enemyTable);
            }
            catch (CsvFormatException ex)
            {
                report.Add(ex.Error);
                return;
            }

            for (var wave = 1; wave <= MapCount; wave++)
            {
                if (!maps.TryGetValue($"w{wave}", out var map))
                {
                    continue;
                }

                try
                {
                    if (WaveSchedule.Load(waves, wave, map, enemies).Spawns.Count == 0)
                    {
                        report.Add(new CsvError(waves.FileName, 0, 0, $"ウェーブ {wave} の敵がない"));
                    }
                }
                catch (CsvFormatException ex)
                {
                    report.Add(ex.Error);
                }
            }
        }

        private static string[] WithAny(string[] symbols)
        {
            var values = new string[symbols.Length + 1];
            symbols.CopyTo(values, 0);
            values[symbols.Length] = "any";
            return values;
        }
    }
}
