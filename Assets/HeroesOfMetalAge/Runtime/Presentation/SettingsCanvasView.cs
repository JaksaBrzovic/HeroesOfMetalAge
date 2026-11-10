using HeroesOfMetalAge.Application;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>The options overlay: audio volumes and mute, quick combat and quick battle animation, save, load, new mission and quit.</summary>
    public sealed class SettingsCanvasView : CanvasScreenView
    {
        private const string QUICK_COMBAT_PREFERENCE = "options.quickCombat";
        private const string QUICK_ANIMATION_PREFERENCE = "options.quickBattleAnimation";

        [Header("Audio")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider effectsSlider;
        [SerializeField] private Slider voiceSlider;
        [SerializeField] private Toggle muteToggle;

        [Header("Gameplay")]
        [Tooltip("Battles are resolved at once, straight to the victory or defeat screen. Remembered between runs.")]
        [SerializeField] private Toggle quickCombatToggle;
        [Tooltip("Stacks cross the battlefield at the quick pace set on the mission. Remembered between runs.")]
        [SerializeField] private Toggle quickAnimationToggle;

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
            quickCombatToggle.SetIsOnWithoutNotify(Session.IsQuickCombat);
            quickAnimationToggle.SetIsOnWithoutNotify(Session.IsQuickBattleAnimation);

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
            Require(quickCombatToggle, nameof(quickCombatToggle));
            Require(quickAnimationToggle, nameof(quickAnimationToggle));
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
            Session.IsQuickCombat = PlayerPrefs.GetInt(QUICK_COMBAT_PREFERENCE, 0) == 1;
            Session.IsQuickBattleAnimation = PlayerPrefs.GetInt(QUICK_ANIMATION_PREFERENCE, 0) == 1;
        }

        private void OnEnable()
        {
            musicSlider.onValueChanged.AddListener(HandleMusicChanged);
            effectsSlider.onValueChanged.AddListener(HandleEffectsChanged);
            voiceSlider.onValueChanged.AddListener(HandleVoiceChanged);
            muteToggle.onValueChanged.AddListener(HandleMuteChanged);
            quickCombatToggle.onValueChanged.AddListener(HandleQuickCombatChanged);
            quickAnimationToggle.onValueChanged.AddListener(HandleQuickAnimationChanged);
        }

        private void OnDisable()
        {
            musicSlider.onValueChanged.RemoveListener(HandleMusicChanged);
            effectsSlider.onValueChanged.RemoveListener(HandleEffectsChanged);
            voiceSlider.onValueChanged.RemoveListener(HandleVoiceChanged);
            muteToggle.onValueChanged.RemoveListener(HandleMuteChanged);
            quickCombatToggle.onValueChanged.RemoveListener(HandleQuickCombatChanged);
            quickAnimationToggle.onValueChanged.RemoveListener(HandleQuickAnimationChanged);
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

        private void HandleQuickCombatChanged(bool isOn)
        {
            if (Context == null)
            {
                return;
            }

            Session.IsQuickCombat = isOn;
            PlayerPrefs.SetInt(QUICK_COMBAT_PREFERENCE, isOn ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void HandleQuickAnimationChanged(bool isOn)
        {
            if (Context == null)
            {
                return;
            }

            Session.IsQuickBattleAnimation = isOn;
            PlayerPrefs.SetInt(QUICK_ANIMATION_PREFERENCE, isOn ? 1 : 0);
            PlayerPrefs.Save();
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
