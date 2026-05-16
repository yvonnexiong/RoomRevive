using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RoomRevive.IntentSelector
{
    /// <summary>
    /// Generic data-driven router. Maps any IntentStateData asset to UnityEvents bound in the Inspector.
    /// Adding a new state requires no code changes — drop the SO in the bindings list and wire its UnityEvents.
    /// </summary>
    [DisallowMultipleComponent]
    public class IntentUnityEventRouter : MonoBehaviour
    {
        [Serializable]
        public class IntentStateUnityEventBinding
        {
            public IntentStateData state;
            public UnityEvent onSelected;
            public UnityEvent onConfirmed;
        }

        [Header("Per-state bindings")]
        public List<IntentStateUnityEventBinding> bindings = new List<IntentStateUnityEventBinding>();

        [Header("Wildcard events (fired for ANY state)")]
        public UnityEvent onAnyStateSelected;
        public UnityEvent onAnyStateConfirmed;

        [Header("Debug")]
        public bool debugLogs = false;

        public void RouteSelected(IntentStateData state)
        {
            onAnyStateSelected?.Invoke();

            IntentStateUnityEventBinding binding = FindBinding(state);
            if (binding == null)
            {
                if (debugLogs) Debug.Log($"[IntentUnityEventRouter] No binding for selected state: {SafeName(state)}", this);
                return;
            }

            binding.onSelected?.Invoke();
            if (debugLogs) Debug.Log($"[IntentUnityEventRouter] Routed selected -> {SafeName(state)}", this);
        }

        public void RouteConfirmed(IntentStateData state)
        {
            onAnyStateConfirmed?.Invoke();

            IntentStateUnityEventBinding binding = FindBinding(state);
            if (binding == null) return;

            binding.onConfirmed?.Invoke();
            if (debugLogs) Debug.Log($"[IntentUnityEventRouter] Routed confirmed -> {SafeName(state)}", this);
        }

        IntentStateUnityEventBinding FindBinding(IntentStateData state)
        {
            if (state == null || bindings == null) return null;

            for (int i = 0; i < bindings.Count; i++)
            {
                IntentStateUnityEventBinding b = bindings[i];
                if (b != null && b.state == state) return b;
            }

            return null;
        }

        static string SafeName(IntentStateData s) =>
            s == null ? "<null>" : (!string.IsNullOrEmpty(s.displayName) ? s.displayName : s.name);
    }
}
