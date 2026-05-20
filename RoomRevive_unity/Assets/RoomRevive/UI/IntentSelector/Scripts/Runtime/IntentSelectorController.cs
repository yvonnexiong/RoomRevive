using System;
using UnityEngine;
using GaussianSplatting.Runtime;

namespace RoomRevive.IntentSelector
{
    /// <summary>
    /// Owns the current selection (index + IntentStateData) and raises events.
    /// Holds no UI hierarchy or scene-side-effect logic — those live in the view and routers.
    /// </summary>
    [DisallowMultipleComponent]
    public class IntentSelectorController : MonoBehaviour
    {
        [Header("Data")]
        public IntentStateCatalog catalog;
        public IntentSelectorTheme theme;

        [Header("View")]
        public IntentSelectorView view;

        [Header("Start Selection")]
        public bool selectStateOnStart = true;
        public bool startFromCatalogDefault = true;
        public int startIndex = 0;

        [Header("Behavior")]
        public bool reselectSameStateInvokesEvents = false;

        [Header("Keyboard Debug")]
        public bool keyboardDebug = true;
        public bool arrowKeysWrapAround = true;
        public KeyCode previousKey = KeyCode.LeftArrow;
        public KeyCode nextKey = KeyCode.RightArrow;
        public KeyCode confirmKey = KeyCode.Return;
        public KeyCode confirmKeyAlt = KeyCode.KeypadEnter;

        [Header("Events")]
        public IntentStateEvent onStateSelected = new IntentStateEvent();
        public IntentStateEvent onStateConfirmed = new IntentStateEvent();
        public IntentIndexEvent onIndexSelected = new IntentIndexEvent();
        public IntentIndexEvent onIndexConfirmed = new IntentIndexEvent();

        /// <summary>Backwards-compatible static event from the legacy IntentCardSelectorUI.</summary>
        public static Action<int> OnIntentSelected;

        /// <summary>Preferred static event for new systems.</summary>
        public static Action<IntentStateData> OnIntentStateSelected;

        [Header("Debug")]
        public bool debugLogs = true;

        int _selectedIndex = -1;
        int _hoveredIndex = -1;
        int _pressedIndex = -1;
        IntentStateData _selectedState;

        public int GetSelectedIndex() => _selectedIndex;
        public IntentStateData GetSelectedState() => _selectedState;
        public int HoveredIndex => _hoveredIndex;
        public int PressedIndex => _pressedIndex;

        void Start()
        {
            if (view != null && catalog != null)
                view.BuildOrBindCards(catalog, this);

            if (!selectStateOnStart) return;

            int idx = ResolveStartIndex();
            if (idx >= 0) SelectIndex(idx, instant: true);
        }

        void Update()
        {
            if (keyboardDebug) HandleKeyboardDebug();
            if (view != null) view.AnimateCards(Time.deltaTime, theme);
        }

        int ResolveStartIndex()
        {
            if (catalog == null || catalog.Count == 0) return -1;

            if (startFromCatalogDefault)
            {
                int def = catalog.GetDefaultIndex();
                if (def >= 0) return def;
            }

            return Mathf.Clamp(startIndex, 0, catalog.Count - 1);
        }

        // ── Public API ──────────────────────────────────────────────────────

        public void SelectIndex(int index) => SelectIndex(index, instant: false);

        public void SelectIndex(int index, bool instant)
        {
            if (catalog == null) return;
            if (index < 0 || index >= catalog.Count) return;

            IntentStateData state = catalog.GetState(index);
            bool changed = index != _selectedIndex;

            _selectedIndex = index;
            _selectedState = state;

            RefreshView(instant);

            if (!changed && !reselectSameStateInvokesEvents) return;

            if (debugLogs)
                Debug.Log($"<color=yellow><b>[IntentSelector]</b></color> Selected {index}: {SafeName(state)}", this);

            onStateSelected?.Invoke(state);
            onIndexSelected?.Invoke(index);
            OnIntentSelected?.Invoke(index);
            OnIntentStateSelected?.Invoke(state);

            if (state != null && state.splatAsset != null)
                SplatManager.Instance?.SetAsset(state.splatAsset);

            if (Application.isPlaying && state != null && state.atmosphereMusic != null)
                AudioManager.Instance?.PlayAtmosphereClip(state.atmosphereMusic);

            if (state != null && state.onSelectedAssetEvent != null)
                state.onSelectedAssetEvent.Invoke();
        }

        public void SelectState(IntentStateData state)
        {
            if (catalog == null || state == null) return;
            int idx = catalog.IndexOf(state);
            if (idx >= 0) SelectIndex(idx);
        }

        public void SelectPrevious()
        {
            if (catalog == null || catalog.Count == 0) return;
            int next = _selectedIndex - 1;
            if (next < 0) next = arrowKeysWrapAround ? catalog.Count - 1 : 0;
            SelectIndex(next);
        }

        public void SelectNext()
        {
            if (catalog == null || catalog.Count == 0) return;
            int next = _selectedIndex + 1;
            if (next >= catalog.Count) next = arrowKeysWrapAround ? 0 : catalog.Count - 1;
            SelectIndex(next);
        }

        public void ConfirmSelection()
        {
            if (_selectedIndex < 0 || _selectedState == null)
            {
                if (debugLogs)
                    Debug.LogWarning("[IntentSelector] Confirm called with no selection.", this);
                return;
            }

            onStateConfirmed?.Invoke(_selectedState);
            onIndexConfirmed?.Invoke(_selectedIndex);

            if (debugLogs)
                Debug.Log($"<color=lime><b>[IntentSelector]</b></color> Confirmed {_selectedIndex}: {SafeName(_selectedState)}", this);
        }

        // ── Hover/Press from cards ──────────────────────────────────────────

        public void NotifyHover(int index, bool hovered)
        {
            if (hovered) _hoveredIndex = index;
            else if (_hoveredIndex == index) _hoveredIndex = -1;
            RefreshView(instant: false);
        }

        public void NotifyPress(int index, bool pressed)
        {
            if (pressed) _pressedIndex = index;
            else if (_pressedIndex == index) _pressedIndex = -1;
            RefreshView(instant: false);
        }

        public void NotifyClick(int index)
        {
            SelectIndex(index);
        }

        // ── Internals ───────────────────────────────────────────────────────

        void RefreshView(bool instant)
        {
            if (view == null) return;
            view.RefreshSelection(_selectedIndex, _hoveredIndex, _pressedIndex, theme, instant);
        }

        void HandleKeyboardDebug()
        {
            if (KeyInput.GetKeyDown(previousKey)) SelectPrevious();
            if (KeyInput.GetKeyDown(nextKey)) SelectNext();
            if (KeyInput.GetKeyDown(confirmKey) || KeyInput.GetKeyDown(confirmKeyAlt)) ConfirmSelection();

            if (KeyInput.GetKeyDown(KeyCode.Alpha1)) SelectIndex(0);
            if (KeyInput.GetKeyDown(KeyCode.Alpha2)) SelectIndex(1);
            if (KeyInput.GetKeyDown(KeyCode.Alpha3)) SelectIndex(2);
            if (KeyInput.GetKeyDown(KeyCode.Alpha4)) SelectIndex(3);
            if (KeyInput.GetKeyDown(KeyCode.Alpha5)) SelectIndex(4);
        }

        static string SafeName(IntentStateData s) =>
            s == null ? "<null>" : (!string.IsNullOrEmpty(s.displayName) ? s.displayName : s.name);
    }
}
