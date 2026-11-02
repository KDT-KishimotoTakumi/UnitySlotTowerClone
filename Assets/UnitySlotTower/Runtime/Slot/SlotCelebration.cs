using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Slot
{
    /// <summary>当たりの演出の種類（spec 002 E-04）。</summary>
    public enum SlotCelebrationKind
    {
        /// <summary>先バレ（FR-120）。</summary>
        Premonition,

        /// <summary>当たり確定の告知（第3停止後、FR-121）。</summary>
        BonusConfirmed,

        /// <summary>BIG が揃った（大当たり、FR-122）。</summary>
        Big,

        /// <summary>REG が揃った（敵が来る警告、FR-122）。</summary>
        Reg,
    }

    /// <summary>
    /// 当たりの派手な演出（spec 002 FR-120〜FR-122、FR-124）。画面のフラッシュ・筐体の揺れ・光の粒・自作の効果音を出す。
    /// 先バレは金、当たり確定は虹色、BIG は金の連続フラッシュとファンファーレ、REG は赤の点滅とサイレンで見分けられるようにする。
    /// どの部品も入力を受けないので、演出中も戦闘とスロットの操作は止まらない。素材を選ぶまで（T501）の仮の見た目と音。
    /// </summary>
    public sealed class SlotCelebration : MonoBehaviour
    {
        [SerializeField] private SlotCabinet cabinet;
        [Tooltip("揺らす筐体（Slot Area）。")]
        [SerializeField] private RectTransform shakeTarget;
        [Tooltip("フラッシュと光の粒を重ねる親（画面全体の Canvas）。")]
        [SerializeField] private RectTransform overlayParent;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private Sprite sparkSprite;

        [Header("Look")]
        [SerializeField] private Color premonitionColor = new Color(1f, 0.75f, 0.1f);
        [SerializeField] private Color bigColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color regColor = new Color(1f, 0.1f, 0.1f);
        [SerializeField] private float flashAlpha = 0.55f;
        [SerializeField] private float shakePixels = 18f;
        [SerializeField] private int sparkCount = 40;
        [SerializeField] private float sparkSize = 26f;

        private struct Spark
        {
            public Image Image;
            public Vector2 Velocity;
            public float Life;
            public float Age;
        }

        private Image _flash;
        private RectTransform _sparkRoot;
        private Spark[] _sparks;
        private int _nextSpark;
        private Vector2 _shakeOrigin;
        private SlotCelebrationKind _kind;
        private float _started = float.NegativeInfinity;
        private float _duration;
        private float _shakeUntil = float.NegativeInfinity;
        private readonly Dictionary<SlotCelebrationKind, AudioClip> _sounds = new Dictionary<SlotCelebrationKind, AudioClip>();

        /// <summary>演出の最中か。</summary>
        public bool IsPlaying => Time.time < _started + _duration;

        /// <summary>最後に始めた演出の種類。</summary>
        public SlotCelebrationKind LastKind => _kind;

        /// <summary>演出のたびに呼ぶ（確認・テスト用）。</summary>
        public event System.Action<SlotCelebrationKind> Played;

        private void Awake()
        {
            _flash = CreateImage("Celebration Flash", overlayParent, null);
            var flashRect = _flash.rectTransform;
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = flashRect.offsetMax = Vector2.zero;
            _flash.enabled = false;

            _sparkRoot = new GameObject("Celebration Sparks", typeof(RectTransform)).GetComponent<RectTransform>();
            _sparkRoot.SetParent(overlayParent, false);
            _sparks = new Spark[sparkCount];
            for (var i = 0; i < _sparks.Length; i++)
            {
                _sparks[i].Image = CreateImage("Spark", _sparkRoot, sparkSprite);
                _sparks[i].Image.rectTransform.sizeDelta = new Vector2(sparkSize, sparkSize);
                _sparks[i].Image.enabled = false;
            }

            _shakeOrigin = shakeTarget.anchoredPosition;
            _sounds[SlotCelebrationKind.Premonition] = SlotSounds.Arpeggio("Premonition Sparkle", new[] { 1046.5f, 1318.5f, 1568f, 2093f }, 0.07f);
            _sounds[SlotCelebrationKind.BonusConfirmed] = SlotSounds.Arpeggio("Bonus Confirmed", new[] { 784f, 1046.5f, 1318.5f, 1568f, 2093f, 2637f }, 0.09f);
            _sounds[SlotCelebrationKind.Big] = SlotSounds.Fanfare();
            _sounds[SlotCelebrationKind.Reg] = SlotSounds.Siren();
        }

        private void OnEnable()
        {
            cabinet.PremonitionStarted += OnPremonition;
            cabinet.BonusAnnounced += OnBonusAnnounced;
            cabinet.RolesAligned += OnRolesAligned;
        }

        private void OnDisable()
        {
            cabinet.PremonitionStarted -= OnPremonition;
            cabinet.BonusAnnounced -= OnBonusAnnounced;
            cabinet.RolesAligned -= OnRolesAligned;
            shakeTarget.anchoredPosition = _shakeOrigin;
        }

        private void OnPremonition() => Play(SlotCelebrationKind.Premonition);
        private void OnBonusAnnounced() => Play(SlotCelebrationKind.BonusConfirmed);

        private void OnRolesAligned(IReadOnlyList<string> roles)
        {
            foreach (var role in roles)
            {
                if (role == SlotFlag.BigRole)
                {
                    Play(SlotCelebrationKind.Big);
                }
                else if (role == SlotFlag.RegRole)
                {
                    Play(SlotCelebrationKind.Reg);
                }
            }
        }

        /// <summary>演出を始める。前の演出が残っていれば置き換える。</summary>
        public void Play(SlotCelebrationKind kind)
        {
            _kind = kind;
            _started = Time.time;
            switch (kind)
            {
                case SlotCelebrationKind.Premonition:
                    _duration = 0.7f;
                    Burst(14, premonitionColor, 500f);
                    break;
                case SlotCelebrationKind.BonusConfirmed:
                    _duration = 1.2f;
                    _shakeUntil = Time.time + 0.4f;
                    Burst(24, Color.white, 700f);
                    break;
                case SlotCelebrationKind.Big:
                    _duration = 2f;
                    _shakeUntil = Time.time + 0.6f;
                    Burst(sparkCount, bigColor, 900f);
                    break;
                case SlotCelebrationKind.Reg:
                    _duration = 1.8f;
                    _shakeUntil = Time.time + 1f;
                    break;
            }

            audioSource.PlayOneShot(_sounds[kind]);
            Played?.Invoke(kind);
        }

        private void Update()
        {
            var t = Time.time - _started;
            var playing = t < _duration;
            _flash.enabled = playing;
            if (playing)
            {
                _flash.color = FlashColor(t);
            }

            if (Time.time < _shakeUntil)
            {
                var strength = _kind == SlotCelebrationKind.Reg ? shakePixels * 1.4f : shakePixels;
                shakeTarget.anchoredPosition = _shakeOrigin + Random.insideUnitCircle * strength;
            }
            else if (shakeTarget.anchoredPosition != _shakeOrigin)
            {
                shakeTarget.anchoredPosition = _shakeOrigin;
            }

            UpdateSparks(Time.deltaTime);
        }

        // 種類ごとのフラッシュ。先バレは金の2回、当たり確定は虹色、BIG は金の速い点滅、REG は赤の3回点滅。
        private Color FlashColor(float t)
        {
            var fade = 1f - t / _duration;
            switch (_kind)
            {
                case SlotCelebrationKind.Premonition:
                    return WithAlpha(premonitionColor, flashAlpha * fade * Pulse(t, 2.8f));
                case SlotCelebrationKind.BonusConfirmed:
                    return WithAlpha(Color.HSVToRGB(t * 2.5f % 1f, 0.7f, 1f), flashAlpha * fade);
                case SlotCelebrationKind.Big:
                    return WithAlpha(Color.Lerp(bigColor, Color.white, Pulse(t, 6f)), flashAlpha * fade);
                default:
                    return WithAlpha(regColor, flashAlpha * Pulse(t, 1.7f));
            }
        }

        private static float Pulse(float t, float perSecond) => 0.5f + 0.5f * Mathf.Cos(t * perSecond * 2f * Mathf.PI);

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        // 筐体の中央から光の粒を飛ばす。BonusConfirmed は粒ごとに色を変える。
        private void Burst(int count, Color color, float speed)
        {
            Vector2 center = overlayParent.InverseTransformPoint(shakeTarget.TransformPoint(shakeTarget.rect.center));
            for (var i = 0; i < count; i++)
            {
                ref var spark = ref _sparks[_nextSpark];
                _nextSpark = (_nextSpark + 1) % _sparks.Length;
                var angle = Random.Range(0f, Mathf.PI * 2f);
                spark.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(0.4f, 1f) * speed;
                spark.Life = Random.Range(0.6f, 1.1f);
                spark.Age = 0f;
                spark.Image.color = color == Color.white ? Color.HSVToRGB(Random.value, 0.6f, 1f) : color;
                spark.Image.rectTransform.anchoredPosition = center;
                spark.Image.enabled = true;
            }
        }

        private void UpdateSparks(float deltaTime)
        {
            for (var i = 0; i < _sparks.Length; i++)
            {
                ref var spark = ref _sparks[i];
                if (!spark.Image.enabled)
                {
                    continue;
                }

                spark.Age += deltaTime;
                if (spark.Age >= spark.Life)
                {
                    spark.Image.enabled = false;
                    continue;
                }

                spark.Velocity += Vector2.down * 900f * deltaTime;
                var rect = spark.Image.rectTransform;
                rect.anchoredPosition += spark.Velocity * deltaTime;
                var scale = 1f - spark.Age / spark.Life;
                rect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private static Image CreateImage(string name, RectTransform parent, Sprite sprite)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.gameObject.layer = parent.gameObject.layer;
            image.rectTransform.SetParent(parent, false);
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }
    }
}
