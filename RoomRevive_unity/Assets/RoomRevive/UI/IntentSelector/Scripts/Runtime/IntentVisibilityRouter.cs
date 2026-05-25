using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomRevive.IntentSelector
{
    /// <summary>
    /// Toggles GameObjects in response to selected state. Supports two complementary modes:
    /// (1) State flags (showProductUI/CabinetUI/Fridges/Cabinets) applied to known scene refs.
    /// (2) Generic per-state bindings: enable/disable arbitrary GameObject lists for arbitrary states.
    /// </summary>
    [DisallowMultipleComponent]
    public class IntentVisibilityRouter : MonoBehaviour
    {
        [Serializable]
        public class IntentVisibilityBinding
        {
            public IntentStateData state;
            public List<GameObject> objectsToEnable = new List<GameObject>();
            public List<GameObject> objectsToDisable = new List<GameObject>();
        }

        [Header("Known scene refs (driven by state flags)")]
        public GameObject productUI;
        public GameObject cabinetUI;
        public GameObject fridgesGO;
        public GameObject cabinetsGO;

        [Header("Generic per-state bindings")]
        public List<IntentVisibilityBinding> bindings = new List<IntentVisibilityBinding>();

        [Header("Modes")]
        public bool applyStateFlags = true;
        public bool applyBindings = true;

        [Header("Debug")]
        public bool debugLogs = false;

        public void RouteSelected(IntentStateData state) => Apply(state);

        public void RouteConfirmed(IntentStateData state)
        {
            // Default: no-op on confirm; selection already drove visibility.
        }

        void Apply(IntentStateData state)
        {
            if (state == null) return;

            if (applyStateFlags)
            {
                SetActive(productUI, state.showProductUI);
                SetActive(cabinetUI, state.showCabinetUI);
                SetActive(fridgesGO, state.showFridges);
                SetActive(cabinetsGO, state.showCabinets);
            }

            if (applyBindings && bindings != null)
            {
                for (int i = 0; i < bindings.Count; i++)
                {
                    IntentVisibilityBinding b = bindings[i];
                    if (b == null || b.state != state) continue;

                    if (b.objectsToEnable != null)
                        for (int j = 0; j < b.objectsToEnable.Count; j++)
                            SetActive(b.objectsToEnable[j], true);

                    if (b.objectsToDisable != null)
                        for (int j = 0; j < b.objectsToDisable.Count; j++)
                            SetActive(b.objectsToDisable[j], false);
                }
            }

            if (debugLogs) Debug.Log($"[IntentVisibilityRouter] Applied visibility for {state.name}", this);
        }

        static void SetActive(GameObject go, bool active)
        {
            if (go == null) return;
            if (go.activeSelf == active) return;
            go.SetActive(active);
        }
    }
}
