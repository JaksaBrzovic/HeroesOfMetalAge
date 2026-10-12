using System;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// Root of the prefab-backed UI: routes keyboard input to session commands, shows the screen for the session state,
    /// blocks input under modal overlays and restores keyboard focus. It redraws only after the session changed.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        private const float NOTICE_SECONDS = 5f;
        private const float HIT_SECONDS = 1.5f;
        private const string ADVENTURE_HELP = "CLICK TO PLAN / AGAIN OR SPACE TO RIDE / ARROWS OR DRAG TO LOOK AROUND / E END DAY / H HERO";
        private const string BATTLE_HELP = "CLICK A BLUE HEX TO MOVE / CLICK AN ENEMY TO ATTACK / ARCHERS CAN SHOOT AT RANGE";
        private const string MENU_HELP = "ESC RETURN / F3 DEVELOPMENT PANEL / M MUTE";

        [SerializeField] private UiTheme theme;
        [SerializeField] private RectTransform designFrame;
        [SerializeField] private EventSystem eventSystem;

        [Header("Chrome")]
        [SerializeField] private HeaderView header;
        [SerializeField] private FooterView footer;
        [SerializeField] private NotificationsView notifications;
        [SerializeField] private CanvasGroup contentGroup;
        [SerializeField] private CanvasGroup dialogGroup;

        [Header("Screens")]
        [SerializeField] private WorldCanvasView worldView;
        [SerializeField] private CombatCanvasView combatView;
        [SerializeField] private TownCanvasView townView;
        [SerializeField] private HeroCanvasView heroView;
        [SerializeField] private DialogCanvasView dialogView;
        [SerializeField] private SettingsCanvasView settingsView;

        private CanvasViewContext context;
        private DebugPanelView debugPanel;
        private long shownRevision = -1;
        private int shownNoticeRevision;
        private int shownDamageRevision;
        private float noticeUntil;
        private float hitUntil;
        private bool isDirty = true;
        private bool isSettingsOpen;
        private bool wasSettingsOpen;
        private DialogKind shownDialog;
        private GameObject selectionBeforeDialog;
        private GameObject selectionBeforeSettings;

        /// <summary>False while an automated scenario drives the session itself.</summary>
        public bool IsDrivingSession { get; set; } = true;
        public bool IsDebugVisible { get; private set; }
        public bool IsSettingsOpen => isSettingsOpen;
        public RectTransform DesignFrame => designFrame;
        public WorldCanvasView WorldView => worldView;
        public TownCanvasView TownView => townView;
        public HeroCanvasView HeroView => heroView;
        public CombatCanvasView CombatView => combatView;
        public DialogCanvasView DialogView => dialogView;
        public EventSystem Events => eventSystem;
        public CanvasGroup ContentGroup => contentGroup;
        public CanvasViewContext Context => context;

        /// <summary>False under dialogs, options and the development panel; the world takes no clicks then either.</summary>
        public bool IsGameplayInputEnabled => contentGroup.interactable;

        /// <exception cref="InvalidOperationException">A screen or control reference is missing on the root prefab.</exception>
        public void Initialize(GameSession session, IAudioControls audio, PresentationAssets media)
        {
            if (theme == null || designFrame == null || eventSystem == null || header == null || footer == null || notifications == null
                || contentGroup == null || dialogGroup == null)
            {
                throw new InvalidOperationException($"GameHud on '{name}' has unassigned references. Assign them in the GameUiRoot prefab.");
            }

            context = new CanvasViewContext(session, audio, media, theme);
            header.Initialize(session.ShowHero, OpenSettings);
            footer.Initialize();
            notifications.Initialize();
            settingsView.SetOwner(this);
            foreach (CanvasScreenView view in new CanvasScreenView[] { worldView, combatView, townView, heroView, dialogView, settingsView })
            {
                view.Initialize(context);
            }

            RefreshNow();
        }

        /// <summary>Adds the development panel (instantiated by the bootstrap in development builds) to F3.</summary>
        public void AttachDebugPanel(DebugPanelView panel)
        {
            debugPanel = panel;
            debugPanel.gameObject.SetActive(IsDebugVisible);
        }

        public void Invalidate()
        {
            isDirty = true;
        }

        /// <summary>Shows or hides the development panel (F3); gameplay input pauses while it is open.</summary>
        public void SetDebugVisible(bool isVisible)
        {
            IsDebugVisible = isVisible;
            Invalidate();
        }

        public void OpenSettings()
        {
            if (!isSettingsOpen)
            {
                selectionBeforeSettings = eventSystem.currentSelectedGameObject;
            }

            isSettingsOpen = true;
            Invalidate();
        }

        public void CloseSettings()
        {
            isSettingsOpen = false;
            context.Audio.SavePreferences();
            Invalidate();
        }

        public void RefreshNow()
        {
            if (context == null)
            {
                return;
            }

            GameSession session = context.Session;
            bool isModal = session.Dialog != DialogKind.None;
            RefreshScreens(session, isModal);
            RefreshInputBlocking(isModal);
            RefreshChrome(session);
            RestoreSelection(isModal);
            shownRevision = session.Revision;
            isDirty = false;
        }

        private void Update()
        {
            if (context == null)
            {
                return;
            }

            if (IsDrivingSession)
            {
                HandleKeyboard();
            }

            UpdateNotifications();
        }

        private void LateUpdate()
        {
            if (context != null && (isDirty || shownRevision != context.Session.Revision))
            {
                RefreshNow();
            }
        }

        private void HandleKeyboard()
        {
            GameSession session = context.Session;
            if (Input.GetKeyDown(KeyCode.F3))
            {
                SetDebugVisible(!IsDebugVisible);
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                context.Audio.IsMuted = !context.Audio.IsMuted;
                Invalidate();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscape(session);
            }

            if (isSettingsOpen || IsDebugVisible)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                session.Travel();
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                session.EndDay();
            }

            if (Input.GetKeyDown(KeyCode.H))
            {
                session.ShowHero();
            }

            session.Tick(Time.deltaTime);
        }

        /// <summary>Escape closes the innermost layer: options, the debug panel, a dialog or overlay screen, else opens options.</summary>
        private void HandleEscape(GameSession session)
        {
            if (isSettingsOpen)
            {
                CloseSettings();
            }
            else if (IsDebugVisible)
            {
                SetDebugVisible(false);
            }
            else if (session.Dialog != DialogKind.None || session.Screen == GameScreen.Hero || session.Screen == GameScreen.Town)
            {
                session.Back();
            }
            else
            {
                OpenSettings();
            }
        }

        private void UpdateNotifications()
        {
            GameSession session = context.Session;
            if (shownNoticeRevision != session.NoticeRevision)
            {
                shownNoticeRevision = session.NoticeRevision;
                noticeUntil = Time.unscaledTime + NOTICE_SECONDS;
                Invalidate();
            }

            if (shownDamageRevision != session.DamageRevision)
            {
                shownDamageRevision = session.DamageRevision;
                hitUntil = Time.unscaledTime + HIT_SECONDS;
                Invalidate();
            }

            bool isNoticeVisible = Time.unscaledTime < noticeUntil;
            bool isHitVisible = Time.unscaledTime < hitUntil && session.Screen == GameScreen.Battle && session.Dialog == DialogKind.None;
            notifications.SetVisibility(isNoticeVisible, isHitVisible);
        }

        private void RefreshScreens(GameSession session, bool isModal)
        {
            ShowScreen(worldView, session.Screen == GameScreen.Adventure);
            ShowScreen(combatView, session.Screen == GameScreen.Battle);
            ShowScreen(townView, session.Screen == GameScreen.Town);
            ShowScreen(heroView, session.Screen == GameScreen.Hero);
            ShowScreen(dialogView, isModal);
            ShowScreen(settingsView, isSettingsOpen);
            if (debugPanel != null)
            {
                UiBinding.SetVisible(debugPanel.gameObject, IsDebugVisible);
                if (IsDebugVisible)
                {
                    debugPanel.Refresh();
                }
            }
        }

        private static void ShowScreen(CanvasScreenView view, bool isVisible)
        {
            UiBinding.SetVisible(view.gameObject, isVisible);
            if (isVisible)
            {
                view.Refresh();
            }
        }

        // a hidden overlay is not enough: the layers below must also stop taking clicks
        private void RefreshInputBlocking(bool isModal)
        {
            bool isContentEnabled = !isModal && !isSettingsOpen && !IsDebugVisible;
            contentGroup.interactable = isContentEnabled;
            contentGroup.blocksRaycasts = isContentEnabled;
            bool isDialogEnabled = !isSettingsOpen && !IsDebugVisible;
            dialogGroup.interactable = isDialogEnabled;
            dialogGroup.blocksRaycasts = isDialogEnabled;
        }

        private void RefreshChrome(GameSession session)
        {
            CampaignState campaign = session.Campaign;
            MissionDefinition mission = session.Catalog.Mission;
            int week = (campaign.Day - 1) / mission.DaysPerWeek + 1;
            header.Display(mission.Name + " / A SOUND-DRIVEN STRATEGY PROTOTYPE", "DAY " + campaign.Day + " / WEEK " + week, "GOLD " + campaign.Gold,
                session.Screen != GameScreen.Battle && !session.IsBusy);
            footer.Display(HelpText(session.Screen), AudioStateText());
            notifications.SetNotice(session.Notice);
            notifications.SetHit((session.LastHit.IsReceivedByPlayer ? "RECEIVED " : "DEALT ") + session.LastHit.Damage + " DAMAGE");
        }

        private string AudioStateText()
        {
            IAudioControls audio = context.Audio;
            if (audio.IsMuted)
            {
                return "AUDIO MUTED";
            }

            return "AUDIO / " + (audio.IsFighting ? "BATTLE" : audio.IsRiding ? "GALLOP" : "ASHEN AMBIENT");
        }

        private static string HelpText(GameScreen screen)
        {
            switch (screen)
            {
                case GameScreen.Adventure:
                    return ADVENTURE_HELP;
                case GameScreen.Battle:
                    return BATTLE_HELP;
                default:
                    return MENU_HELP;
            }
        }

        /// <summary>Moves keyboard focus into an opening dialog or options and back to the previous control when they close.</summary>
        private void RestoreSelection(bool isModal)
        {
            if (shownDialog != context.Session.Dialog)
            {
                if (isModal)
                {
                    selectionBeforeDialog = eventSystem.currentSelectedGameObject;
                    eventSystem.SetSelectedGameObject(dialogView.PrimaryButton.gameObject);
                }
                else
                {
                    eventSystem.SetSelectedGameObject(IsSelectable(selectionBeforeDialog) ? selectionBeforeDialog : header.HeroButton.gameObject);
                }

                shownDialog = context.Session.Dialog;
            }

            if (wasSettingsOpen == isSettingsOpen)
            {
                return;
            }

            if (isSettingsOpen)
            {
                eventSystem.SetSelectedGameObject(settingsView.BackButton.gameObject);
            }
            else if (isModal)
            {
                eventSystem.SetSelectedGameObject(dialogView.PrimaryButton.gameObject);
            }
            else
            {
                eventSystem.SetSelectedGameObject(IsSelectable(selectionBeforeSettings) ? selectionBeforeSettings : header.OptionsButton.gameObject);
            }

            wasSettingsOpen = isSettingsOpen;
        }

        private static bool IsSelectable(GameObject target)
        {
            return target != null && target.activeInHierarchy;
        }
    }
}
