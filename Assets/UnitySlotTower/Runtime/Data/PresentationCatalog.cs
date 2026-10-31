using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnitySlotTower
{
    /// <summary>
    /// CSV の id をキーにした「数値でない参照」（プレハブ・アイコン・音）。数値の正本は CSV に置く（plan 6章）。
    /// </summary>
    [CreateAssetMenu(menuName = "UnitySlotTower/Presentation Catalog", fileName = "PresentationCatalog")]
    public sealed class PresentationCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            public GameObject prefab;
            public Sprite icon;
            public AudioClip sound;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<string, Entry> _lookup;

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>Editor ツールとテスト用。</summary>
        public void SetEntries(IEnumerable<Entry> newEntries)
        {
            entries = new List<Entry>(newEntries);
            _lookup = null;
        }

        public bool TryGet(string id, out Entry entry)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, Entry>(entries.Count);
                foreach (var e in entries)
                {
                    if (e != null && !string.IsNullOrEmpty(e.id))
                    {
                        _lookup[e.id] = e;
                    }
                }
            }

            return _lookup.TryGetValue(id, out entry);
        }

        private void OnValidate() => _lookup = null;
    }
}
