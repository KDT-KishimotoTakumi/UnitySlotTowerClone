using System;
using System.Collections.Generic;
using System.Globalization;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>抽選表の状態（plan 6章 slot_probabilities.csv の state 列）。</summary>
    public enum SlotState
    {
        Normal,
        Kakuhen,
        Bonus,
    }

    /// <summary>内部抽選で成立するフラグ。重複当選は複数の役を持つ。</summary>
    public sealed class SlotFlag
    {
        public const string HazureId = "hazure";

        /// <summary>CSV の role 列で重複当選の役をつなぐ文字（例 <c>big+center_cherry</c>）。</summary>
        public const char RoleSeparator = '+';

        public const string BigRole = "big";
        public const string RegRole = "reg";

        /// <summary>BIG・REG（揃えるまで持ち越すボーナス役）。</summary>
        public static bool IsBonusRole(string role) => role == BigRole || role == RegRole;

        private readonly string[] _roles;

        internal SlotFlag(string id, string[] roles)
        {
            Id = id;
            _roles = roles;
        }

        public string Id { get; }
        public IReadOnlyList<string> Roles => _roles;

        public bool Has(string role) => Array.IndexOf(_roles, role) >= 0;

        public override string ToString() => Id;
    }

    /// <summary>slot_probabilities.csv の成立確率と、レバーオン時の内部抽選。行ごとに排他のフラグで、残りの確率はハズレ。</summary>
    public sealed class SlotProbabilities
    {
        public const string FileName = "slot_probabilities.csv";
        public const int SettingCount = 6;

        private static readonly int StateCount = Enum.GetValues(typeof(SlotState)).Length;

        private readonly List<SlotFlag> _flags = new List<SlotFlag>();

        // [状態 * SettingCount + 設定-1][フラグ]。ハズレ（先頭）は残りの確率。
        private readonly List<double[]> _probabilities = new List<double[]>();

        private SlotProbabilities()
        {
            Hazure = new SlotFlag(SlotFlag.HazureId, Array.Empty<string>());
            _flags.Add(Hazure);
        }

        /// <summary>ハズレを先頭に、表に出てくる順のフラグ。</summary>
        public IReadOnlyList<SlotFlag> Flags => _flags;

        public SlotFlag Hazure { get; }

        public double Probability(SlotFlag flag, SlotState state, int setting)
        {
            var index = _flags.IndexOf(flag);
            if (index < 0)
            {
                throw new ArgumentException($"この表のフラグではない: '{flag?.Id}'", nameof(flag));
            }

            return Column(state, setting)[index];
        }

        /// <summary>レバーオン時の内部抽選。乱数を1回だけ使う。</summary>
        public SlotFlag Draw(IRandom random, SlotState state, int setting)
        {
            var column = Column(state, setting);
            var value = random.NextDouble();
            for (var i = 1; i < column.Length; i++)
            {
                value -= column[i];
                if (value < 0)
                {
                    return _flags[i];
                }
            }

            return Hazure;
        }

        /// <summary>
        /// <paramref name="flag"/> からボーナス役を除いたフラグ（例 big+watermelon → watermelon）。表にない組ならハズレ。
        /// ボーナス持ち越し中は新たなボーナスを成立させないために使う（plan 5章）。
        /// </summary>
        public SlotFlag WithoutBonus(SlotFlag flag)
        {
            var bonusCount = 0;
            foreach (var role in flag.Roles)
            {
                if (SlotFlag.IsBonusRole(role))
                {
                    bonusCount++;
                }
            }

            if (bonusCount == 0)
            {
                return flag;
            }

            foreach (var candidate in _flags)
            {
                if (candidate.Roles.Count == flag.Roles.Count - bonusCount && IsSmallRoleSubset(candidate, flag))
                {
                    return candidate;
                }
            }

            return Hazure;
        }

        private static bool IsSmallRoleSubset(SlotFlag candidate, SlotFlag flag)
        {
            foreach (var role in candidate.Roles)
            {
                if (SlotFlag.IsBonusRole(role) || !flag.Has(role))
                {
                    return false;
                }
            }

            return true;
        }

        public static SlotProbabilities Load(CsvTable table, SlotPayouts payouts)
        {
            var result = new SlotProbabilities();
            var flagIndex = new Dictionary<string, int>();
            var rows = new List<(CsvRow Row, int Flag, SlotState State)>();
            var seen = new HashSet<(int, SlotState)>();

            foreach (var row in table.Rows)
            {
                var id = row.Get("role");
                if (!flagIndex.TryGetValue(id, out var index))
                {
                    index = result._flags.Count;
                    result._flags.Add(new SlotFlag(id, ParseRoles(row, id, payouts)));
                    flagIndex.Add(id, index);
                }

                var state = ParseState(row);
                if (!seen.Add((index, state)))
                {
                    throw row.Error("role", $"役 '{id}' と状態 '{row.Get("state")}' の組が重複している");
                }

                rows.Add((row, index, state));
            }

            for (var i = 0; i < StateCount * SettingCount; i++)
            {
                result._probabilities.Add(new double[result._flags.Count]);
            }

            foreach (var (row, flag, state) in rows)
            {
                for (var setting = 1; setting <= SettingCount; setting++)
                {
                    var p = row.GetProbability(SettingColumn(setting));
                    if (p < 0 || p > 1)
                    {
                        throw row.Error(SettingColumn(setting), $"確率が 0〜1 の範囲外: {p.ToString(CultureInfo.InvariantCulture)}");
                    }

                    result._probabilities[ColumnIndex(state, setting)][flag] = p;
                }
            }

            for (var i = 0; i < result._probabilities.Count; i++)
            {
                var column = result._probabilities[i];
                var sum = 0.0;
                for (var flag = 1; flag < column.Length; flag++)
                {
                    sum += column[flag];
                }

                // 分母表記の丸め誤差は許す。
                if (sum > 1 + 1e-9)
                {
                    var setting = i % SettingCount + 1;
                    var state = (SlotState)(i / SettingCount);
                    throw new CsvFormatException(new CsvError(table.FileName, 0, table.ColumnIndex(SettingColumn(setting)) + 1,
                        $"{state} の {SettingColumn(setting)} の確率の合計 {sum.ToString("0.####", CultureInfo.InvariantCulture)} が 1 を超えている"));
                }

                column[0] = Math.Max(0, 1 - sum);
            }

            return result;
        }

        private double[] Column(SlotState state, int setting)
        {
            if (setting < 1 || setting > SettingCount)
            {
                throw new ArgumentOutOfRangeException(nameof(setting), setting, $"設定は 1〜{SettingCount}");
            }

            return _probabilities[ColumnIndex(state, setting)];
        }

        private static int ColumnIndex(SlotState state, int setting) => (int)state * SettingCount + setting - 1;

        private static string SettingColumn(int setting) => "setting" + setting.ToString(CultureInfo.InvariantCulture);

        private static string[] ParseRoles(CsvRow row, string id, SlotPayouts payouts)
        {
            var roles = id.Split(SlotFlag.RoleSeparator);
            for (var i = 0; i < roles.Length; i++)
            {
                if (!payouts.Contains(roles[i]))
                {
                    throw row.Error("role", $"役 '{roles[i]}' が {SlotPayouts.FileName} にない");
                }

                if (Array.IndexOf(roles, roles[i]) != i)
                {
                    throw row.Error("role", $"役 '{roles[i]}' が重複している");
                }
            }

            return roles;
        }

        private static SlotState ParseState(CsvRow row)
        {
            switch (row.Get("state"))
            {
                case "normal": return SlotState.Normal;
                case "kakuhen": return SlotState.Kakuhen;
                case "bonus": return SlotState.Bonus;
                default: throw row.Error("state", $"未知の状態 '{row.Get("state")}'");
            }
        }
    }
}
