using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Application;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The intro on a black curtain over the whole screen: the stanzas typed letter by letter in the middle, the title
    /// fading in, then the curtain burning away with the title on it (Load Game burns the curtain alone). It only displays the <see cref="FrontEndFlow"/>'s
    /// phases and its <see cref="IntroScript"/>. The curtain's burn material reads how much of it is left from the
    /// Image's alpha, and the Mask beside it cuts the texts out of the burnt holes.
    /// </summary>
    public sealed class IntroView : MonoBehaviour
    {
        [Tooltip("Black Image with the IntroBurn material and a Mask, over the whole screen.")]
        [SerializeField] private Image curtain;
        [Tooltip("Fades a stanza out after its hold.")]
        [SerializeField] private CanvasGroup verses;
        [Tooltip("One text per line, top to bottom; a stanza uses the first as many as it has lines.")]
        [SerializeField] private TMP_Text[] lineTexts = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text titleText;

        private IntroScript script;
        private int shownStanza = -1;

        public Image Curtain => curtain;
        public CanvasGroup Verses => verses;
        public IReadOnlyList<TMP_Text> LineTexts => lineTexts;
        public TMP_Text TitleText => titleText;

        /// <exception cref="InvalidOperationException">A reference is missing on the FrontEnd prefab, or a stanza has more lines than there are texts.</exception>
        public void Initialize(IntroScript introScript)
        {
            if (curtain == null || verses == null || titleText == null || Array.Exists(lineTexts, text => text == null))
            {
                throw new InvalidOperationException($"IntroView on '{name}' has unassigned references. Assign them in the FrontEnd prefab.");
            }

            if (introScript.MaxLines > lineTexts.Length)
            {
                throw new InvalidOperationException($"The intro has a stanza of {introScript.MaxLines} lines, but IntroView on '{name}' has only {lineTexts.Length} line texts. Add texts in the FrontEnd prefab.");
            }

            script = introScript;
            UiBinding.SetText(titleText, script.Title);
            Hide();
        }

        public void Show(FrontEndFlow flow)
        {
            UiBinding.SetVisible(gameObject, true);
            FrontEndPhase phase = flow.Phase;
            bool isVerses = phase == FrontEndPhase.Verses;
            int stanza = isVerses ? script.StanzaAt(flow.Elapsed) : -1;
            ShowStanza(stanza, flow.Elapsed);

            // Load Game burns the plain black, without the intro's title
            bool hasTitle = flow.Choice != FrontEndChoice.LoadGame;
            UiBinding.SetVisible(titleText.gameObject, hasTitle && (phase == FrontEndPhase.Title || phase == FrontEndPhase.Burning));
            SetAlpha(titleText, phase == FrontEndPhase.Title ? script.TitleAlpha(flow.Elapsed) : 1f);
            float left = phase == FrontEndPhase.Burning ? 1f - flow.Progress : 1f;
            Color color = new Color(curtain.color.r, curtain.color.g, curtain.color.b, left);
            if (curtain.color != color)
            {
                curtain.color = color;
            }
        }

        public void Hide()
        {
            UiBinding.SetVisible(gameObject, false);
        }

        private void ShowStanza(int stanza, float time)
        {
            UiBinding.SetVisible(verses.gameObject, stanza >= 0);
            if (stanza < 0)
            {
                shownStanza = -1;
                return;
            }

            IReadOnlyList<IntroLine> lines = script.Stanzas[stanza].Lines;
            if (stanza != shownStanza)
            {
                // the whole stanza is laid out at once, so typing never moves a line
                shownStanza = stanza;
                for (int i = 0; i < lineTexts.Length; i++)
                {
                    bool isUsed = i < lines.Count;
                    UiBinding.SetVisible(lineTexts[i].gameObject, isUsed);
                    if (isUsed)
                    {
                        UiBinding.SetText(lineTexts[i], lines[i].Text);
                    }
                }
            }

            for (int i = 0; i < lines.Count; i++)
            {
                int visible = script.VisibleCharacters(stanza, i, time);
                if (lineTexts[i].maxVisibleCharacters != visible)
                {
                    lineTexts[i].maxVisibleCharacters = visible;
                }
            }

            float alpha = script.StanzaAlpha(stanza, time);
            if (verses.alpha != alpha)
            {
                verses.alpha = alpha;
            }
        }

        private static void SetAlpha(TMP_Text text, float alpha)
        {
            if (text.alpha != alpha)
            {
                text.alpha = alpha;
            }
        }
    }
}
