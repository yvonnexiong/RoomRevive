using System.Collections.Generic;
using UnityEngine;

namespace RoomRevive.IntentSelector
{
    [CreateAssetMenu(menuName = "RoomRevive/Intent Selector/Intent State Catalog", fileName = "IntentStateCatalog")]
    public class IntentStateCatalog : ScriptableObject
    {
        [Tooltip("Ordered list of states. Element order is the visual order in the selector.")]
        public List<IntentStateData> states = new List<IntentStateData>();

        public int Count => states != null ? states.Count : 0;

        public IntentStateData GetState(int index)
        {
            if (states == null) return null;
            if (index < 0 || index >= states.Count) return null;
            return states[index];
        }

        public int IndexOf(IntentStateData state)
        {
            if (states == null || state == null) return -1;
            return states.IndexOf(state);
        }

        public IntentStateData GetDefaultState()
        {
            if (states == null || states.Count == 0) return null;

            for (int i = 0; i < states.Count; i++)
            {
                IntentStateData s = states[i];
                if (s != null && s.startsSelectedByDefault) return s;
            }

            return states[0];
        }

        public int GetDefaultIndex()
        {
            IntentStateData def = GetDefaultState();
            return def != null ? IndexOf(def) : -1;
        }
    }
}
