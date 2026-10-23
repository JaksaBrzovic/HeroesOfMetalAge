using System;
using System.Collections.Generic;
using System.Text;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// Development panel (D or F3) in two tabs. GENERAL: options (speed mode and the toggles the bootstrap adds), campaign
    /// shortcuts and dialogue previews. SOUNDS: audition every music layer and track, the riding loop, every event sound and
    /// unit voice. Instantiated only in the Editor and development builds; normal play never depends on it.
    /// </summary>
    public sealed class DebugPanelView : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private UiWidget generalTab;
        [SerializeField] private UiWidget soundsTab;
        [SerializeField] private GameObject generalPage;
        [SerializeField] private GameObject soundsPage;

        [Header("General")]
        [Tooltip("Speed mode and the on/off options.")]
        [SerializeField] private RectTransform optionButtons;
        [Tooltip("Development actions added by others, such as the dialogue previews.")]
        [SerializeField] private RectTransform actionButtons;

        [Header("Sounds")]
        [SerializeField] private RectTransform cueButtons;
        [SerializeField] private UiWidget cueButtonPrefab;
        [SerializeField] private UiWidget goldButton;
        [SerializeField] private UiWidget nextDayButton;
        [SerializeField] private UiWidget townButton;
        [SerializeField] private UiWidget artefactsButton;
        [Tooltip("Shows the adventure map's terrain logic layer over the map image.")]
        [SerializeField] private UiWidget terrainButton;

        private readonly List<UiWidget> cueWidgets = new List<UiWidget>();
        private GameSession session;
        private UiWidget speedButton;
        private bool isSoundsTabOpen;
        private readonly List<Toggle> toggles = new List<Toggle>();

        public IReadOnlyList<UiWidget> CueWidgets => cueWidgets;
        public bool IsSoundsTabOpen => isSoundsTabOpen;
        /// <summary>Toggles speed mode: the hero rides almost instantly.</summary>
        public UiWidget SpeedButton => speedButton;

        /// <exception cref="InvalidOperationException">A reference is missing on the DebugPanel prefab.</exception>
        public void Initialize(GameSession gameSession, IAudioControls audio, Action toggleTerrainOverlay)
        {
            if (cueButtons == null || cueButtonPrefab == null || goldButton == null || nextDayButton == null || townButton == null || artefactsButton == null
                || terrainButton == null || generalTab == null || soundsTab == null || generalPage == null || soundsPage == null || optionButtons == null
                || actionButtons == null)
            {
                throw new InvalidOperationException($"DebugPanelView on '{name}' has unassigned references. Assign them in the DebugPanel prefab.");
            }

            session = gameSession;
            foreach (MusicLayer layer in Enum.GetValues(typeof(MusicLayer)))
            {
                AddCueButton(layer + " Layer", () => audio.AuditionLayer(layer));
            }

            foreach (MusicTrack track in Enum.GetValues(typeof(MusicTrack)))
            {
                AddCueButton(track + " Music", () => audio.AuditionMusic(track));
            }

            AddCueButton("Riding Loop", audio.AuditionRidingLoop);
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
            terrainButton.Bind(toggleTerrainOverlay);
            generalTab.Bind(() => ShowTab(false));
            soundsTab.Bind(() => ShowTab(true));
            ShowTab(false);
            // an instance of the saved button prefab among the options
            speedButton = Instantiate(cueButtonPrefab, optionButtons);
            speedButton.name = "SpeedMode";
            speedButton.Bind(() =>
            {
                session.IsSpeedMode = !session.IsSpeedMode;
                Refresh();
            });
        }

        /// <summary>Adds an on/off option owned elsewhere beside the campaign actions; its label shows the current state.</summary>
        public UiWidget AddToggle(string label, Func<bool> isOn, Action<bool> setOn)
        {
            UiWidget button = Instantiate(cueButtonPrefab, optionButtons);
            button.name = label;
            Toggle toggle = new Toggle(button, label.ToUpperInvariant(), isOn);
            button.Bind(() =>
            {
                setOn(!isOn());
                toggle.Show();
            });
            toggles.Add(toggle);
            toggle.Show();
            return button;
        }

        /// <summary>Adds a button for a development action owned elsewhere to the first tab (the bootstrap adds dialogue previews).</summary>
        public UiWidget AddAction(string label, Action action)
        {
            UiWidget button = Instantiate(cueButtonPrefab, actionButtons);
            button.name = label;
            button.SetTitle(label.ToUpperInvariant());
            button.Bind(action);
            return button;
        }

        /// <summary>Opens the sounds tab (true) or the general one (false).</summary>
        public void ShowTab(bool isSounds)
        {
            isSoundsTabOpen = isSounds;
            UiBinding.SetVisible(generalPage, !isSounds);
            UiBinding.SetVisible(soundsPage, isSounds);
            UiBinding.SetButton(generalTab, isSounds ? "GENERAL" : "[ GENERAL ]", true);
            UiBinding.SetButton(soundsTab, isSounds ? "[ SOUNDS ]" : "SOUNDS", true);
        }

        public void Refresh()
        {
            bool canUseCampaign = session.Screen != GameScreen.Battle && !session.IsBusy && session.Dialog == DialogKind.None;
            UiBinding.SetButton(goldButton, "+ 2000 GOLD", canUseCampaign);
            UiBinding.SetButton(nextDayButton, "NEXT DAY", canUseCampaign);
            UiBinding.SetButton(townButton, "TELEPORT TO TOWN", canUseCampaign);
            UiBinding.SetButton(artefactsButton, "GRANT ARTEFACTS", canUseCampaign);
            UiBinding.SetButton(terrainButton, "TERRAIN OVERLAY", true);
            UiBinding.SetButton(speedButton, session.IsSpeedMode ? "SPEED MODE: ON" : "SPEED MODE: OFF", true);
            foreach (Toggle toggle in toggles)
            {
                toggle.Show();
            }
        }

        private void AddCueButton(string label, Action play)
        {
            UiWidget button = Instantiate(cueButtonPrefab, cueButtons);
            button.name = label;
            button.SetTitle(label.ToUpperInvariant());
            button.Bind(play);
            cueWidgets.Add(button);
        }

        private sealed class Toggle
        {
            private readonly UiWidget button;
            private readonly string label;
            private readonly Func<bool> isOn;

            public Toggle(UiWidget button, string label, Func<bool> isOn)
            {
                this.button = button;
                this.label = label;
                this.isOn = isOn;
            }

            public void Show()
            {
                UiBinding.SetButton(button, label + (isOn() ? ": ON" : ": OFF"), true);
            }
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
