using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Dialogue;
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
        [SerializeField] private DialogueDatabaseScriptableObject dialogueDatabase;
        [SerializeField] private GameHud uiPrefab;
        [Tooltip("World root: cameras, the battlefield and the parent of the mission's adventure map.")]
        [SerializeField] private GameWorld worldPrefab;
        [Tooltip("Development panel (F3); only instantiated in the Editor and development builds.")]
        [SerializeField] private DebugPanelView debugPanelPrefab;
        [Tooltip("Dialogue box overlay with its own Canvas, drawn above the game UI.")]
        [SerializeField] private DialogueDirector dialoguePrefab;

        /// <summary>Used by the scene generator to wire the authored assets.</summary>
        public void Configure(UnitDatabaseScriptableObject unitDatabase, ArtefactDatabaseScriptableObject artefactDatabase,
            HeroDataBaseScriptableObject heroDatabase, AudioDatabaseScriptableObject audio, MissionSettingsScriptableObject missionSettings,
            DialogueDatabaseScriptableObject dialogue, GameHud hudPrefab, GameWorld world, DebugPanelView debugPrefab, DialogueDirector dialogueOverlay)
        {
            units = unitDatabase;
            artefacts = artefactDatabase;
            heroes = heroDatabase;
            audioDatabase = audio;
            mission = missionSettings;
            dialogueDatabase = dialogue;
            uiPrefab = hudPrefab;
            worldPrefab = world;
            debugPanelPrefab = debugPrefab;
            dialoguePrefab = dialogueOverlay;
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

        /// <summary>The conversation each mission site plays when the hero reaches it, by encounter ID.</summary>
        public static IReadOnlyDictionary<string, string> SiteConversations(MissionSettingsScriptableObject missionSettings)
        {
            return missionSettings.Encounters
                .Where(entry => !string.IsNullOrWhiteSpace(entry.ArrivalConversationId))
                .ToDictionary(entry => entry.Id, entry => entry.ArrivalConversationId);
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
            if (uiPrefab == null || worldPrefab == null || dialoguePrefab == null)
            {
                throw new InvalidOperationException("GameUiRoot, World or DialogueOverlay prefab is not configured on GameBootstrap.");
            }

            DefinitionCatalog catalog = ContentLoader.Load(units, artefacts, heroes, audioDatabase, mission);
            string[] arguments = Environment.GetCommandLineArgs();
            bool isSmokeTest = arguments.Contains(SMOKE_TEST_FLAG);
            string saveFile = isSmokeTest ? SMOKE_TEST_CAMPAIGN_FILE : CAMPAIGN_FILE;
            JsonCampaignStore store = new JsonCampaignStore(Path.Combine(UnityEngine.Application.persistentDataPath, saveFile));
            IRandomSource random = new SeededRandomSource(isSmokeTest ? SMOKE_TEST_SEED : Environment.TickCount);
            GameSession session = new GameSession(catalog, store, random, mission.TravelStepSeconds, mission.EnemyTurnSeconds,
                mission.BattleActionSeconds, mission.BattleResultSeconds, mission.EndDaySeconds, Debug.LogException);

            GameAudio audio = gameObject.AddComponent<GameAudio>();
            audio.Initialize(audioDatabase, units, mission, session);

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

            // after the game audio, which initializes the audio system the voice-overs play through
            DialogueDirector dialogue = Instantiate(dialoguePrefab);
            dialogue.Initialize(dialogueDatabase, new AudioSystemDialogueVoice());
            // the automated scenario expects encounter dialogs at once, so it runs without the site conversations
            if (!isSmokeTest)
            {
                gameObject.AddComponent<SiteDialogueCues>().Initialize(session, hud, dialogue, SiteConversations(mission), world.ScreenPoint);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugPanelPrefab != null)
            {
                DebugPanelView debugPanel = Instantiate(debugPanelPrefab, hud.DesignFrame);
                debugPanel.Initialize(session, audio, world.ToggleTerrainOverlay);
                hud.AttachDebugPanel(debugPanel);
                AddDialogueAuditions(debugPanel, dialogue);
            }

            if (isSmokeTest)
            {
                gameObject.AddComponent<RuntimeSmokeRunner>().Initialize(session, store, hud, world);
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // each conversation opens at a random point of the screen, to check the box anywhere
        private void AddDialogueAuditions(DebugPanelView debugPanel, DialogueDirector dialogue)
        {
            foreach (DialogueConversationEntry conversation in dialogueDatabase.Conversations)
            {
                string conversationId = conversation.Id;
                debugPanel.AddAction("Dialogue " + conversationId,
                    () => dialogue.Play(conversationId, new Vector2(UnityEngine.Random.value, UnityEngine.Random.value)));
            }
        }
#endif
    }
}
