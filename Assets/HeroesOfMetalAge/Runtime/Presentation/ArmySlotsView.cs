using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Domain;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// A row of army slots authored in the screen prefab over the slot frames of its artwork, one per mission army slot.
    /// Each slot shows the unit's portrait, its count and, where the slot has a name label, the unit's name.
    /// </summary>
    public sealed class ArmySlotsView : MonoBehaviour
    {
        [SerializeField] private List<UiWidget> slots = new List<UiWidget>();

        private CanvasViewContext context;

        public IReadOnlyList<UiWidget> Slots => slots;

        /// <exception cref="InvalidOperationException">There are fewer slots than the mission's army has.</exception>
        public void Initialize(CanvasViewContext viewContext)
        {
            context = viewContext;
            int needed = context.Session.Catalog.Mission.ArmySlots;
            if (slots.Count < needed || slots.Exists(slot => slot == null))
            {
                throw new InvalidOperationException($"ArmySlotsView on '{name}' needs {needed} assigned slots.");
            }
        }

        /// <param name="onSlotClicked">Receives the clicked stack index; null makes the slots read-only.</param>
        public void Display(IReadOnlyList<Troop> army, bool isPlayer, Action<int> onSlotClicked = null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                UiWidget slot = slots[i];
                slot.Unbind();
                slot.BindInfo(null);
                if (i >= army.Count)
                {
                    slot.Display(string.Empty, string.Empty, string.Empty, null, Color.white, false);
                    continue;
                }

                Troop troop = army[i];
                string unitName = context.Session.Catalog.Unit(troop.UnitId).Name;
                slot.Display(unitName, string.Empty, troop.Count.ToString(), context.UnitCard(troop.UnitId), context.UnitTint(troop.UnitId, isPlayer),
                    onSlotClicked != null);
                if (onSlotClicked != null)
                {
                    int index = i;
                    slot.Bind(() => onSlotClicked(index));
                }

                // a right press shows the stack's info card
                if (context.Info != null)
                {
                    slot.BindInfo(() => context.Info.ShowTroop(troop, isPlayer));
                }
            }
        }
    }
}
