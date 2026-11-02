using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Battle
{
    public enum MapTile
    {
        Ground,
        HighGround,
        Blocked,
        Spawn,
        Goal,
    }

    /// <summary>マスの位置。x は左から、y は CSV の上の行から数える（0 始まり）。</summary>
    public readonly struct GridPoint : IEquatable<GridPoint>
    {
        public GridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(GridPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPoint other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";
    }

    /// <summary>map_paths.csv の 1 経路。出現口から始まりゴールで終わる、上下左右に連続したマスの並び。</summary>
    public sealed class MapPath
    {
        internal MapPath(string id, bool isAir, IReadOnlyList<GridPoint> cells)
        {
            Id = id;
            IsAir = isAir;
            Cells = cells;
        }

        public string Id { get; }

        /// <summary>空中経路は高台・通行不可マスの上も通れる（plan 1章）。</summary>
        public bool IsAir { get; }

        public IReadOnlyList<GridPoint> Cells { get; }
    }

    /// <summary>
    /// maps/{id}.csv のグリッドと、map_paths.csv のうちそのマップの経路（plan 6・7章、FR-023）。
    /// マス記号は G 地上・H 高台・X 通行不可・S1〜S4 出現口・E ゴール。
    /// </summary>
    public sealed class BattleMap
    {
        public const string PathsFileName = "map_paths.csv";

        private readonly MapTile[,] _tiles;
        private readonly int[,] _spawnNumbers;
        private readonly List<MapPath> _paths = new List<MapPath>();

        private BattleMap(string id, int width, int height)
        {
            Id = id;
            Width = width;
            Height = height;
            _tiles = new MapTile[width, height];
            _spawnNumbers = new int[width, height];
        }

        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<MapPath> Paths => _paths;

        public static string MapFileName(string id) => $"maps/{id}.csv";

        public bool Contains(GridPoint point) =>
            point.X >= 0 && point.X < Width && point.Y >= 0 && point.Y < Height;

        public MapTile TileAt(GridPoint point) => _tiles[point.X, point.Y];

        /// <summary>出現口の番号（S1 なら 1）。出現口でなければ 0。</summary>
        public int SpawnNumberAt(GridPoint point) => _spawnNumbers[point.X, point.Y];

        public MapPath FindPath(string pathId)
        {
            foreach (var path in _paths)
            {
                if (path.Id == pathId)
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>不正な内容はファイル名・行・列つきの <see cref="CsvFormatException"/> にする。</summary>
        public static BattleMap Load(string id, string mapText, CsvTable paths)
        {
            var fileName = MapFileName(id);
            var records = CsvParser.ParseRecords(fileName, mapText);
            if (records.Count == 0)
            {
                throw new CsvFormatException(new CsvError(fileName, 0, 0, "マスがない"));
            }

            var map = new BattleMap(id, records[0].Cells.Count, records.Count);
            for (var y = 0; y < records.Count; y++)
            {
                var record = records[y];
                if (record.Cells.Count != map.Width)
                {
                    throw new CsvFormatException(new CsvError(fileName, record.Line, 0,
                        $"列数 {record.Cells.Count} が 1 行目の {map.Width} と違う"));
                }

                for (var x = 0; x < map.Width; x++)
                {
                    if (!TryParseTile(record.Cells[x], out map._tiles[x, y], out map._spawnNumbers[x, y]))
                    {
                        throw new CsvFormatException(new CsvError(fileName, record.Line, x + 1,
                            $"未知のマス '{record.Cells[x]}'"));
                    }
                }
            }

            map.LoadPaths(paths);
            if (map._paths.Count == 0)
            {
                throw new CsvFormatException(new CsvError(paths.FileName, 0, 0, $"マップ '{id}' の経路がない"));
            }

            return map;
        }

        private static bool TryParseTile(string text, out MapTile tile, out int spawnNumber)
        {
            spawnNumber = 0;
            switch (text)
            {
                case "G":
                    tile = MapTile.Ground;
                    return true;
                case "H":
                    tile = MapTile.HighGround;
                    return true;
                case "X":
                    tile = MapTile.Blocked;
                    return true;
                case "E":
                    tile = MapTile.Goal;
                    return true;
                case "S1":
                case "S2":
                case "S3":
                case "S4":
                    tile = MapTile.Spawn;
                    spawnNumber = text[1] - '0';
                    return true;
                default:
                    tile = MapTile.Blocked;
                    return false;
            }
        }

        private void LoadPaths(CsvTable table)
        {
            var groups = new Dictionary<string, List<(int Step, GridPoint Point, CsvRow Row)>>();
            var order = new List<string>();
            foreach (var row in table.Rows)
            {
                if (row.Get("map_id") != Id)
                {
                    continue;
                }

                var pathId = row.Get("path_id");
                if (!groups.TryGetValue(pathId, out var cells))
                {
                    cells = new List<(int, GridPoint, CsvRow)>();
                    groups.Add(pathId, cells);
                    order.Add(pathId);
                }

                var point = new GridPoint(row.GetInt("x"), row.GetInt("y"));
                if (!Contains(point))
                {
                    throw row.Error("x", $"{point} がマップ {Width}×{Height} の外");
                }

                cells.Add((row.GetInt("step"), point, row));
            }

            foreach (var pathId in order)
            {
                var cells = groups[pathId];
                cells.Sort((a, b) => a.Step.CompareTo(b.Step));
                var isAir = cells[0].Row.GetBool("is_air");
                var points = new GridPoint[cells.Count];
                for (var i = 0; i < cells.Count; i++)
                {
                    var (_, point, row) = cells[i];
                    if (row.GetBool("is_air") != isAir)
                    {
                        throw row.Error("is_air", $"経路 '{pathId}' の中で is_air が混在している");
                    }

                    if (i > 0)
                    {
                        var previous = points[i - 1];
                        if (Math.Abs(point.X - previous.X) + Math.Abs(point.Y - previous.Y) != 1)
                        {
                            throw row.Error("x", $"{previous} から {point} へ上下左右につながっていない");
                        }
                    }

                    var tile = TileAt(point);
                    if (i == 0 && tile != MapTile.Spawn)
                    {
                        throw row.Error("x", $"経路 '{pathId}' の最初 {point} が出現口ではない");
                    }

                    if (i == cells.Count - 1 && tile != MapTile.Goal)
                    {
                        throw row.Error("x", $"経路 '{pathId}' の最後 {point} がゴールではない");
                    }

                    if (!isAir && (tile == MapTile.HighGround || tile == MapTile.Blocked))
                    {
                        throw row.Error("x", $"地上経路 '{pathId}' が通れないマス {point} を通る");
                    }

                    points[i] = point;
                }

                _paths.Add(new MapPath(pathId, isAir, points));
            }
        }
    }
}
