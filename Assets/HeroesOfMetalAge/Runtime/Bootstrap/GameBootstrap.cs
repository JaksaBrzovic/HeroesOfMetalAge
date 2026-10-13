using System;
using System.IO;
using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.Infrastructure.Audio;
using HeroesOfMetalAge.Infrastructure.Content;
using HeroesOfMetalAge.Infrastructure.Persistence;
using HeroesOfMetalAge.Presentation;
using UnityEngine;

namespace HeroesOfMetalAge.Bootstrap
{
    /// <summary>
    /// Composition root: validates the authored databases, builds the session, the save store, audio and UI, and wires
    /// them together. A configuration failure is logged with its reason and the game does not start half-wired.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const int TARGET_FRAME_RATE = 60;
        private const string SMOKE_TEST_FLAG = "--smoke-test";
        private const string CAMPAIGN_FILE = "campaign.json";
        // automated runs use their own file so they can never touch the player's campaign
        private const string SMOKE_TEST_CAMPAIGN_FILE = "refactor-smoke-campaign.json";
        private const int SMOKE_TEST_SEED = 42;

        [SerializeField] private UnitDatabaseScriptableObject units;
        [SerializeField] private ArtefactDatabaseScriptableObject artefacts;
        [SerializeField] private HeroDataBaseScriptableObject heroes;
        [SerializeField] private AudioDatabaseScriptableObject audioDatabase;
        [SerializeField] private MissionSettingsScriptableObject mission;
        [SerializeField] private GameHud uiPrefab;
        [Tooltip("World root: cameras, the battlefield and the parent of the mission's adventure map.")]
        [SerializeField] private GameWorld worldPrefab;
        [Tooltip("Development panel (F3); only instantiated in the Editor and development builds.")]
        [SerializeField] private DebugPanelView debugPanelPrefab;

        /// <summary>Used by the scene generator to wire the authored assets.</summary>
        public void Configure(UnitDatabaseScriptableObject unitDatabase, ArtefactDatabaseScriptableObject artefactDatabase,
            HeroDataBaseScriptableObject heroDatabase, AudioDatabaseScriptableObject audio, MissionSettingsScriptableObject missionSettings,
            GameHud hudPrefab, GameWorld world, DebugPanelView debugPrefab)
        {
            units = unitDatabase;
            artefacts = artefactDatabase;
            heroes = heroDatabase;
            audioDatabase = audio;
            mission = missionSettings;
            uiPrefab = hudPrefab;
            worldPrefab = world;
            debugPanelPrefab = debugPrefab;
        }

        /// <summary>Authored portraits, icons, walk cycles and battlefield images by ID for the presentation layer.</summary>
        public static PresentationAssets CreatePresentationAssets(UnitDatabaseScriptableObject unitDatabase, ArtefactDatabaseScriptableObject artefactDatabase,
            HeroDataBaseScriptableObject heroDatabase, MissionSettingsScriptableObject missionSettings)
        {
            return new PresentationAssets(
                unitDatabase.Units.ToDictionary(entry => entry.Id, entry => entry.Portrait),
                heroDatabase.Heroes.ToDictionary(entry => entry.Id, entry => entry.Portrait),
                artefactDatabase.Artefacts.ToDictionary(entry => entry.Id, entry => entry.Icon),
                missionSettings.TownPortrait,
                unitDatabase.Units.ToDictionary(entry => entry.Id, entry => new FigureAnimation(entry.WalkFrames, entry.WalkFramesPerSecond)),
                heroDatabase.Heroes.ToDictionary(entry => entry.Id, entry => new FigureAnimation(entry.MapFrames, entry.MapFramesPerSecond)),
                missionSettings.Encounters.ToDictionary(entry => entry.Id, entry => entry.BattleBackground),
                missionSettings.BattleBackground);
        }

        private void Awake()
        {
            UnityEngine.Application.targetFrameRate = TARGET_FRAME_RATE;
            try
            {
                Compose();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                enabled = false;
            }
        }

        private void Compose()
        {
            if (uiPrefab == null || worldPrefab == null)
            {
                throw new InvalidOperationException("GameUiRoot or World prefab is not configured on GameBootstrap.");
            }

            DefinitionCatalog catalog = ContentLoader.Load(units, artefacts, heroes, audioDatabase, mission);
            string[] arguments = Environment.GetCommandLineArgs();
            bool isSmokeTest = arguments.Contains(SMOKE_TEST_FLAG);
            string saveFile = isSmokeTest ? SMOKE_TEST_CAMPAIGN_FILE : CAMPAIGN_FILE;
            JsonCampaignStore store = new JsonCampaignStore(Path.Combine(UnityEngine.Application.persistentDataPath, saveFile));
            IRandomSource random = new SeededRandomSource(isSmokeTest ? SMOKE_TEST_SEED : Environment.TickCount);
            GameSession session = new GameSession(catalog, store, random, mission.TravelStepSeconds, mission.EnemyTurnSeconds, Debug.LogException);

            GameAudio audio = gameObject.AddComponent<GameAudio>();
            audio.Initialize(audioDatabase, units, session);

            PresentationAssets media = CreatePresentationAssets(units, artefacts, heroes, mission);
            GameHud hud = Instantiate(uiPrefab);
            hud.Initialize(session, audio, media);
            hud.IsDrivingSession = !isSmokeTest;

            AdventureMapView mapPrefab = mission.Map.GetComponent<AdventureMapView>();
            if (mapPrefab == null)
            {
                throw new InvalidOperationException("Map prefab '" + mission.Map.name + "' has no AdventureMapView.");
            }

            GameWorld world = Instantiate(worldPrefab);
            world.Initialize(hud, Instantiate(mapPrefab, world.Maps));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugPanelPrefab != null)
            {
                DebugPanelView debugPanel = Instantiate(debugPanelPrefab, hud.DesignFrame);
                debugPanel.Initialize(session, audio, world.ToggleTerrainOverlay);
                hud.AttachDebugPanel(debugPanel);
            }

            if (isSmokeTest)
            {
                gameObject.AddComponent<RuntimeSmokeRunner>().Initialize(session, store, hud, world);
            }
#endif
        }
    }
}
