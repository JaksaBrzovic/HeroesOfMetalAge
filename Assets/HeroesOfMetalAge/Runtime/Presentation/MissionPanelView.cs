using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The mission's details over the adventure map: its name, story, every goal with a check mark when reached and how
    /// many are reached. Opened from the mission card on the adventure screen; presentation only, it never changes the
    /// session.
    /// </summary>
    public sealed class MissionPanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text descriptionText;
        [Tooltip("One row per mission goal, top to bottom: the goal's text, a check mark when reached and the box behind it.")]
        [SerializeField] private List<UiWidget> objectiveRows = new List<UiWidget>();
        [SerializeField] private UiWidget closeButton;

        private CanvasViewContext context;

        public bool IsOpen => gameObject.activeSelf;

        /// <exception cref="InvalidOperationException">A reference is missing on the prefab.</exception>
        public void Initialize(CanvasViewContext viewContext, Action close)
        {
            if (titleText == null || progressText == null || descriptionText == null || closeButton == null || objectiveRows.Count == 0
                || objectiveRows.Exists(row => row == null))
            {
                throw new InvalidOperationException($"MissionPanelView on '{name}' has unassigned references. Assign them in the Adventure prefab.");
            }

            context = viewContext;
            closeButton.Bind(close);
        }

        public void SetOpen(bool isOpen)
        {
            UiBinding.SetVisible(gameObject, isOpen);
        }

        public void Display()
        {
            GameSession session = context.Session;
            MissionDefinition mission = session.Catalog.Mission;
            IReadOnlyList<ObjectiveDefinition> goals = mission.Objectives;
            UiBinding.SetText(titleText, mission.Name);
            UiBinding.SetText(progressText, "GOALS REACHED " + session.Objects.ReachedCount(session.Campaign) + " / " + goals.Count);
            UiBinding.SetText(descriptionText, mission.Description + "\n<size=80%>* required to finish the mission</size>");
            UiBinding.SetButton(closeButton, "CLOSE", true);
            for (int i = 0; i < objectiveRows.Count; i++)
            {
                UiWidget row = objectiveRows[i];
                bool hasGoal = i < goals.Count;
                UiBinding.SetVisible(row.gameObject, hasGoal);
                if (!hasGoal)
                {
                    continue;
                }

                ObjectiveDefinition goal = goals[i];
                bool isReached = session.Objects.IsReached(session.Campaign, goal);
                string text = goal.IsRequired ? goal.Text + " *" : goal.Text;
                row.Display(isReached ? "<s>" + text + "</s>" : text, string.Empty, string.Empty, isReached ? context.Theme.CheckMark : null,
                    Color.white, false);
            }
        }
    }
}
