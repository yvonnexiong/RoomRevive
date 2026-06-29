using UnityEngine;
using UnityEngine.UI;

namespace RoomRevive.Onboarding
{
    public class OnboardingQ1Controller : MonoBehaviour
    {
        [SerializeField] OnboardingOptionCardView[] _cards;
        [SerializeField] Button                     _nextButton;
        [SerializeField] RectTransform              _nextRT;
        [SerializeField] CanvasGroup                _nextGroup;
        [SerializeField] CanvasGroup                _backGroup;

        [SerializeField] string[] _values;
        OnboardingOptionCardView  _selected;

        public string SelectedValue
        {
            get
            {
                if (_selected == null || _values == null) return null;
                int i = System.Array.IndexOf(_cards, _selected);
                return i >= 0 ? _values[i] : null;
            }
        }

        void Start()
        {
            foreach (var c in _cards)
                c.onClicked.AddListener(OnCardClicked);

            // Back has no target on Q1 — hide it and expand Next to full width
            if (_backGroup != null)
            {
                _backGroup.alpha          = 0f;
                _backGroup.blocksRaycasts = false;
                if (_nextRT != null)
                    _nextRT.offsetMin = new Vector2(0f, _nextRT.offsetMin.y);
            }

            RefreshNext();
        }

        void OnCardClicked(OnboardingOptionCardView card)
        {
            if (_selected != null) _selected.SetSelected(false);
            _selected = card;
            _selected.SetSelected(true);
            RefreshNext();
        }

        void RefreshNext()
        {
            bool has = _selected != null;
            if (_nextButton) _nextButton.interactable = has;
            if (_nextGroup)  _nextGroup.alpha          = has ? 1f : 0.45f;
        }

        // Called by the editor build script to wire serialized references
        public void Setup(OnboardingOptionCardView[] cards,
            Button nextButton, RectTransform nextRT, CanvasGroup nextGroup, CanvasGroup backGroup,
            string[] values = null)
        {
            _cards      = cards;
            _nextButton = nextButton;
            _nextRT     = nextRT;
            _nextGroup  = nextGroup;
            _backGroup  = backGroup;
            _values     = values;
        }
    }
}
