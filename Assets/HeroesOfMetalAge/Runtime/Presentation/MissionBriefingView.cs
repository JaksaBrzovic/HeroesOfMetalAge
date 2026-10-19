using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Application;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>Where the mission briefing is in its sequence.</summary>
    public enum BriefingPhase
    {
        /// <summary>Nothing shown; waits for a new mission.</summary>
        Hidden,
        /// <summary>The scroll breaks its seal and unrolls.</summary>
        Opening,
        /// <summary>The page is shown until a click.</summary>
        Reading,
        /// <summary>The scroll rolls up.</summary>
        Closing,
        /// <summary>The rolled scroll is hurled away off the screen.</summary>
        Throwing
    }

    /// <summary>
    /// The story told on a big scroll as a new mission starts, in place of the old intro dialog. The scroll unrolls with
    /// the first page; half a second later a huge explosion goes off over it, the whole screen shakes and a boom sounds.
    /// A click rolls the scroll up and unrolls it again with the next page; a click on the last page rolls it up and hurls
    /// it away off the screen, and the mission starts. Clicks while it rolls are ignored. It starts when the session shows
    /// the mission intro and the given condition holds (the front end has finished), and steps aside if the intro ends
    /// another way (a loaded campaign). Time is unscaled.
    /// </summary>
    public sealed class MissionBriefingView : MonoBehaviour
    {
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");

        [Tooltip("A dim over the whole screen that takes every click; it carries the click area's Button.")]
        [SerializeField] private Image backdrop;
        [SerializeField] private Button clickArea;
        [Tooltip("The scroll with its text: shaken by the explosion and thrown away at the end.")]
        [SerializeField] private RectTransform content;
        [SerializeField] private Image scroll;
        [Tooltip("Rolled and sealed to fully open.")]
        [SerializeField] private Sprite[] scrollFrames = Array.Empty<Sprite>();
        [SerializeField] private TMP_Text pageText;
        [Tooltip("Square Image with the NukeExplosion material, over the scroll.")]
        [SerializeField] private Image explosion;
        [Tooltip("White over the whole screen as the bomb goes off.")]
        [SerializeField] private Image flash;

        [Header("Timing (seconds)")]
        [Min(0.01f)]
        [SerializeField] private float openSeconds = 0.6f;
        [Min(0.01f)]
        [SerializeField] private float closeSeconds = 0.45f;
        [Min(0.01f)]
        [SerializeField] private float textFadeSeconds = 0.4f;
        [Tooltip("After the first page has opened.")]
        [Min(0f)]
        [SerializeField] private float explosionDelay = 0.5f;
        [Min(0.1f)]
        [SerializeField] private float explosionSeconds = 4.5f;
        [Min(0.01f)]
        [SerializeField] private float flashSeconds = 0.6f;
        [Min(0.01f)]
        [SerializeField] private float throwSeconds = 0.9f;

        [Header("Shake and throw")]
        [Min(0f)]
        [SerializeField] private float shakeSeconds = 1.6f;
        [Tooltip("Canvas units at the start of the shake.")]
        [Min(0f)]
        [SerializeField] private float shakeAmplitude = 30f;
        [Tooltip("Where the thrown scroll flies to, in canvas units from where it was.")]
        [SerializeField] private Vector2 throwDistance = new Vector2(1500f, 900f);
        [Tooltip("Turns the scroll spins while it flies.")]
        [SerializeField] private float throwTurns = 2.5f;
        [Range(0.01f, 1f)]
        [SerializeField] private float throwFinalScale = 0.2f;
        [Range(0f, 1f)]
        [SerializeField] private float backdropAlpha = 0.55f;

        private GameSession session;
        private IAudioControls audioControls;
        private IReadOnlyList<string> pages = Array.Empty<string>();
        private Func<bool> canStart;
        private Action<float, float> shakeScreen;
        private Material explosionMaterial;
        private Vector2 contentHome;
        private int page;
        private float phaseSeconds;
        // -1 until the bomb goes off
        private float explosionTime = -1f;
        private bool hasExploded;
        private float shakeLeft;
        private readonly System.Random random = new System.Random();

        public BriefingPhase Phase { get; private set; }
        public int Page => page;
        public int PageCount => pages.Count;
        public TMP_Text PageText => pageText;
        public Image Scroll => scroll;
        public RectTransform Content => content;
        public Image Explosion => explosion;
        public Image Flash => flash;
        public int ScrollFrameCount => scrollFrames.Length;
        /// <summary>0 before the bomb goes off and after the cloud has gone, between while it rises.</summary>
        public float ExplosionProgress => explosionTime < 0f ? 0f : Mathf.Clamp01(explosionTime / explosionSeconds);

        /// <param name="briefingPages">The story, one page each; none skips the briefing and starts the mission at once.</param>
        /// <param name="canBegin">The briefing waits while this is false (the front end still shows).</param>
        /// <param name="shake">Shakes the rest of the screen: seconds, then canvas units.</param>
        /// <exception cref="InvalidOperationException">A reference or a frame is missing on the MissionBriefing prefab.</exception>
        public void Initialize(GameSession gameSession, IAudioControls audio, IReadOnlyList<string> briefingPages, Func<bool> canBegin,
            Action<float, float> shake)
        {
            if (backdrop == null || clickArea == null || content == null || scroll == null || pageText == null || explosion == null || flash == null
                || scrollFrames.Length < 2 || Array.Exists(scrollFrames, frame => frame == null))
            {
                throw new InvalidOperationException($"MissionBriefingView on '{name}' has unassigned references. Assign them in the MissionBriefing prefab.");
            }

            session = gameSession;
            audioControls = audio;
            pages = briefingPages ?? Array.Empty<string>();
            canStart = canBegin;
            shakeScreen = shake;
            contentHome = content.anchoredPosition;
            if (explosionMaterial == null && explosion.material.HasProperty(ProgressId))
            {
                // a copy, so the shared material asset never changes at runtime
                explosionMaterial = new Material(explosion.material);
                explosion.material = explosionMaterial;
            }

            clickArea.onClick.RemoveListener(Advance);
            clickArea.onClick.AddListener(Advance);
            Enter(BriefingPhase.Hidden);
            Apply();
        }

        /// <summary>A click: on a shown page, rolls the scroll up for the next page or, on the last, throws it away.</summary>
        public void Advance()
        {
            if (Phase == BriefingPhase.Reading)
            {
                Enter(BriefingPhase.Closing);
            }
        }

        internal void Tick(float deltaSeconds)
        {
            float delta = Mathf.Max(0f, deltaSeconds);
            phaseSeconds += delta;
            if (explosionTime >= 0f)
            {
                explosionTime += delta;
            }

            shakeLeft = Mathf.Max(0f, shakeLeft - delta);
            bool isIntro = session.Dialog == DialogKind.Intro;
            switch (Phase)
            {
                case BriefingPhase.Hidden:
                    if (isIntro && canStart())
                    {
                        Begin();
                    }

                    break;
                case BriefingPhase.Opening:
                    if (phaseSeconds >= openSeconds)
                    {
                        Enter(BriefingPhase.Reading);
                    }

                    break;
                case BriefingPhase.Reading:
                    if (page == 0 && !hasExploded && phaseSeconds >= explosionDelay)
                    {
                        Explode();
                    }

                    break;
                case BriefingPhase.Closing:
                    if (phaseSeconds >= closeSeconds)
                    {
                        if (page < pages.Count - 1)
                        {
                            page++;
                            Enter(BriefingPhase.Opening);
                        }
                        else
                        {
                            Enter(BriefingPhase.Throwing);
                        }
                    }

                    break;
                case BriefingPhase.Throwing:
                    if (phaseSeconds >= throwSeconds)
                    {
                        Enter(BriefingPhase.Hidden);
                        session.StartMission();
                    }

                    break;
            }

            // the intro ended another way (a loaded campaign): step aside
            if (Phase != BriefingPhase.Hidden && !isIntro)
            {
                Enter(BriefingPhase.Hidden);
            }

            Apply();
        }

        private void Update()
        {
            if (session == null)
            {
                return;
            }

            if (Phase == BriefingPhase.Reading && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            {
                Advance();
            }

            Tick(Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            if (clickArea != null)
            {
                clickArea.onClick.RemoveListener(Advance);
            }

            if (explosionMaterial != null)
            {
                Destroy(explosionMaterial);
            }
        }

        private void Begin()
        {
            page = 0;
            hasExploded = false;
            explosionTime = -1f;
            shakeLeft = 0f;
            if (pages.Count == 0)
            {
                session.StartMission();
                return;
            }

            UiBinding.SetText(pageText, pages[0]);
            Enter(BriefingPhase.Opening);
        }

        private void Explode()
        {
            hasExploded = true;
            explosionTime = 0f;
            shakeLeft = shakeSeconds;
            audioControls.Play(SoundCue.BriefingExplosion);
            shakeScreen?.Invoke(shakeSeconds, shakeAmplitude);
        }

        private void Enter(BriefingPhase phase)
        {
            Phase = phase;
            phaseSeconds = 0f;
            if (phase == BriefingPhase.Opening)
            {
                UiBinding.SetText(pageText, pages[page]);
            }
        }

        private void Apply()
        {
            // the root stays active so it notices the next mission; its parts show while the briefing does
            bool isShown = Phase != BriefingPhase.Hidden;
            UiBinding.SetVisible(backdrop.gameObject, isShown);
            UiBinding.SetVisible(content.gameObject, isShown);

            // the scroll's frames: forward while it opens, backwards while it closes, rolled while thrown
            float unrolled = Phase == BriefingPhase.Opening ? phaseSeconds / openSeconds
                : Phase == BriefingPhase.Reading ? 1f
                : Phase == BriefingPhase.Closing ? 1f - phaseSeconds / closeSeconds
                : 0f;
            Sprite frame = scrollFrames[Mathf.RoundToInt(Mathf.Clamp01(unrolled) * (scrollFrames.Length - 1))];
            if (scroll.sprite != frame)
            {
                scroll.sprite = frame;
            }

            float textAlpha = Phase == BriefingPhase.Reading ? Mathf.Clamp01(phaseSeconds / textFadeSeconds)
                : Phase == BriefingPhase.Closing ? Mathf.Clamp01(1f - phaseSeconds / (closeSeconds * 0.4f))
                : 0f;
            if (pageText.alpha != textAlpha)
            {
                pageText.alpha = textAlpha;
            }

            float thrown = Phase == BriefingPhase.Throwing ? Mathf.Clamp01(phaseSeconds / throwSeconds) : 0f;
            SetAlpha(backdrop, backdropAlpha * (isShown ? 1f - thrown : 0f));
            ApplyContent(thrown);

            float progress = ExplosionProgress;
            bool isExploding = progress > 0f && progress < 1f;
            UiBinding.SetVisible(explosion.gameObject, isExploding);
            if (isExploding && explosionMaterial != null)
            {
                explosionMaterial.SetFloat(ProgressId, progress);
            }

            float flashAlpha = explosionTime >= 0f ? Mathf.Clamp01(1f - explosionTime / flashSeconds) : 0f;
            flashAlpha *= flashAlpha;
            UiBinding.SetVisible(flash.gameObject, flashAlpha > 0f);
            SetAlpha(flash, flashAlpha);
        }

        // hurled away: spinning faster and faster, flying off along a slight arc and getting smaller as it goes
        private void ApplyContent(float thrown)
        {
            float eased = thrown * thrown;
            Vector2 shake = shakeLeft > 0f
                ? new Vector2((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f) * (shakeAmplitude * shakeLeft / shakeSeconds)
                : Vector2.zero;
            Vector2 flight = new Vector2(throwDistance.x * eased, throwDistance.y * (eased * 0.6f + Mathf.Sin(thrown * Mathf.PI) * 0.25f));
            content.anchoredPosition = contentHome + flight + shake;
            content.localEulerAngles = new Vector3(0f, 0f, -360f * throwTurns * eased);
            float scale = Mathf.Lerp(1f, throwFinalScale, eased);
            content.localScale = new Vector3(scale, scale, 1f);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            if (color.a != alpha)
            {
                color.a = alpha;
                graphic.color = color;
            }
        }
    }
}
