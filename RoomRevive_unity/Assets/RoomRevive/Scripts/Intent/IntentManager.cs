using System;
using System.Collections;
using UnityEngine;
using GaussianSplatting.Runtime;

namespace RoomRevive
{
    public class IntentManager : MonoBehaviour
    {
        public static IntentManager Instance { get; private set; }

        [SerializeField] private GaussianSplatRenderer splatRenderer;
        [SerializeField] private IntentSO defaultIntent;

        public IntentSO CurrentIntent { get; private set; }
        public bool IsSwitching { get; private set; }
        public event Action<IntentSO> OnIntentChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (defaultIntent != null)
                SetIntent(defaultIntent);
        }

        public void SetIntent(IntentSO intent)
        {
            if (intent == null || intent == CurrentIntent || IsSwitching) return;
            StartCoroutine(SwitchIntent(intent));
        }

        IEnumerator SwitchIntent(IntentSO intent)
        {
            IsSwitching = true;
            Debug.Log($"[IntentManager] Starting switch to: {intent.displayName}");

            splatRenderer.enabled = false;
            yield return null;

            splatRenderer.m_Asset = intent.splatAsset;
            yield return null;

            splatRenderer.enabled = true;

            CurrentIntent = intent;
            IsSwitching = false;
            Debug.Log($"[IntentManager] Switched to: {intent.displayName}");
            OnIntentChanged?.Invoke(intent);
        }
    }
}
