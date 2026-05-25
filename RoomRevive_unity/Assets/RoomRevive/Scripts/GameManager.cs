using System.Collections.Generic;
using RoomRevive.IntentSelector;
using UnityEngine;

namespace RoomRevive
{
    public enum GamePhase
    {
        Welcome,    // Only WelcomeUI visible
        Alignment,  // AlignmentSphere + AlignmentUI visible
        Browsing,   // browsingObjects list enabled
    }

    /// <summary>
    /// Orchestrates the game flow through Welcome → Alignment → Browsing.
    /// Set <see cref="startPhase"/> in the inspector to jump to a specific phase on play.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Start Phase")]
        [Tooltip("Which phase the game starts in. Use Browsing to skip Welcome and Alignment during iteration.")]
        public GamePhase startPhase = GamePhase.Welcome;

        [Header("Welcome")]
        public GameObject welcomeUI;

        [Header("Alignment")]
        public GameObject alignmentSphere;
        public GameObject alignmentUI;

        [Header("Browsing")]
        [Tooltip("All GameObjects enabled when the Browsing phase starts.")]
        public List<GameObject> browsingObjects = new List<GameObject>();

        [Header("Fridges")]
        [Tooltip("Enabled when the 3rd intent (index 2) is selected.")]
        public GameObject fridgesObject;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable()
        {
            WelcomeUI.OnScanRequested             += OnWelcomeConfirmed;
            AlignmentUIController.OnConfirmed     += OnAlignmentComplete;
            IntentSelectorController.OnIntentSelected += OnIntentSelected;
        }

        void OnDisable()
        {
            WelcomeUI.OnScanRequested             -= OnWelcomeConfirmed;
            AlignmentUIController.OnConfirmed     -= OnAlignmentComplete;
            IntentSelectorController.OnIntentSelected -= OnIntentSelected;
        }

        void Start()
        {
            EnterPhase(startPhase);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                EnterPhase(startPhase);
            };
        }
#endif

        // ── Phase transitions ─────────────────────────────────────────────────

        void EnterPhase(GamePhase phase)
        {
            SetActive(welcomeUI,       phase == GamePhase.Welcome);
            SetActive(alignmentSphere, phase == GamePhase.Alignment);
            SetActive(alignmentUI,     phase == GamePhase.Alignment);

            bool browsing = phase == GamePhase.Browsing;
            foreach (var go in browsingObjects)
                SetActive(go, browsing);
        }

        void OnWelcomeConfirmed()  => EnterPhase(GamePhase.Alignment);
        void OnAlignmentComplete() => EnterPhase(GamePhase.Browsing);

        // ── Intent selection ──────────────────────────────────────────────────

        void OnIntentSelected(int index)
        {
            if (index == 2)
                SetActive(fridgesObject, true);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        static void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }
    }
}
