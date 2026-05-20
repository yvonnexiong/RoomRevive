using System;
using UnityEngine;
using GaussianSplatting.Runtime;
using RoomRevive.IntentSelector;

namespace RoomRevive
{
    public class IntentManager : MonoBehaviour
    {
        public static IntentManager Instance { get; private set; }

        [SerializeField] private GaussianSplatRenderer splatRenderer;
        [SerializeField] private IntentSO defaultIntent;
        [SerializeField] private GameObject hotspotsRoot;

        public IntentSO CurrentIntent { get; private set; }
        public event Action<IntentSO> OnIntentChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            hotspotsRoot?.SetActive(false);
            IntentSelectorController.OnIntentStateSelected += OnIntentStateSelected;
        }

        void OnDestroy()
        {
            IntentSelectorController.OnIntentStateSelected -= OnIntentStateSelected;
        }

        void OnIntentStateSelected(IntentStateData state) { }

        [SerializeField] private bool autoInitOnStart = false;

        void Start()
        {
            if (autoInitOnStart && defaultIntent != null)
                SetIntent(defaultIntent);
        }

        // Called by StartupController after alignment is confirmed
        public void Initialize()
        {
            if (defaultIntent != null)
                SetIntent(defaultIntent);
        }

        public void SetIntent(IntentSO intent)
        {
            if (intent == null || intent == CurrentIntent) return;
            if (splatRenderer == null) { Debug.LogError("[IntentManager] No GaussianSplatRenderer assigned."); return; }

            splatRenderer.m_Asset = intent.splatAsset;
            CurrentIntent = intent;
            OnIntentChanged?.Invoke(intent);

            if (hotspotsRoot != null && !hotspotsRoot.activeSelf)
                hotspotsRoot.SetActive(true);
        }
    }
}
