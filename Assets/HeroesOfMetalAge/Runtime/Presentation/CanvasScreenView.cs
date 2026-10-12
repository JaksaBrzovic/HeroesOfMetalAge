using System;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// Base of the prefab-backed screens. A view binds its controls once, displays session state in <see cref="Refresh"/>
    /// and sends commands to the session; it never changes gameplay state itself.
    /// </summary>
    public abstract class CanvasScreenView : MonoBehaviour
    {
        protected CanvasViewContext Context { get; private set; }
        protected GameSession Session => Context.Session;
        protected CampaignState Campaign => Session.Campaign;
        protected DefinitionCatalog Catalog => Session.Catalog;
        protected CampaignRules Rules => Session.Rules;

        /// <exception cref="InvalidOperationException">A serialized control reference is missing on the prefab.</exception>
        public void Initialize(CanvasViewContext context)
        {
            Context = context;
            ValidateReferences();
            BindControls();
        }

        public abstract void Refresh();

        /// <summary>Calls <see cref="Require"/> for every serialized reference the view needs.</summary>
        protected abstract void ValidateReferences();

        protected abstract void BindControls();

        protected void Require(UnityEngine.Object reference, string fieldName)
        {
            if (reference == null)
            {
                throw new InvalidOperationException($"{GetType().Name} on '{name}' is missing '{fieldName}'. Assign it in the screen prefab.");
            }
        }
    }
}
