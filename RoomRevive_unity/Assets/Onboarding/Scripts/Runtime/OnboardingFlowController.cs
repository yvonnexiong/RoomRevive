using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RoomRevive.Onboarding
{
    // Sits on the root OnboardingFlowUI Canvas object.
    // Finds Q1-Q4 + Review panels at Start by name, wires Next/Back clicks, manages transitions.
    public class OnboardingFlowController : MonoBehaviour
    {
        static readonly Dictionary<string, string[]> s_disabledTones = new Dictionary<string, string[]>
        {
            { "natural & scandinavian", new[] { "dark", "bold" } }
        };

        // ReadyUI is a separate World Space canvas — drag its scene instance here.
        [SerializeField] GameObject _readyUI;

        GameObject _q1Panel, _q2Panel, _q3Panel, _q4Panel, _reviewPanel;
        OnboardingQ1Controller        _q1Ctrl;
        OnboardingImagePageController _q2Ctrl;
        OnboardingTextPageController  _q3Ctrl;
        OnboardingTextPageController  _q4Ctrl;
        OnboardingReviewController    _reviewCtrl;
        OnboardingBridge              _bridge;

        int _currentPage;

        void Start()
        {
            _q1Panel     = FindPanel("Q1Panel") ?? FindPanel("Panel");
            _q2Panel     = FindPanel("Q2Panel");
            _q3Panel     = FindPanel("Q3Panel");
            _q4Panel     = FindPanel("Q4Panel");
            _reviewPanel = FindPanel("ReviewPanel");

            _q1Ctrl     = _q1Panel?.GetComponent<OnboardingQ1Controller>();
            _q2Ctrl     = _q2Panel?.GetComponent<OnboardingImagePageController>();
            _q3Ctrl     = _q3Panel?.GetComponent<OnboardingTextPageController>();
            _q4Ctrl     = _q4Panel?.GetComponent<OnboardingTextPageController>();
            _reviewCtrl = _reviewPanel?.GetComponent<OnboardingReviewController>();

            _bridge = GetComponent<OnboardingBridge>();

            if (_reviewCtrl != null)
                _reviewCtrl.onComplete = OnBuildBReady;

            if (_bridge == null)
                Debug.LogWarning("[OnboardingFlow] OnboardingBridge not found — answers JSON will not be written. Add component to OnboardingFlowUI or re-drag the prefab.");

            Debug.Log($"[OnboardingFlow] Panels — " +
                $"Q1:{_q1Panel?.name}({(_q1Ctrl != null ? "ok" : "NO CTRL")}) " +
                $"Q2:{(_q2Panel != null ? "ok" : "MISSING — run Phase 4")} " +
                $"Q3:{(_q3Panel != null ? "ok" : "MISSING — run Phase 5")} " +
                $"Q4:{(_q4Panel != null ? "ok" : "MISSING — run Phase 6")} " +
                $"Review:{(_reviewPanel != null ? "ok" : "MISSING — run Phase 8a")} " +
                $"ReadyUI:{(_readyUI != null ? "ok" : "MISSING — assign in Inspector")}");

            SetActivePage(0);

            AddNextListener(_q1Panel, () => GoToPage(1));
            AddNextListener(_q2Panel, () => GoToPage(2));
            AddNextListener(_q3Panel, () => GoToPage(3));
            AddNextListener(_q4Panel, () => GoToPage(4));
            // ReviewPanel has no buttons — navigation is driven by onComplete at end of animation.

            AddBackListener(_q2Panel, () => GoToPage(0));
            AddBackListener(_q3Panel, () => GoToPage(1));
            AddBackListener(_q4Panel, () => GoToPage(2));
        }

        void GoToPage(int index)
        {
            // Page 5 = ReadyUI — a separate canvas, not a child panel
            if (index == 5)
            {
                SetActivePage(-1); // hide all flow panels
                if (_readyUI != null)
                    _readyUI.SetActive(true);
                else
                    Debug.LogWarning("[OnboardingFlow] ReadyUI not assigned — drag ReadyUI scene instance into the Inspector.");
                Debug.Log("[OnboardingFlow] → ReadyUI");
                return;
            }

            var dest = GetPanel(index);
            if (dest == null)
            {
                Debug.LogWarning($"[OnboardingFlow] Cannot navigate to page {index} — panel not found. " +
                    $"Run Phase {index + 3} to build it.");
                return;
            }
            if (index == 1) ApplyQ2ComboFilter();
            SetActivePage(index);
            if (index == 4 && _reviewCtrl != null)
                _reviewCtrl.Activate(
                    _q1Ctrl?.SelectedLabel ?? "",
                    _q2Ctrl?.SelectedLabel ?? "",
                    _q3Ctrl?.SelectedLabel ?? "",
                    _q4Ctrl?.SelectedLabel ?? "");
            Debug.Log($"[OnboardingFlow] → page {index}");
        }

        void SetActivePage(int index)
        {
            if (_q1Panel)     _q1Panel.SetActive(index == 0);
            if (_q2Panel)     _q2Panel.SetActive(index == 1);
            if (_q3Panel)     _q3Panel.SetActive(index == 2);
            if (_q4Panel)     _q4Panel.SetActive(index == 3);
            if (_reviewPanel) _reviewPanel.SetActive(index == 4);
            _currentPage = index;
        }

        void ApplyQ2ComboFilter()
        {
            if (_q2Ctrl == null) return;
            string style = _q1Ctrl?.SelectedValue ?? "";
            if (s_disabledTones.TryGetValue(style, out var disabled))
                _q2Ctrl.ApplyComboFilter(disabled);
            else
                _q2Ctrl.ClearComboFilter();
        }

        void OnBuildBReady()
        {
            Debug.Log("[OnboardingFlow] Review complete — submitting answers + showing Ready page");
            _bridge?.SubmitAnswers(
                _q1Ctrl?.SelectedValue ?? "",
                _q2Ctrl?.SelectedValue ?? "",
                _q3Ctrl?.SelectedValue ?? "",
                _q4Ctrl?.SelectedValue ?? "");
            GoToPage(5);
        }

        GameObject GetPanel(int index) => index switch
        {
            0 => _q1Panel, 1 => _q2Panel, 2 => _q3Panel,
            3 => _q4Panel, 4 => _reviewPanel,
            _ => null
        };

        GameObject FindPanel(string name) => transform.Find(name)?.gameObject;

        static void AddNextListener(GameObject page, System.Action callback)
        {
            var btn = FindButton(page, "NavBar/NextButton");
            if (btn == null && page != null)
                Debug.LogWarning($"[OnboardingFlow] NextButton not found on {page.name}");
            else if (btn != null)
                btn.onClick.AddListener(() => callback());
        }

        static void AddBackListener(GameObject page, System.Action callback)
        {
            var btn = FindButton(page, "NavBar/BackButton");
            if (btn != null) btn.onClick.AddListener(() => callback());
        }

        static Button FindButton(GameObject page, string path) =>
            page?.transform.Find(path)?.GetComponent<Button>();
    }
}
