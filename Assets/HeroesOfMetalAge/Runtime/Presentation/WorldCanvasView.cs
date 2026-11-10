using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The adventure screen's UI: the hero panel, the mission card (it opens <see cref="MissionPanelView"/>) and the end day
    /// button. The army is shown on the hero screen. The map itself is drawn in the world (<see cref="AdventureMapView"/>)
    /// inside <see cref="MapViewport"/>, which must stay free of raycast targets.
    /// </summary>
    public sealed class WorldCanvasView : CanvasScreenView
    {
        [Header("Hero panel")]
        [SerializeField] private TMP_Text heroText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text movementText;
        [SerializeField] private Image movementBarImage;
        [SerializeField] private UiWidget endDayButton;

        [Header("Mission")]
        [Tooltip("The mission card; a click opens or closes the mission panel.")]
        [SerializeField] private UiWidget missionButton;
        [SerializeField] private TMP_Text missionNameText;
        [SerializeField] private TMP_Text missionProgressText;
        [SerializeField] private MissionPanelView missionPanel;

        [Header("Map")]
        [Tooltip("Screen area where the world camera draws the adventure map.")]
        [SerializeField] private RectTransform mapViewport;

        public RectTransform MapViewport => mapViewport;
        public UiWidget MissionButton => missionButton;
        public bool IsMissionOpen => missionPanel.IsOpen;

        public override void Refresh()
        {
            HeroDefinition hero = Catalog.Hero(Campaign.HeroId);
            int dailyMovement = Rules.DailyMovement(Campaign);
            UiBinding.SetText(heroText, hero.Name);
            UiBinding.SetText(levelText, Catalog.ClassOf(hero.Id).Name.ToUpperInvariant() + "  •  LEVEL " + Rules.Level(Campaign));
            UiBinding.SetSprite(portraitImage, Context.HeroPanelPortrait, Context.HeroTint);
            UiBinding.SetText(movementText, Campaign.Movement + " / " + dailyMovement);
            UiBinding.SetProgress(movementBarImage, Campaign.Movement / (float)dailyMovement);
            UiBinding.SetButton(endDayButton, "END DAY", !Session.IsBusy);
            UiBinding.SetButton(missionButton, string.Empty, true);
            UiBinding.SetText(missionNameText, Catalog.Mission.Name);
            UiBinding.SetText(missionProgressText, "GOALS REACHED " + Session.Objects.ReachedCount(Campaign) + " / " + Catalog.Mission.Objectives.Count);
            if (missionPanel.IsOpen)
            {
                missionPanel.Display();
            }
        }

        /// <summary>Closes the mission panel.</summary>
        /// <returns>True if it was open.</returns>
        public bool CloseMission()
        {
            if (!missionPanel.IsOpen)
            {
                return false;
            }

            missionPanel.SetOpen(false);
            return true;
        }

        protected override void ValidateReferences()
        {
            Require(heroText, nameof(heroText));
            Require(levelText, nameof(levelText));
            Require(portraitImage, nameof(portraitImage));
            Require(movementText, nameof(movementText));
            Require(movementBarImage, nameof(movementBarImage));
            Require(endDayButton, nameof(endDayButton));
            Require(missionButton, nameof(missionButton));
            Require(missionNameText, nameof(missionNameText));
            Require(missionProgressText, nameof(missionProgressText));
            Require(missionPanel, nameof(missionPanel));
            Require(mapViewport, nameof(mapViewport));
        }

        protected override void BindControls()
        {
            endDayButton.Bind(Session.EndDay);
            missionButton.Bind(ToggleMission);
            missionPanel.Initialize(Context, () => CloseMission());
            missionPanel.SetOpen(false);
        }

        private void ToggleMission()
        {
            if (CloseMission())
            {
                return;
            }

            missionPanel.SetOpen(true);
            missionPanel.Display();
        }
    }
}
