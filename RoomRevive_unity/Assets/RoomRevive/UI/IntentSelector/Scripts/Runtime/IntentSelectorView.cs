using System.Collections.Generic;
using UnityEngine;

namespace RoomRevive.IntentSelector
{
    /// <summary>
    /// Owns the cards container and the card template. Instantiates one card per catalog state.
    /// Does not own selection state — that lives on IntentSelectorController.
    /// </summary>
    [DisallowMultipleComponent]
    public class IntentSelectorView : MonoBehaviour
    {
        [Header("Prefab Wiring")]
        [Tooltip("Parent under which card instances are added.")]
        public RectTransform cardsContainer;

        [Tooltip("Card template living in the prefab. Cloned for each catalog state.")]
        public IntentCardView cardTemplate;

        [Header("Behavior")]
        public bool instantiateCardsFromCatalog = true;
        public bool hideTemplateAtRuntime = true;

        [Header("Generated Cards")]
        [Tooltip("Cards built by BuildOrBindCards. Populated at runtime or by the editor binder.")]
        public List<IntentCardView> cardViews = new List<IntentCardView>();

        public void BuildOrBindCards(IntentStateCatalog catalog, IntentSelectorController controller)
        {
            if (catalog == null)
            {
                Debug.LogWarning("[IntentSelectorView] No catalog assigned. Cannot build cards.", this);
                return;
            }

            if (cardsContainer == null || cardTemplate == null)
            {
                Debug.LogWarning("[IntentSelectorView] cardsContainer or cardTemplate not assigned. Cannot build cards.", this);
                return;
            }

            if (instantiateCardsFromCatalog)
            {
                ClearGeneratedCards();

                for (int i = 0; i < catalog.Count; i++)
                {
                    IntentStateData state = catalog.GetState(i);
                    IntentCardView card = InstantiateCard(state, i);
                    if (card == null) continue;
                    card.Bind(state, i, controller);
                    cardViews.Add(card);
                }
            }
            else
            {
                RebindExistingCards(catalog, controller);
            }

            if (hideTemplateAtRuntime && cardTemplate != null && cardTemplate.gameObject != null)
                cardTemplate.gameObject.SetActive(false);
        }

        public void RebindExistingCards(IntentStateCatalog catalog, IntentSelectorController controller)
        {
            if (catalog == null || cardViews == null) return;

            int n = Mathf.Min(cardViews.Count, catalog.Count);
            for (int i = 0; i < n; i++)
            {
                IntentCardView card = cardViews[i];
                if (card == null) continue;
                card.Bind(catalog.GetState(i), i, controller);
            }
        }

        public void RefreshSelection(int selectedIndex, int hoveredIndex, int pressedIndex, IntentSelectorTheme theme, bool instant)
        {
            bool hasSelection = selectedIndex >= 0;

            for (int i = 0; i < cardViews.Count; i++)
            {
                IntentCardView card = cardViews[i];
                if (card == null) continue;

                bool selected = i == selectedIndex;
                bool hovered = i == hoveredIndex;
                bool pressed = i == pressedIndex;

                card.SetVisualState(hasSelection, selected, hovered, pressed, theme, instant);
            }
        }

        public void AnimateCards(float deltaTime, IntentSelectorTheme theme)
        {
            if (theme == null) return;
            for (int i = 0; i < cardViews.Count; i++)
            {
                IntentCardView card = cardViews[i];
                if (card == null) continue;
                card.Animate(deltaTime, theme);
            }
        }

        public void ClearGeneratedCards()
        {
            if (cardViews == null) cardViews = new List<IntentCardView>();

            for (int i = cardViews.Count - 1; i >= 0; i--)
            {
                IntentCardView card = cardViews[i];
                if (card == null) continue;
                if (cardTemplate != null && card == cardTemplate) continue;
                DestroySmart(card.gameObject);
            }

            cardViews.Clear();
        }

        IntentCardView InstantiateCard(IntentStateData state, int index)
        {
            // Per-state prefab wins; CardTemplate is the fallback when a state didn't author its own.
            IntentCardView source = (state != null && state.cardPrefab != null) ? state.cardPrefab : cardTemplate;
            if (source == null) return null;

            GameObject clone = Instantiate(source.gameObject, cardsContainer);
            clone.SetActive(true);
            clone.name = $"Card_{index:00}_{(state != null && !string.IsNullOrEmpty(state.id) ? state.id : "state")}";

            return clone.GetComponent<IntentCardView>();
        }

        static void DestroySmart(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
