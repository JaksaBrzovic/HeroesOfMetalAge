using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Domain;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>A row of army slots instantiated from the slot prefab, one per mission army slot.</summary>
    public sealed class ArmySlotsView : MonoBehaviour
    {
        [SerializeField] private UiWidget slotPrefab;
        [SerializeField] private RectTransform content;

        private readonly List<UiWidget> slots = new List<UiWidget>();
        private CanvasViewContext context;

        public void Initialize(CanvasViewContext viewContext)
        {
            if (slotPrefab == null || content == null)
            {
                throw new InvalidOperationException($"ArmySlotsView on '{name}' is missing its slot prefab or content.");
            }

            context = viewContext;
            for (int i = 0; i < context.Session.Catalog.Mission.ArmySlots; i++)
            {
                slots.Add(Instantiate(slotPrefab, content));
            }
        }

        /// <param name="onSlotClicked">Receives the clicked stack index; null makes the slots read-only.</param>
        public void Display(IReadOnlyList<Troop> army, bool isPlayer, Action<int> onSlotClicked = null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                UiWidget slot = slots[i];
                slot.Unbind();
                if (i >= army.Count)
                {
                    slot.Display(string.Empty, string.Empty, string.Empty, null, Color.white, false);
                    continue;
                }

                Troop troop = army[i];
                string unitName = context.Session.Catalog.Unit(troop.UnitId).Name;
                slot.Display(unitName, string.Empty, troop.Count.ToString(), context.UnitIcon(troop.UnitId), context.UnitTint(troop.UnitId, isPlayer),
                    onSlotClicked != null);
                if (onSlotClicked != null)
                {
                    int index = i;
                    slot.Bind(() => onSlotClicked(index));
                }
            }
        }
    }
}
