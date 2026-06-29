using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RoomRevive.Onboarding
{
    // Single-select controller for text-row question pages (Q3 / Q4).
    // Pass nextLabel="See my kitchen" on Q4; leave default for Q3.
    public class OnboardingTextPageController : MonoBehaviour
    {
        [SerializeField] OnboardingTextRowView[] _rows;
        [SerializeField] string[]               _values;
        [SerializeField] string[]               _labels;
        [SerializeField] Button                 _nextButton;
        [SerializeField] CanvasGroup            _nextGroup;
        [SerializeField] CanvasGroup            _backGroup;
        [SerializeField] bool                   _isFirstPage;
        OnboardingTextRowView _selected; // runtime state, not serialized

        public string SelectedValue
        {
            get
            {
                if (_selected == null) return null;
                int i = System.Array.IndexOf(_rows, _selected);
                return i >= 0 ? _values[i] : null;
            }
        }

        public string SelectedLabel
        {
            get
            {
                if (_selected == null || _labels == null || _labels.Length == 0) return SelectedValue;
                int i = System.Array.IndexOf(_rows, _selected);
                return (i >= 0 && i < _labels.Length) ? _labels[i] : SelectedValue;
            }
        }

        public void Setup(OnboardingTextRowView[] rows, string[] values, string[] labels,
            Button nextButton, CanvasGroup nextGroup, CanvasGroup backGroup,
            bool isFirstPage, string nextLabel = "Next")
        {
            _rows        = rows;
            _values      = values;
            _labels      = labels;
            _nextButton  = nextButton;
            _nextGroup   = nextGroup;
            _backGroup   = backGroup;
            _isFirstPage = isFirstPage;

            if (nextButton != null && !string.IsNullOrEmpty(nextLabel))
            {
                var lbl = nextButton.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null) lbl.text = nextLabel;
            }
        }

        void Start()
        {
            foreach (var r in _rows) r.onClicked.AddListener(OnRowClicked);

            if (_isFirstPage && _backGroup != null)
            {
                _backGroup.alpha          = 0f;
                _backGroup.blocksRaycasts = false;
            }

            RefreshNext();
        }

        void OnRowClicked(OnboardingTextRowView row)
        {
            if (_selected != null) _selected.SetSelected(false);
            _selected = row;
            row.SetSelected(true);
            RefreshNext();
        }

        public void ClearSelection()
        {
            if (_selected == null) return;
            _selected.SetSelected(false);
            _selected = null;
            RefreshNext();
        }

        void RefreshNext()
        {
            bool has = _selected != null;
            if (_nextButton) _nextButton.interactable = has;
            if (_nextGroup)  _nextGroup.alpha = has ? 1f : 0.45f;
        }
    }
}
