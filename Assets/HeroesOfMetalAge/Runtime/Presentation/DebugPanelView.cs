using System;
using System.Collections.Generic;
using System.Text;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// Development panel (F3): audition every music layer, event sound and unit voice, and shortcut the campaign. Instantiated only in the Editor and
    /// development builds; normal play never depends on it.
    /// </summary>
    public sealed class DebugPanelView : MonoBehaviour
    {
        [SerializeField] private RectTransform cueButtons;
        [SerializeField] private UiWidget cueButtonPrefab;
        [SerializeField] private UiWidget goldButton;
        [SerializeField] private UiWidget nextDayButton;
        [SerializeField] private UiWidget townButton;
        [SerializeField] private UiWidget artefactsButton;

        private readonly List<UiWidget> cueWidgets = new List<UiWidget>();
        private GameSession session;

        public IReadOnlyList<UiWidget> CueWidgets => cueWidgets;

        /// <exception cref="InvalidOperationException">A reference is missing on the DebugPanel prefab.</exception>
        public void Initialize(GameSession gameSession, IAudioControls audio)
        {
            if (cueButtons == null || cueButtonPrefab == null || goldButton == null || nextDayButton == null || townButton == null || artefactsButton == null)
            {
                throw new InvalidOperationException($"DebugPanelView on '{name}' has unassigned references. Assign them in the DebugPanel prefab.");
            }

            session = gameSession;
            foreach (MusicLayer layer in Enum.GetValues(typeof(MusicLayer)))
            {
                AddCueButton(layer + " Layer", () => audio.AuditionLayer(layer));
            }

            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                AddCueButton(SplitWords(cue.ToString()), () => audio.Audition(cue));
            }

            foreach (UnitDefinition unit in session.Catalog.Units)
            {
                string unitId = unit.Id;
                AddCueButton(unit.Name + " Voice", () => audio.AuditionVoice(unitId));
            }

            goldButton.Bind(session.DebugGold);
            nextDayButton.Bind(session.EndDay);
            townButton.Bind(session.DebugTown);
            artefactsButton.Bind(session.DebugArtefacts);
        }

        public void Refresh()
        {
            bool canUseCampaign = session.Screen != GameScreen.Battle && !session.IsBusy && session.Dialog == DialogKind.None;
            UiBinding.SetButton(goldButton, "+ 2000 GOLD", canUseCampaign);
            UiBinding.SetButton(nextDayButton, "NEXT DAY", canUseCampaign);
            UiBinding.SetButton(townButton, "TELEPORT TO TOWN", canUseCampaign);
            UiBinding.SetButton(artefactsButton, "GRANT ARTEFACTS", canUseCampaign);
        }

        private void AddCueButton(string label, Action play)
        {
            UiWidget button = Instantiate(cueButtonPrefab, cueButtons);
            button.name = label;
            button.SetTitle(label.ToUpperInvariant());
            button.Bind(play);
            cueWidgets.Add(button);
        }

        // "HeavyHit" -> "Heavy Hit"
        private static string SplitWords(string pascalCase)
        {
            StringBuilder words = new StringBuilder(pascalCase.Length + 4);
            for (int i = 0; i < pascalCase.Length; i++)
            {
                if (i > 0 && char.IsUpper(pascalCase[i]))
                {
                    words.Append(' ');
                }

                words.Append(pascalCase[i]);
            }

            return words.ToString();
        }
    }
}
