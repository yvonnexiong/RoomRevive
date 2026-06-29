using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RoomRevive.Onboarding
{
    // Sits on the root OnboardingFlowUI Canvas object.
    // Finds Q1-Q4 panels by name at Start, wires Next/Back clicks, and manages page transitions.
    public class OnboardingFlowController : MonoBehaviour
    {
        // Style values that restrict palette options on Q2
        static readonly Dictionary<string, string[]> s_disabledTones = new()
        {
            { "natural & scandinavian", new[] { "dark", "bold" } }
        };

        GameObject _q1Panel, _q2Panel, _q3Panel, _q4Panel;
        OnboardingQ1Controller          _q1Ctrl;
        OnboardingImagePageController   _q2Ctrl;
        OnboardingTextPageController    _q3Ctrl;
        OnboardingTextPageController    _q4Ctrl;

        int _currentPage;

        void Start()
        {
            _q1Panel = transform.Find("Q1Panel")?.gameObject;
            _q2Panel = transform.Find("Q2Panel")?.gameObject;
            _q3Panel = transform.Find("Q3Panel")?.gameObject;
            _q4Panel = transform.Find("Q4Panel")?.gameObject;

            _q1Ctrl = _q1Panel?.GetComponent<OnboardingQ1Controller>();
            _q2Ctrl = _q2Panel?.GetComponent<OnboardingImagePageController>();
            _q3Ctrl = _q3Panel?.GetComponent<OnboardingTextPageController>();
            _q4Ctrl = _q4Panel?.GetComponent<OnboardingTextPageController>();

            // Show Q1 only — others were built hidden
            SetActivePage(0);

            // Next buttons advance the flow
            AddNextListener(_q1Panel, () => GoToPage(1));
            AddNextListener(_q2Panel, () => GoToPage(2));
            AddNextListener(_q3Panel, () => GoToPage(3));
            AddNextListener(_q4Panel, OnFlowComplete);

            // Back buttons step back
            AddBackListener(_q2Panel, () => GoToPage(0));
            AddBackListener(_q3Panel, () => GoToPage(1));
            AddBackListener(_q4Panel, () => GoToPage(2));
        }

        void GoToPage(int index)
        {
            // Re-evaluate combo filter every time Q2 is entered so back-nav + style
            // change clears a now-disabled tone selection
            if (index == 1) ApplyQ2ComboFilter();
            SetActivePage(index);
        }

        void SetActivePage(int index)
        {
            if (_q1Panel) _q1Panel.SetActive(index == 0);
            if (_q2Panel) _q2Panel.SetActive(index == 1);
            if (_q3Panel) _q3Panel.SetActive(index == 2);
            if (_q4Panel) _q4Panel.SetActive(index == 3);
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

        // Phase 8 will take over here — for now log the assembled answers
        void OnFlowComplete()
        {
            Debug.Log($"[OnboardingFlow] Complete — " +
                $"style={_q1Ctrl?.SelectedValue} | " +
                $"tone={_q2Ctrl?.SelectedValue} | " +
                $"household={_q3Ctrl?.SelectedValue} | " +
                $"budget={_q4Ctrl?.SelectedValue}");
        }

        static void AddNextListener(GameObject page, System.Action callback)
        {
            var btn = FindButton(page, "NavBar/NextButton");
            if (btn != null) btn.onClick.AddListener(() => callback());
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
