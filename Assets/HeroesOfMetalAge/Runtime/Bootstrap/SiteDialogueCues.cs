using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Dialogue;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.Presentation;
using UnityEngine;

namespace HeroesOfMetalAge.Bootstrap
{
    /// <summary>
    /// Plays the conversation authored on a site (<c>Arrival Conversation Id</c> on the mission's encounters) when the hero
    /// reaches it. At a friendly or hostile encounter the encounter dialog waits until the conversation is read; leaving the
    /// encounter (Escape) stops it; a speech balloon rises from the encounter's figure on the map. In the town the garrison
    /// hero speaks from his slot while the clan still holds the town.
    /// Gameplay never waits for it: the session is already in the encounter dialog, only its window is held.
    /// </summary>
    public sealed class SiteDialogueCues : MonoBehaviour
    {
        private readonly Dictionary<string, string> conversationsBySite = new Dictionary<string, string>(StringComparer.Ordinal);
        private GameSession session;
        private GameHud hud;
        private DialogueDirector dialogue;
        private Func<WorldPosition, Vector2> screenPointOfCell;
        // the encounter dialog waiting for its conversation; None when nothing is held
        private DialogKind heldDialog;
        // the town conversation waits one frame for the town screen to be shown and laid out
        private string pendingTownConversationId;

        /// <exception cref="InvalidOperationException">Called twice, or a site names a conversation the dialogue database lacks.</exception>
        /// <param name="cellScreenPoint">The screen pixel of a map cell's centre, for balloons spoken from the map.</param>
        public void Initialize(GameSession gameSession, GameHud gameHud, DialogueDirector director, IReadOnlyDictionary<string, string> siteConversations,
            Func<WorldPosition, Vector2> cellScreenPoint)
        {
            if (session != null)
            {
                throw new InvalidOperationException("Site dialogue cues were initialized twice.");
            }

            foreach (KeyValuePair<string, string> pair in siteConversations)
            {
                if (!director.HasConversation(pair.Value))
                {
                    throw new InvalidOperationException("Encounter '" + pair.Key + "' names dialogue conversation '" + pair.Value
                        + "', which the dialogue database does not have.");
                }

                conversationsBySite.Add(pair.Key, pair.Value);
            }

            session = gameSession;
            hud = gameHud;
            dialogue = director;
            screenPointOfCell = cellScreenPoint;
            session.SiteReached += HandleSiteReached;
            dialogue.Completed += HandleConversationCompleted;
        }

        private void Update()
        {
            if (session == null)
            {
                return;
            }

            // Escape or another command left the encounter while its conversation was still on screen
            if (heldDialog != DialogKind.None && session.Dialog != heldDialog)
            {
                dialogue.Stop();
                Release();
            }

            if (pendingTownConversationId != null)
            {
                PlayPendingTownConversation();
            }
        }

        private void OnDestroy()
        {
            if (session != null)
            {
                session.SiteReached -= HandleSiteReached;
                dialogue.Completed -= HandleConversationCompleted;
            }
        }

        private void HandleSiteReached(EncounterDefinition site)
        {
            if (!conversationsBySite.TryGetValue(site.Id, out string conversationId))
            {
                return;
            }

            if (site.Kind == EncounterKind.Town)
            {
                if (!session.Campaign.IsTownOwned)
                {
                    pendingTownConversationId = conversationId;
                }

                return;
            }

            heldDialog = session.Dialog;
            hud.SetDialogHeld(true);
            if (dialogue.StyleOf(conversationId) == DialogueStyle.SpeechBalloon)
            {
                dialogue.PlayAtScreenPoint(conversationId, screenPointOfCell(site.Position));
            }
            else
            {
                dialogue.Play(conversationId);
            }
        }

        private void HandleConversationCompleted(string conversationId)
        {
            if (heldDialog != DialogKind.None)
            {
                Release();
            }
        }

        private void PlayPendingTownConversation()
        {
            if (session.Screen != GameScreen.Town)
            {
                pendingTownConversationId = null;
                return;
            }

            if (hud.TownView.gameObject.activeInHierarchy)
            {
                dialogue.Play(pendingTownConversationId, hud.TownView.GarrisonHeroAnchor);
                pendingTownConversationId = null;
            }
        }

        private void Release()
        {
            heldDialog = DialogKind.None;
            hud.SetDialogHeld(false);
        }
    }
}
