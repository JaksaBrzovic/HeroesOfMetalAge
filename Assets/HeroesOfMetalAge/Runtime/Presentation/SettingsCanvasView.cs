using HeroesOfMetalAge.Application;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>The options overlay: audio volumes and mute, save, load, new mission and quit.</summary>
    public sealed class SettingsCanvasView : CanvasScreenView
    {
        [Header("Audio")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider effectsSlider;
        [SerializeField] private Slider voiceSlider;
        [SerializeField] private Toggle muteToggle;

        [Header("Campaign")]
        [SerializeField] private UiWidget saveButton;
        [SerializeField] private UiWidget loadButton;
        [SerializeField] private UiWidget newMissionButton;
        [SerializeField] private UiWidget backButton;
        [SerializeField] private UiWidget quitButton;

        private GameHud hud;

        /// <summary>The control selected when the overlay opens.</summary>
        public UiWidget BackButton => backButton;

        public void SetOwner(GameHud owner)
        {
            hud = owner;
        }

        public override void Refresh()
        {
            musicSlider.SetValueWithoutNotify(Context.Audio.MusicVolume);
            effectsSlider.SetValueWithoutNotify(Context.Audio.EffectsVolume);
            voiceSlider.SetValueWithoutNotify(Context.Audio.VoiceVolume);
            muteToggle.SetIsOnWithoutNotify(Context.Audio.IsMuted);

            // saving or loading in the middle of a battle or a ride would capture an unstable state
            bool canUseCampaign = Session.Screen != GameScreen.Battle && !Session.IsBusy;
            UiBinding.SetButton(saveButton, "SAVE", canUseCampaign);
            UiBinding.SetButton(loadButton, "LOAD", canUseCampaign && Session.HasSave);
            UiBinding.SetButton(newMissionButton, "NEW MISSION", canUseCampaign);
        }

        protected override void ValidateReferences()
        {
            Require(musicSlider, nameof(musicSlider));
            Require(effectsSlider, nameof(effectsSlider));
            Require(voiceSlider, nameof(voiceSlider));
            Require(muteToggle, nameof(muteToggle));
            Require(saveButton, nameof(saveButton));
            Require(loadButton, nameof(loadButton));
            Require(newMissionButton, nameof(newMissionButton));
            Require(backButton, nameof(backButton));
            Require(quitButton, nameof(quitButton));
            Require(hud, nameof(hud));
        }

        protected override void BindControls()
        {
            saveButton.Bind(() => Session.Save());
            loadButton.Bind(HandleLoad);
            newMissionButton.Bind(HandleNewMission);
            backButton.Bind(hud.CloseSettings);
            quitButton.Bind(UnityEngine.Application.Quit);
        }

        private void OnEnable()
        {
            musicSlider.onValueChanged.AddListener(HandleMusicChanged);
            effectsSlider.onValueChanged.AddListener(HandleEffectsChanged);
            voiceSlider.onValueChanged.AddListener(HandleVoiceChanged);
            muteToggle.onValueChanged.AddListener(HandleMuteChanged);
        }

        private void OnDisable()
        {
            musicSlider.onValueChanged.RemoveListener(HandleMusicChanged);
            effectsSlider.onValueChanged.RemoveListener(HandleEffectsChanged);
            voiceSlider.onValueChanged.RemoveListener(HandleVoiceChanged);
            muteToggle.onValueChanged.RemoveListener(HandleMuteChanged);
        }

        private void HandleLoad()
        {
            Session.Load();
            hud.CloseSettings();
        }

        private void HandleNewMission()
        {
            Session.Restart();
            hud.CloseSettings();
        }

        // slider events can arrive before Initialize when the prefab is first enabled, so the context is checked
        private void HandleMusicChanged(float value)
        {
            if (Context != null)
            {
                Context.Audio.MusicVolume = value;
            }
        }

        private void HandleEffectsChanged(float value)
        {
            if (Context != null)
            {
                Context.Audio.EffectsVolume = value;
            }
        }

        private void HandleVoiceChanged(float value)
        {
            if (Context != null)
            {
                Context.Audio.VoiceVolume = value;
            }
        }

        private void HandleMuteChanged(bool isMuted)
        {
            if (Context != null)
            {
                Context.Audio.IsMuted = isMuted;
                hud.Invalidate();
            }
        }
    }
}
