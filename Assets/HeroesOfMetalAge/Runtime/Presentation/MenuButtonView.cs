using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// A main menu entry drawn as artwork (Load Game, Credits, Quit): its title grows a little while hovered and a click is
    /// reported once until <see cref="ResetClick"/>. What the click starts is shown by the entry's own effect, driven by
    /// the front end's phases. Hover eases in unscaled time.
    /// </summary>
    public sealed class MenuButtonView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [Tooltip("Scaled up while hovered, usually the title.")]
        [SerializeField] private RectTransform highlight;
        [Min(1f)]
        [SerializeField] private float hoverScale = 1.08f;
        [Min(0.01f)]
        [SerializeField] private float hoverSeconds = 0.12f;

        private float hover;
        private bool isHovered;
        private bool isClicked;

        public event Action Clicked;

        public bool IsHovered => isHovered;
        /// <summary>0 at rest, 1 fully highlighted.</summary>
        public float Hover => hover;

        /// <exception cref="InvalidOperationException">A reference is missing on the prefab.</exception>
        public void Initialize()
        {
            if (button == null || highlight == null)
            {
                throw new InvalidOperationException($"MenuButtonView on '{name}' needs its button and highlight.");
            }

            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
            ResetClick();
        }

        /// <summary>Takes clicks again and drops the highlight, e.g. back in the menu after the credits.</summary>
        public void ResetClick()
        {
            isClicked = false;
            isHovered = false;
            hover = 0f;
            ApplyHover();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHovered(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHovered(false);
        }

        /// <summary>The pointer moved onto (true) or off (false) the entry; tests call it directly.</summary>
        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
        }

        internal void Tick(float deltaSeconds)
        {
            float target = isHovered && !isClicked && button.IsInteractable() ? 1f : 0f;
            float next = Mathf.MoveTowards(hover, target, Mathf.Max(0f, deltaSeconds) / hoverSeconds);
            if (next != hover)
            {
                hover = next;
                ApplyHover();
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

        private void ApplyHover()
        {
            float scale = Mathf.Lerp(1f, hoverScale, Mathf.SmoothStep(0f, 1f, hover));
            highlight.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
