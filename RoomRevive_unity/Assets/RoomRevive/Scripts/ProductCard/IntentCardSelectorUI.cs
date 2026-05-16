using System;
using System.Collections;
using UnityEngine;
using RoomRevive.IntentSelector;

namespace RoomRevive
{
    /// <summary>
    /// Backwards-compatibility wrapper for the legacy IntentCardSelectorUI.
    /// The real selection logic now lives on IntentSelectorController + IntentSelectorView,
    /// driven by IntentStateCatalog data assets.
    ///
    /// Existing scene references and prefab GUIDs to this component continue to work,
    /// and the static OnIntentSelected event continues to fire.
    ///
    /// To migrate fully: replace this component in the scene with the prefab created by
    /// Tools/RoomRevive/Intent Selector/Create Default Assets And Prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public class IntentCardSelectorUI : MonoBehaviour
    {
        [Tooltip("Optional explicit controller. If empty, one is searched in self/children.")]
        public IntentSelectorController controller;

        [Header("Show/Hide Fade")]
        public bool fadeOnShowHide = true;
        public float fadeDuration = 0.25f;

        [Header("Debug")]
        public bool debugLogs = false;

        /// <summary>Legacy static event preserved for existing listeners (e.g. IntentManager).
        /// The new IntentSelectorController also raises this same field.</summary>
        public static Action<int> OnIntentSelected
        {
            get => IntentSelectorController.OnIntentSelected;
            set => IntentSelectorController.OnIntentSelected = value;
        }

        CanvasGroup _cg;
        Coroutine _fade;

        void Awake()
        {
            ResolveController();
            _cg = GetComponent<CanvasGroup>();
        }

        void OnEnable() => ResolveController();

        void ResolveController()
        {
            if (controller != null) return;
            controller = GetComponent<IntentSelectorController>();
            if (controller == null) controller = GetComponentInChildren<IntentSelectorController>(true);

            if (controller == null && debugLogs)
                Debug.LogWarning("[IntentCardSelectorUI] No IntentSelectorController found. Legacy methods will no-op.", this);
        }

        public void SelectCard(int index) => controller?.SelectIndex(index);
        public void SelectPreviousCard() => controller?.SelectPrevious();
        public void SelectNextCard() => controller?.SelectNext();
        public void ConfirmSelection() => controller?.ConfirmSelection();

        public void Show()
        {
            gameObject.SetActive(true);
            StartFade(targetAlpha: 1f);
        }

        public void Hide() => StartFade(targetAlpha: 0f);

        void StartFade(float targetAlpha)
        {
            if (_cg == null) _cg = GetComponent<CanvasGroup>();

            if (_cg == null)
            {
                if (debugLogs) Debug.Log("[IntentCardSelectorUI] No CanvasGroup — skipping fade.", this);
                return;
            }

            if (!fadeOnShowHide || !Application.isPlaying)
            {
                _cg.alpha = targetAlpha;
                _cg.interactable = targetAlpha > 0.5f;
                _cg.blocksRaycasts = targetAlpha > 0.5f;
                return;
            }

            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(targetAlpha));
        }

        IEnumerator FadeRoutine(float targetAlpha)
        {
            float startAlpha = _cg.alpha;
            float t = 0f;

            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                _cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, fadeDuration <= 0f ? 1f : t / fadeDuration);
                yield return null;
            }

            _cg.alpha = targetAlpha;
            _cg.interactable = targetAlpha > 0.5f;
            _cg.blocksRaycasts = targetAlpha > 0.5f;
            _fade = null;
        }

        /// <summary>
        /// Compatibility shim. Existing scenes (MainScene, UIScene) have GameObjects whose
        /// MonoBehaviours point at the nested type "RoomRevive.IntentCardSelectorUI/IntentCardPointerProxy".
        /// We preserve the nested type so those scene components do not become Missing Script,
        /// and we forward pointer events to the new IntentSelector pointer proxy if one is found
        /// on the same GameObject. New work should use RoomRevive.IntentSelector.IntentCardPointerProxy.
        /// </summary>
        [DisallowMultipleComponent]
        public class IntentCardPointerProxy : MonoBehaviour,
            UnityEngine.EventSystems.IPointerEnterHandler,
            UnityEngine.EventSystems.IPointerExitHandler,
            UnityEngine.EventSystems.IPointerDownHandler,
            UnityEngine.EventSystems.IPointerUpHandler,
            UnityEngine.EventSystems.IPointerClickHandler
        {
            [Tooltip("If assigned, pointer events forward to this new-system card view. Otherwise this shim is inert.")]
            public IntentSelector.IntentCardView card;

            void Reset()
            {
                if (card == null) card = GetComponent<IntentSelector.IntentCardView>();
                if (card == null) card = GetComponentInParent<IntentSelector.IntentCardView>();
            }

            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) => card?.NotifyPointerEnter();
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => card?.NotifyPointerExit();
            public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) => card?.NotifyPointerDown();
            public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => card?.NotifyPointerUp();
            public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e) => card?.NotifyPointerClick();
        }
    }
}
