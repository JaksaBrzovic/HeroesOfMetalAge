using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The main menu's New Game button: the title over an arm. Hovering flexes the arm (the flex frames forward, holding the
    /// last), leaving relaxes it (the same frames backwards from wherever it is). A click finishes the flex first if it is
    /// still running, then raises the fist into the metal horns and holds them while molten gold pours over the title
    /// (its Gild material). <see cref="Clicked"/> is raised at once; the animation only shows it. Frames advance in
    /// unscaled time.
    /// </summary>
    public sealed class NewGameButtonView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly int GildProgressId = Shader.PropertyToID("_GildProgress");

        [SerializeField] private Button button;
        [Tooltip("Shows the current frame; its first flex frame is the resting arm.")]
        [SerializeField] private Image armImage;
        [Tooltip("Relaxed arm to flexed biceps; the last frame equals the first horns frame.")]
        [SerializeField] private Sprite[] flexFrames;
        [Tooltip("Flexed fist to metal horns; the first frame repeats the last flex frame and is skipped.")]
        [SerializeField] private Sprite[] hornsFrames;
        [Min(1f)]
        [SerializeField] private float framesPerSecond = 16f;
        [Tooltip("The New Game title, drawn with the Gild material; the button gives it its own copy.")]
        [SerializeField] private Image titleImage;
        [Tooltip("Seconds the gold takes to pour over the title, from the start of the horns.")]
        [Min(0.05f)]
        [SerializeField] private float gildSeconds = 1.1f;

        private int flexIndex;
        // 0 until the horns start; the horns' first frame is never shown, it is the flex's last
        private int hornsIndex;
        private float frameTimer;
        private bool isHovered;
        private bool isClicked;
        private Material gildMaterial;
        private float gild;

        /// <summary>The pointer moved onto the button (not after it was clicked).</summary>
        public event Action Hovered;
        public event Action Clicked;

        public bool IsClicked => isClicked;
        /// <summary>The sprite on screen now.</summary>
        public Sprite CurrentFrame => hornsIndex > 0 ? hornsFrames[hornsIndex] : flexFrames[flexIndex];
        public bool IsHornsComplete => hornsIndex == hornsFrames.Length - 1;
        /// <summary>0 before the horns, 1 once the gold has covered the title.</summary>
        public float GildProgress => gild;

        /// <exception cref="InvalidOperationException">A reference or a frame is missing on the prefab.</exception>
        public void Initialize()
        {
            if (button == null || armImage == null || titleImage == null || !HasFrames(flexFrames) || !HasFrames(hornsFrames) || hornsFrames.Length < 2)
            {
                throw new InvalidOperationException($"NewGameButtonView on '{name}' needs its button, arm and title images, flex frames and at least two horns frames.");
            }

            // a copy, so the shared material asset never changes at runtime
            if (gildMaterial == null && titleImage.material.HasProperty(GildProgressId))
            {
                gildMaterial = new Material(titleImage.material);
                titleImage.material = gildMaterial;
            }

            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
            ResetAnimation();
        }

        /// <summary>Back to the resting arm, ready for another click.</summary>
        public void ResetAnimation()
        {
            flexIndex = 0;
            hornsIndex = 0;
            frameTimer = 0f;
            isHovered = false;
            isClicked = false;
            gild = 0f;
            ShowFrame();
            ShowGild();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHovered(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHovered(false);
        }

        /// <summary>The pointer moved onto (true) or off (false) the button; tests call it directly.</summary>
        public void SetHovered(bool hovered)
        {
            if (isHovered == hovered)
            {
                return;
            }

            isHovered = hovered;
            if (hovered && !isClicked && button.IsInteractable())
            {
                Hovered?.Invoke();
            }
        }

        /// <summary>Advances the animation by whole frames; Update passes unscaled time, tests pass their own.</summary>
        internal void Tick(float deltaSeconds)
        {
            frameTimer += Mathf.Max(0f, deltaSeconds);
            float frameSeconds = 1f / framesPerSecond;
            bool isChanged = false;
            while (frameTimer >= frameSeconds)
            {
                frameTimer -= frameSeconds;
                isChanged |= Step();
            }

            if (isChanged)
            {
                ShowFrame();
            }

            if (hornsIndex > 0 && gild < 1f)
            {
                gild = Mathf.Min(1f, gild + Mathf.Max(0f, deltaSeconds) / gildSeconds);
                ShowGild();
            }
        }

        internal void Click()
        {
            HandleClicked();
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }

            if (gildMaterial != null)
            {
                Destroy(gildMaterial);
            }
        }

        private void HandleClicked()
        {
            if (isClicked)
            {
                return;
            }

            isClicked = true;
            Clicked?.Invoke();
        }

        // one frame towards the target: the flex's end while hovered or clicked, its start otherwise, then the horns
        private bool Step()
        {
            int lastFlex = flexFrames.Length - 1;
            if (isClicked)
            {
                if (flexIndex < lastFlex)
                {
                    flexIndex++;
                    return true;
                }

                if (hornsIndex < hornsFrames.Length - 1)
                {
                    hornsIndex++;
                    return true;
                }

                return false;
            }

            if (isHovered && flexIndex < lastFlex)
            {
                flexIndex++;
                return true;
            }

            if (!isHovered && flexIndex > 0)
            {
                flexIndex--;
                return true;
            }

            return false;
        }

        private void ShowFrame()
        {
            if (armImage.sprite != CurrentFrame)
            {
                armImage.sprite = CurrentFrame;
            }
        }

        private void ShowGild()
        {
            if (gildMaterial != null)
            {
                gildMaterial.SetFloat(GildProgressId, gild);
            }
        }

        private static bool HasFrames(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0)
            {
                return false;
            }

            foreach (Sprite frame in frames)
            {
                if (frame == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
