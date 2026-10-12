using System.Text;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The adventure screen's UI: the hero panel, objectives, army and the travel buttons. The map itself is drawn in the
    /// world (<see cref="AdventureMapView"/>) inside <see cref="MapViewport"/>, which must stay free of raycast targets.
    /// </summary>
    public sealed class WorldCanvasView : CanvasScreenView
    {
        [Header("Hero panel")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text heroText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text movementText;
        [SerializeField] private Image movementBarImage;
        [SerializeField] private TMP_Text objectivesText;
        [SerializeField] private UiWidget rideButton;
        [SerializeField] private UiWidget endDayButton;
        [SerializeField] private ArmySlotsView armySlots;

        [Header("Map")]
        [Tooltip("Screen area where the world camera draws the adventure map.")]
        [SerializeField] private RectTransform mapViewport;

        private readonly StringBuilder objectives = new StringBuilder();

        public RectTransform MapViewport => mapViewport;

        public override void Refresh()
        {
            HeroDefinition hero = Catalog.Hero(Campaign.HeroId);
            int dailyMovement = Rules.DailyMovement(Campaign);
            UiBinding.SetText(titleText, Catalog.Mission.Name);
            UiBinding.SetText(descriptionText, Catalog.Mission.Description);
            UiBinding.SetText(heroText, hero.Name);
            UiBinding.SetText(levelText, hero.ClassName + " / LEVEL " + Rules.Level(Campaign));
            UiBinding.SetSprite(portraitImage, Context.HeroIcon, Context.HeroTint);
            UiBinding.SetText(movementText, "MOVEMENT " + Campaign.Movement + " / " + dailyMovement);
            UiBinding.SetProgress(movementBarImage, Campaign.Movement / (float)dailyMovement);
            UiBinding.SetText(objectivesText, BuildObjectives());
            UiBinding.SetButton(rideButton, "RIDE [SPACE]", Session.Route.Count > 0 && !Session.IsBusy);
            UiBinding.SetButton(endDayButton, "END DAY [E]", !Session.IsBusy);
            armySlots.Display(Campaign.Army, true);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(descriptionText, nameof(descriptionText));
            Require(heroText, nameof(heroText));
            Require(levelText, nameof(levelText));
            Require(portraitImage, nameof(portraitImage));
            Require(movementText, nameof(movementText));
            Require(movementBarImage, nameof(movementBarImage));
            Require(objectivesText, nameof(objectivesText));
            Require(rideButton, nameof(rideButton));
            Require(endDayButton, nameof(endDayButton));
            Require(armySlots, nameof(armySlots));
            Require(mapViewport, nameof(mapViewport));
        }

        protected override void BindControls()
        {
            rideButton.Bind(Session.Travel);
            endDayButton.Bind(Session.EndDay);
            armySlots.Initialize(Context);
        }

        private string BuildObjectives()
        {
            MissionDefinition mission = Catalog.Mission;
            objectives.Clear();
            AppendObjective(Campaign.IsCompleted(mission.Encounter(EncounterKind.Friendly).Id), "Gather the willing");
            AppendObjective(Campaign.IsCompleted(mission.Encounter(EncounterKind.Sentinel).Id), "Defeat the pass wardens");
            AppendObjective(Campaign.HasRecruited, "Recruit at the town");
            AppendObjective(Campaign.IsCompleted(mission.Encounter(EncounterKind.Boss).Id), "Break the Black Banner");
            return objectives.ToString().TrimEnd('\n');
        }

        private void AppendObjective(bool isDone, string text)
        {
            objectives.Append(isDone ? "[+] " : "[ ] ").Append(text).Append('\n');
        }
    }
}
