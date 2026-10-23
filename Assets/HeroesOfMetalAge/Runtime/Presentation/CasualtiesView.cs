using System;
using System.Collections.Generic;
using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The casualties of a battle on the victory and defeat screens, as the classic hero games show them: the creatures each
    /// side lost, one slot per unit type with its count.
    /// </summary>
    public sealed class CasualtiesView : MonoBehaviour
    {
        [SerializeField] private TMP_Text playerTitleText;
        [SerializeField] private ArmySlotsView playerSlots;
        [SerializeField] private TMP_Text enemyTitleText;
        [SerializeField] private ArmySlotsView enemySlots;

        /// <exception cref="InvalidOperationException">A reference is missing on the prefab.</exception>
        public void Initialize(CanvasViewContext context)
        {
            if (playerTitleText == null || playerSlots == null || enemyTitleText == null || enemySlots == null)
            {
                throw new InvalidOperationException($"CasualtiesView on '{name}' has unassigned references. Assign them in the CasualtiesPanel prefab.");
            }

            playerSlots.Initialize(context);
            enemySlots.Initialize(context);
        }

        public void Display(BattleCasualties casualties)
        {
            UiBinding.SetText(playerTitleText, "YOUR LOSSES" + Total(casualties.Player));
            UiBinding.SetText(enemyTitleText, "ENEMY LOSSES" + Total(casualties.Enemy));
            playerSlots.Display(casualties.Player, true);
            enemySlots.Display(casualties.Enemy, false);
        }

        private static string Total(IReadOnlyList<Troop> lost)
        {
            return lost.Count == 0 ? ": NONE" : ": " + lost.Sum(troop => troop.Count);
        }
    }
}
