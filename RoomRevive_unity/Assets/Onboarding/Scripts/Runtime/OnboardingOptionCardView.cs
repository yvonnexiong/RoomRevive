using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace RoomRevive.Onboarding
{
    public class OnboardingOptionCardView : MonoBehaviour
    {
        [SerializeField] Image           _ring;
        [SerializeField] Image           _cardBody;
        [SerializeField] Image           _caption;
        [SerializeField] TextMeshProUGUI _label;
        [SerializeField] TextMeshProUGUI _sub;

        public UnityEvent<OnboardingOptionCardView> onClicked = new();

        static readonly Color s_teal    = new Color(0x3A/255f, 0x40/255f, 0x55/255f); // InkPrimary, matches Next button
        static readonly Color s_inkPri  = new Color(0x3A/255f, 0x40/255f, 0x55/255f);
        static readonly Color s_inkSec  = new Color(0x6B/255f, 0x73/255f, 0x88/255f);

        Color     _ringDefault;
        Color     _captionDefault;
        bool      _hovering;
        Coroutine _scaleRoutine;

        void Awake()
        {
            _ringDefault    = _ring.color;
            _captionDefault = _caption.color;

            var btn = _cardBody.GetComponent<Button>();
            if (btn) btn.onClick.AddListener(() => onClicked.Invoke(this));
        }

        public void SetSelected(bool selected)
        {
            _ring.color    = selected ? s_teal       : _ringDefault;
            _caption.color = selected ? s_teal       : _captionDefault;
            _label.color   = (selected ? Color.white : s_inkPri).linear;
            if (_sub != null && _sub.gameObject.activeSelf)
                _sub.color = (selected ? Color.white : s_inkSec).linear;
        }

        public void SetDisabled(bool disabled)
        {
            var cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            cg.alpha          = disabled ? 0.35f : 1f;
            cg.interactable   = !disabled;
            cg.blocksRaycasts = !disabled;
        }

        // ── Hover / press animation (called by OnboardingCardInteractionProxy) ──
        // CellWrapper localScale is animated; XR ray and poke fire the same proxy
        // callbacks via TrackedDeviceGraphicRaycaster — no changes needed when migrating.

        public void OnHoverEnter()
        {
            _hovering = true;
            ScaleTo(1.04f, 0.15f);
        }

        public void OnHoverExit()
        {
            _hovering = false;
            ScaleTo(1.0f, 0.12f);
        }

        public void OnPressDown() => ScaleTo(0.97f, 0.08f);
        public void OnPressUp()   => ScaleTo(_hovering ? 1.04f : 1.0f, 0.10f);

        void ScaleTo(float target, float duration)
        {
            if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
            _scaleRoutine = StartCoroutine(ScaleRoutine(target, duration));
        }

        IEnumerator ScaleRoutine(float target, float duration)
        {
            float start = transform.localScale.x;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float s = Mathf.Lerp(start, target, EaseOut(Mathf.Clamp01(t)));
                transform.localScale = Vector3.one * s;
                yield return null;
            }
            transform.localScale = Vector3.one * target;
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        // Called by the editor build script to wire serialized references
        public void Init(Image ring, Image cardBody, Image caption,
            TextMeshProUGUI label, TextMeshProUGUI sub)
        {
            _ring     = ring;
            _cardBody = cardBody;
            _caption  = caption;
            _label    = label;
            _sub      = sub;
        }
    }
}
