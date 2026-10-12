using System;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>Transient notice and damage banners; each banner's panel is shown only while its message is current.</summary>
    public sealed class NotificationsView : MonoBehaviour
    {
        [SerializeField] private GameObject noticePanel;
        [SerializeField] private TMP_Text noticeText;
        [SerializeField] private GameObject hitPanel;
        [SerializeField] private TMP_Text hitText;

        /// <exception cref="InvalidOperationException">A reference is missing on the Notifications prefab.</exception>
        public void Initialize()
        {
            if (noticePanel == null || noticeText == null || hitPanel == null || hitText == null)
            {
                throw new InvalidOperationException($"NotificationsView on '{name}' has unassigned references. Assign them in the Notifications prefab.");
            }
        }

        public void SetNotice(string notice)
        {
            UiBinding.SetText(noticeText, notice);
        }

        public void SetHit(string hit)
        {
            UiBinding.SetText(hitText, hit);
        }

        public void SetVisibility(bool isNoticeVisible, bool isHitVisible)
        {
            UiBinding.SetVisible(noticePanel, isNoticeVisible);
            UiBinding.SetVisible(hitPanel, isHitVisible);
        }
    }
}
