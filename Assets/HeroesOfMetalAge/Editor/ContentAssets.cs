using HeroesOfMetalAge.Dialogue;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.Infrastructure.Content;
using HeroesOfMetalAge.Presentation;
using MLin.Audio;
using UnityEditor;

namespace HeroesOfMetalAge.EditorTools
{
    /// <summary>Locations of the authored assets the scene generator, tests and tooling work with.</summary>
    public static class ContentAssets
    {
        public const string DATABASE_FOLDER = "Assets/HeroesOfMetalAge/Content/Databases";
        public const string AUDIO_FOLDER = "Assets/HeroesOfMetalAge/Content/Audio";
        public const string UI_FOLDER = "Assets/HeroesOfMetalAge/Prefabs/UI";
        public const string WORLD_FOLDER = "Assets/HeroesOfMetalAge/Prefabs/World";
        public const string MAPS_FOLDER = "Assets/HeroesOfMetalAge/Content/Maps";
        public const string THEME_PATH = "Assets/HeroesOfMetalAge/Content/UI/UiTheme.asset";
        public const string SCENE_PATH = "Assets/Scenes/AshenPass.unity";
        public const string DIALOGUE_DATABASE_PATH = DATABASE_FOLDER + "/DialogueDatabase.asset";
        public const string DIALOGUE_BOX_PATH = UI_FOLDER + "/Widgets/DialogueBox.prefab";
        public const string DIALOGUE_OVERLAY_PATH = UI_FOLDER + "/DialogueOverlay.prefab";
        public const string DIALOGUE_BALLOON_PATH = UI_FOLDER + "/Widgets/DialogueBalloon.prefab";
        public const string END_DAY_OVERLAY_PATH = UI_FOLDER + "/Screens/EndDayOverlay.prefab";
        public const string FRONT_END_PATH = UI_FOLDER + "/FrontEnd.prefab";
        public const string FRONT_END_SETTINGS_PATH = DATABASE_FOLDER + "/FrontEnd.asset";

        public static UnitDatabaseScriptableObject Units => Load<UnitDatabaseScriptableObject>(DATABASE_FOLDER + "/UnitDatabase.asset");
        public static ArtefactDatabaseScriptableObject Artefacts => Load<ArtefactDatabaseScriptableObject>(DATABASE_FOLDER + "/ArtefactDatabase.asset");
        public static HeroDataBaseScriptableObject Heroes => Load<HeroDataBaseScriptableObject>(DATABASE_FOLDER + "/HeroDatabase.asset");
        public static AudioDatabaseScriptableObject Audio => Load<AudioDatabaseScriptableObject>(DATABASE_FOLDER + "/AudioDatabase.asset");
        public static MissionSettingsScriptableObject Mission => Load<MissionSettingsScriptableObject>(DATABASE_FOLDER + "/AshenPass.asset");
        public static FrontEndSettingsScriptableObject FrontEnd => Load<FrontEndSettingsScriptableObject>(FRONT_END_SETTINGS_PATH);
        public static DialogueDatabaseScriptableObject Dialogue => Load<DialogueDatabaseScriptableObject>(DIALOGUE_DATABASE_PATH);
        public static AudioSystemSettings AudioSettings => Load<AudioSystemSettings>(AUDIO_FOLDER + "/GameAudioSettings.asset");
        public static UiTheme Theme => Load<UiTheme>(THEME_PATH);
        public static GameHud RootPrefab => Load<GameHud>(UI_FOLDER + "/GameUiRoot.prefab");
        public static DebugPanelView DebugPanelPrefab => Load<DebugPanelView>(UI_FOLDER + "/Screens/DebugPanel.prefab");
        public static DialogueBoxView DialogueBoxPrefab => Load<DialogueBoxView>(DIALOGUE_BOX_PATH);
        public static DialogueBalloonView DialogueBalloonPrefab => Load<DialogueBalloonView>(DIALOGUE_BALLOON_PATH);
        public static DialogueDirector DialogueOverlayPrefab => Load<DialogueDirector>(DIALOGUE_OVERLAY_PATH);
        public static EndDayView EndDayOverlayPrefab => Load<EndDayView>(END_DAY_OVERLAY_PATH);
        public static FrontEndView FrontEndPrefab => Load<FrontEndView>(FRONT_END_PATH);
        public static GameWorld WorldPrefab => Load<GameWorld>(WORLD_FOLDER + "/World.prefab");
        public static BattlefieldView BattlefieldPrefab => Load<BattlefieldView>(WORLD_FOLDER + "/Battlefield.prefab");
        public static WorldMarker MarkerPrefab => Load<WorldMarker>(WORLD_FOLDER + "/WorldMarker.prefab");

        /// <summary>The validated gameplay catalog built from the authored databases.</summary>
        public static DefinitionCatalog LoadCatalog()
        {
            return ContentLoader.Load(Units, Artefacts, Heroes, Audio, Mission);
        }

        private static T Load<T>(string path)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
