using System;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// Molten gold poured over a main menu title (an Image with the MenuGild material): it pours while the owner wants it
    /// (hovered or clicked) and drains away again when it no longer does. The title gets its own copy of the material, so
    /// the shared asset never changes. The owning button drives it with <see cref="Tick"/>.
    /// </summary>
    public sealed class GildEffect : MonoBehaviour
    {
        private static readonly int GildProgressId = Shader.PropertyToID("_GildProgress");

        [Tooltip("The title, drawn with the MenuGild material.")]
        [SerializeField] private Image image;
        [Tooltip("Seconds the gold takes to cover the whole title.")]
        [Min(0.05f)]
        [SerializeField] private float pourSeconds = 0.9f;
        [Tooltip("Seconds the gold takes to drain away once the pointer left.")]
        [Min(0.05f)]
        [SerializeField] private float drainSeconds = 0.6f;

        private Material material;
        private float progress;
        private bool isPoured;

        public Image Image => image;
        /// <summary>0 the plain title, 1 fully gilded.</summary>
        public float Progress => progress;
        public bool IsPoured => isPoured;

        /// <exception cref="InvalidOperationException">The image is missing.</exception>
        public void Initialize()
        {
            if (image == null)
            {
                throw new InvalidOperationException($"GildEffect on '{name}' needs its image.");
            }

            if (material == null && image.material != null && image.material.HasProperty(GildProgressId))
            {
                material = new Material(image.material);
                image.material = material;
            }

            Clear();
        }

        /// <summary>True pours the gold, false drains it.</summary>
        public void SetPoured(bool poured)
        {
            isPoured = poured;
        }

        /// <summary>Back to the plain title at once.</summary>
        public void Clear()
        {
            isPoured = false;
            progress = 0f;
            Show();
        }

        internal void Tick(float deltaSeconds)
        {
            float delta = Mathf.Max(0f, deltaSeconds);
            float next = isPoured ? Mathf.Min(1f, progress + delta / pourSeconds) : Mathf.Max(0f, progress - delta / drainSeconds);
            if (next != progress)
            {
                progress = next;
                Show();
            }
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        private void Show()
        {
            if (material != null)
            {
                material.SetFloat(GildProgressId, progress);
            }
        }
    }
}
