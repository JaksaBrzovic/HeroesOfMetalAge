using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>A short-lived world label (damage numbers) that rises and fades, then deactivates for reuse.</summary>
    public sealed class FloatingText : MonoBehaviour
    {
        private const float LIFETIME_SECONDS = 0.9f;

        [SerializeField] private TextMeshPro label;
        [Tooltip("How far the label rises over its lifetime, in world units.")]
        [SerializeField] private float rise = 0.7f;

        private Vector3 origin;
        private float age;

        public bool IsShowing => gameObject.activeSelf;

        public void Show(string text, Color color, Vector3 localPosition)
        {
            label.text = text;
            label.color = color;
            origin = localPosition;
            age = 0f;
            transform.localPosition = localPosition;
            gameObject.SetActive(true);
        }

        public void Tick(float deltaTime)
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            age += deltaTime;
            if (age >= LIFETIME_SECONDS)
            {
                gameObject.SetActive(false);
                return;
            }

            float progress = age / LIFETIME_SECONDS;
            transform.localPosition = origin + Vector3.up * (rise * progress);
            label.alpha = 1f - progress * progress;
        }
    }
}
