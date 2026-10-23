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
        private const string ADVENTURE_HELP = "CLICK TO PLAN / AGAIN OR SPACE TO RIDE / DRAG TO LOOK / WHEEL TO ZOOM / E END DAY / H HERO";
        private const string BATTLE_HELP = "CLICK A HEX TO MOVE / AN ENEMY TO ATTACK / C SPELLS";
        private const string MENU_HELP = "ESC RETURN / D DEBUG PANEL / M MUTE";

        [SerializeField] private UiTheme theme;
        [SerializeField] private RectTransform designFrame;
        [SerializeField] private EventSystem eventSystem;

        [Header("Chrome")]
        [SerializeField] private NotificationsView notifications;
        [SerializeField] private CanvasGroup contentGroup;
        [SerializeField] private CanvasGroup dialogGroup;
        [Tooltip("Hourglass shown in the middle of the screen while the day ends.")]
        [SerializeField] private EndDayView endDayView;

        [Header("Screens")]
        [SerializeField] private WorldCanvasView worldView;
        [SerializeField] private CombatCanvasView combatView;
        [SerializeField] private TownCanvasView townView;
        [SerializeField] private HeroCanvasView heroView;
        [SerializeField] private DialogCanvasView dialogView;
        [SerializeField] private VictoryCanvasView victoryView;
        [SerializeField] private RewardsCanvasView rewardsView;
        [SerializeField] private DefeatCanvasView defeatView;
        [SerializeField] private SettingsCanvasView settingsView;
        [SerializeField] private LevelUpCanvasView levelUpView;
        [SerializeField] private SpellBookCanvasView spellBookView;
        [SerializeField] private SpellShopCanvasView spellShopView;

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
        private bool isDialogHeld;
        private bool isSuspended;
        private DialogKind shownDialog;
        private GameObject selectionBeforeDialog;
        private GameObject selectionBeforeSettings;
        private readonly System.Random shakeRandom = new System.Random();
        private Vector2 designFrameHome;
        private float shakeLeft;
        private float shakeSeconds;
        private float shakeAmplitude;

        /// <summary>False while an automated scenario drives the session itself.</summary>
        public bool IsDrivingSession { get; set; } = true;
        /// <summary>
        /// True while the main menu or the transition into the mission covers the game: no keyboard commands, no clicks
        /// and the session does not advance.
        /// </summary>
        public bool IsSuspended
        {
            get => isSuspended;
            set
            {
                isSuspended = value;
                Invalidate();
            }
        }
        public bool IsDebugVisible { get; private set; }
        public bool IsSettingsOpen => isSettingsOpen;
        public RectTransform DesignFrame => designFrame;
        public WorldCanvasView WorldView => worldView;
        public TownCanvasView TownView => townView;
        public HeroCanvasView HeroView => heroView;
        public CombatCanvasView CombatView => combatView;
        public DialogCanvasView DialogView => dialogView;
        public VictoryCanvasView VictoryView => victoryView;
        public RewardsCanvasView RewardsView => rewardsView;
        public DefeatCanvasView DefeatView => defeatView;
        public EndDayView EndDayView => endDayView;
        public LevelUpCanvasView LevelUpView => levelUpView;
        public SpellBookCanvasView SpellBookView => spellBookView;
        public SpellShopCanvasView SpellShopView => spellShopView;
        public EventSystem Events => eventSystem;

        private CanvasScreenView[] Screens => new CanvasScreenView[]
        {
            worldView, combatView, townView, heroView, dialogView, victoryView, rewardsView, defeatView, settingsView, levelUpView, spellBookView,
            spellShopView
        };

        /// <summary>The top bar of the screen on display (the world, battle, town or hero screen).</summary>
        public HeaderView ActiveHeader
        {
            get
            {
                if (context == null)
                {
                    return worldView.Header;
                }

                switch (context.Session.Screen)
                {
                    case GameScreen.Battle:
                        return combatView.Header;
                    case GameScreen.Town:
                        return townView.Header;
                    case GameScreen.Hero:
                        return heroView.Header;
                    default:
                        return worldView.Header;
                }
            }
        }
        public CanvasGroup ContentGroup => contentGroup;
        public CanvasViewContext Context => context;

        /// <summary>False under dialogs, options and the development panel; the world takes no clicks then either.</summary>
        public bool IsGameplayInputEnabled => contentGroup.interactable;

        /// <exception cref="InvalidOperationException">A screen or control reference is missing on the root prefab.</exception>
        public void Initialize(GameSession session, IAudioControls audio, PresentationAssets media)
        {
            if (theme == null || designFrame == null || eventSystem == null || notifications == null
                || contentGroup == null || dialogGroup == null || victoryView == null || rewardsView == null || defeatView == null || endDayView == null
                || levelUpView == null || spellBookView == null || spellShopView == null)
            {
                throw new InvalidOperationException($"GameHud on '{name}' has unassigned references. Assign them in the GameUiRoot prefab.");
            }

            context = new CanvasViewContext(session, audio, media, theme);
            designFrameHome = designFrame.anchoredPosition;
            notifications.Initialize();
            endDayView.Initialize();
            settingsView.SetOwner(this);
            foreach (CanvasScreenView view in Screens)
            {
                view.Initialize(context);
                if (view.Header != null)
                {
                    view.Header.Initialize(session.ShowHero, OpenSettings);
                }

                if (view.Footer != null)
                {
                    view.Footer.Initialize();
                }
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

        /// <summary>
        /// Keeps the encounter dialog hidden while something is presented before it, such as a spoken line. The session is
        /// already in the dialog, so gameplay input stays blocked; the dialog appears and takes focus once released.
        /// </summary>
        /// <summary>Shakes the whole UI, strongest at once and calming down over the given seconds.</summary>
        /// <param name="amplitude">Canvas units at the start.</param>
        public void Shake(float seconds, float amplitude)
        {
            shakeSeconds = Mathf.Max(0.01f, seconds);
            shakeLeft = shakeSeconds;
            shakeAmplitude = Mathf.Max(0f, amplitude);
        }

        public void SetDialogHeld(bool isHeld)
        {
            isDialogHeld = isHeld;
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

            if (IsDrivingSession && !isSuspended)
            {
                HandleKeyboard();
            }

            UpdateNotifications();
            UpdateEndDay();
            UpdateShake();
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
            if (Input.GetKeyDown(KeyCode.F3) || Input.GetKeyDown(KeyCode.D))
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

            if (Input.GetKeyDown(KeyCode.C))
            {
                session.OpenSpellBook();
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
            else if (session.TargetingSpellId != null)
            {
                session.CancelSpell();
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

        // the hourglass follows the session's progress every frame while the day ends
        private void UpdateShake()
        {
            if (shakeLeft <= 0f)
            {
                return;
            }

            shakeLeft = Mathf.Max(0f, shakeLeft - Time.unscaledDeltaTime);
            float strength = shakeAmplitude * shakeLeft / shakeSeconds;
            Vector2 offset = new Vector2((float)shakeRandom.NextDouble() * 2f - 1f, (float)shakeRandom.NextDouble() * 2f - 1f) * strength;
            designFrame.anchoredPosition = designFrameHome + offset;
        }

        private void UpdateEndDay()
        {
            GameSession session = context.Session;
            if (session.IsEndingDay)
            {
                endDayView.Show(session.EndDayProgress);
            }
            else
            {
                endDayView.Hide();
            }
        }

        private void RefreshScreens(GameSession session, bool isModal)
        {
            ShowScreen(worldView, session.Screen == GameScreen.Adventure);
            ShowScreen(combatView, session.Screen == GameScreen.Battle);
            ShowScreen(townView, session.Screen == GameScreen.Town);
            ShowScreen(heroView, session.Screen == GameScreen.Hero);
            ShowScreen(dialogView, DialogCanvasView.Shows(session.Dialog) && !isDialogHeld);
            ShowScreen(victoryView, session.Dialog == DialogKind.Victory);
            ShowScreen(rewardsView, session.Dialog == DialogKind.Rewards);
            ShowScreen(defeatView, session.Dialog == DialogKind.Defeat);
            ShowScreen(levelUpView, session.Dialog == DialogKind.LevelUp);
            ShowScreen(spellBookView, session.Dialog == DialogKind.SpellBook);
            ShowScreen(spellShopView, session.Dialog == DialogKind.SpellShop);
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
            bool isContentEnabled = !isModal && !isSettingsOpen && !IsDebugVisible && !isSuspended;
            contentGroup.interactable = isContentEnabled;
            contentGroup.blocksRaycasts = isContentEnabled;
            bool isDialogEnabled = !isSettingsOpen && !IsDebugVisible && !isSuspended;
            dialogGroup.interactable = isDialogEnabled;
            dialogGroup.blocksRaycasts = isDialogEnabled;
        }

        private void RefreshChrome(GameSession session)
        {
            CampaignState campaign = session.Campaign;
            string day = "DAY " + campaign.Day + " / WEEK " + session.Rules.Week(campaign);
            string gold = "GOLD " + campaign.Gold;
            bool canShowHero = session.Screen != GameScreen.Battle && !session.IsBusy;
            foreach (CanvasScreenView view in Screens)
            {
                if (!view.gameObject.activeSelf)
                {
                    continue;
                }

                if (view.Header != null)
                {
                    view.Header.Display(day, gold, canShowHero);
                }

                if (view.Footer != null)
                {
                    view.Footer.Display(HelpText(session.Screen), AudioStateText());
                }
            }

            notifications.SetNotice(session.Notice);
            notifications.SetHit(HitText(session.LastHit));
        }

        private static string HitText(DamageEvent hit)
        {
            if (hit.IsMiss)
            {
                return hit.IsReceivedByPlayer ? "THE ENEMY MISSED" : "YOUR STRIKE MISSED";
            }

            return (hit.IsReceivedByPlayer ? "RECEIVED " : "DEALT ") + hit.Damage + " DAMAGE";
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
            // a held dialog takes focus when it appears
            if (shownDialog != context.Session.Dialog && !isDialogHeld)
            {
                if (isModal)
                {
                    // one modal can follow another (victory, then rewards); remember the control from before the first
                    if (shownDialog == DialogKind.None)
                    {
                        selectionBeforeDialog = eventSystem.currentSelectedGameObject;
                    }

                    eventSystem.SetSelectedGameObject(ModalPrimaryButton().gameObject);
                }
                else
                {
                    eventSystem.SetSelectedGameObject(IsSelectable(selectionBeforeDialog) ? selectionBeforeDialog : ActiveHeader.HeroButton.gameObject);
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
                eventSystem.SetSelectedGameObject(ModalPrimaryButton().gameObject);
            }
            else
            {
                eventSystem.SetSelectedGameObject(IsSelectable(selectionBeforeSettings) ? selectionBeforeSettings : ActiveHeader.OptionsButton.gameObject);
            }

            wasSettingsOpen = isSettingsOpen;
        }

        private UiWidget ModalPrimaryButton()
        {
            switch (context.Session.Dialog)
            {
                case DialogKind.Victory:
                    return victoryView.PrimaryButton;
                case DialogKind.Rewards:
                    return rewardsView.PrimaryButton;
                case DialogKind.Defeat:
                    return defeatView.PrimaryButton;
                case DialogKind.LevelUp:
                    return levelUpView.PrimaryButton;
                case DialogKind.SpellBook:
                    return spellBookView.CloseButton;
                case DialogKind.SpellShop:
                    return spellShopView.CloseButton;
                default:
                    return dialogView.PrimaryButton;
            }
        }

        private static bool IsSelectable(GameObject target)
        {
            return target != null && target.activeInHierarchy;
        }
    }
}
