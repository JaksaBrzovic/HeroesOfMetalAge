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
        public const string THEME_PATH = "Assets/HeroesOfMetalAge/Content/UI/UiTheme.asset";
        public const string SCENE_PATH = "Assets/Scenes/AshenPass.unity";

        public static UnitDatabaseScriptableObject Units => Load<UnitDatabaseScriptableObject>(DATABASE_FOLDER + "/UnitDatabase.asset");
        public static ArtefactDatabaseScriptableObject Artefacts => Load<ArtefactDatabaseScriptableObject>(DATABASE_FOLDER + "/ArtefactDatabase.asset");
        public static HeroDataBaseScriptableObject Heroes => Load<HeroDataBaseScriptableObject>(DATABASE_FOLDER + "/HeroDatabase.asset");
        public static AudioDatabaseScriptableObject Audio => Load<AudioDatabaseScriptableObject>(DATABASE_FOLDER + "/AudioDatabase.asset");
        public static MissionSettingsScriptableObject Mission => Load<MissionSettingsScriptableObject>(DATABASE_FOLDER + "/AshenPass.asset");
        public static AudioSystemSettings AudioSettings => Load<AudioSystemSettings>(AUDIO_FOLDER + "/GameAudioSettings.asset");
        public static UiTheme Theme => Load<UiTheme>(THEME_PATH);
        public static GameHud RootPrefab => Load<GameHud>(UI_FOLDER + "/GameUiRoot.prefab");
        public static DebugPanelView DebugPanelPrefab => Load<DebugPanelView>(UI_FOLDER + "/Screens/DebugPanel.prefab");

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
