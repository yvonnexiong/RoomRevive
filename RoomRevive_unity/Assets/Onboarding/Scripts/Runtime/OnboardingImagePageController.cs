using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RoomRevive.Onboarding
{
    // Generic single-select controller for any image-card question page (Q1/Q2).
    // Supports combo greying: call ApplyComboFilter from the flow controller on page entry.
    public class OnboardingImagePageController : MonoBehaviour
    {
        [SerializeField] OnboardingOptionCardView[] _cards;
        [SerializeField] string[]                   _values;
        [SerializeField] string[]                   _labels;
        [SerializeField] Button                     _nextButton;
        [SerializeField] RectTransform              _nextRT;
        [SerializeField] CanvasGroup                _nextGroup;
        [SerializeField] CanvasGroup                _backGroup;
        [SerializeField] bool                       _isFirstPage;
        OnboardingOptionCardView _selected; // runtime state, not serialized

        public string SelectedValue
        {
            get
            {
                if (_selected == null) return null;
                int i = System.Array.IndexOf(_cards, _selected);
                return i >= 0 ? _values[i] : null;
            }
        }

        public string SelectedLabel
        {
            get
            {
                if (_selected == null || _labels == null || _labels.Length == 0) return SelectedValue;
                int i = System.Array.IndexOf(_cards, _selected);
                return (i >= 0 && i < _labels.Length) ? _labels[i] : SelectedValue;
            }
        }

        public void Setup(OnboardingOptionCardView[] cards, string[] values, string[] labels,
            Button nextButton, RectTransform nextRT,
            CanvasGroup nextGroup, CanvasGroup backGroup, bool isFirstPage)
        {
            _cards       = cards;
            _values      = values;
            _labels      = labels;
            _nextButton  = nextButton;
            _nextRT      = nextRT;
            _nextGroup   = nextGroup;
            _backGroup   = backGroup;
            _isFirstPage = isFirstPage;
        }

        void Start()
        {
            foreach (var c in _cards) c.onClicked.AddListener(OnCardClicked);

            if (_isFirstPage && _backGroup != null)
            {
                _backGroup.alpha          = 0f;
                _backGroup.blocksRaycasts = false;
                if (_nextRT != null) _nextRT.offsetMin = new Vector2(0f, _nextRT.offsetMin.y);
            }

            RefreshNext();
        }

        void OnCardClicked(OnboardingOptionCardView card)
        {
            if (_selected != null) _selected.SetSelected(false);
            _selected = card;
            card.SetSelected(true);
            RefreshNext();
        }

        // Call from the flow controller when entering this page.
        // Cards whose value is in disabledValues are greyed; if the current
        // selection becomes disabled it is cleared.
        public void ApplyComboFilter(IEnumerable<string> disabledValues)
        {
            var disabled = new HashSet<string>(disabledValues);
            for (int i = 0; i < _cards.Length; i++)
            {
                bool dis = disabled.Contains(_values[i]);
                _cards[i].SetDisabled(dis);
                if (dis && _selected == _cards[i])
                {
                    _selected.SetSelected(false);
                    _selected = null;
                    RefreshNext();
                }
            }
        }

        public void ClearComboFilter()
        {
            foreach (var c in _cards) c.SetDisabled(false);
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
