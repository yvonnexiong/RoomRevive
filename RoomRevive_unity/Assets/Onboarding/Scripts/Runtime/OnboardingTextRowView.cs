using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace RoomRevive.Onboarding
{
    public class OnboardingTextRowView : MonoBehaviour
    {
        [SerializeField] Image           _ring;
        [SerializeField] Image           _rowBody;
        [SerializeField] TextMeshProUGUI _label;
        [SerializeField] TextMeshProUGUI _sub;

        public UnityEvent<OnboardingTextRowView> onClicked = new();

        static readonly Color s_inkPri  = new Color(0x3A/255f, 0x40/255f, 0x55/255f);
        static readonly Color s_inkSec  = new Color(0x6B/255f, 0x73/255f, 0x88/255f);
        static readonly Color s_fill    = new Color(0x3A/255f, 0x40/255f, 0x55/255f); // InkPrimary

        Color     _ringDefault;
        Color     _bodyDefault;
        bool      _hovering;
        Coroutine _scaleRoutine;

        void Awake()
        {
            _ringDefault = _ring.color;
            _bodyDefault = _rowBody.color;
            var btn = _rowBody.GetComponent<Button>();
            if (btn) btn.onClick.AddListener(() => onClicked.Invoke(this));
        }

        public void SetSelected(bool selected)
        {
            _ring.color    = selected ? s_fill : _ringDefault;
            _rowBody.color = selected ? s_fill : _bodyDefault;
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

        // Subtle scale — rows are wide/flat so 1.02 feels right vs 1.04 for square cards
        public void OnHoverEnter() { _hovering = true;  ScaleTo(1.02f, 0.15f); }
        public void OnHoverExit()  { _hovering = false; ScaleTo(1.00f, 0.12f); }
        public void OnPressDown()  => ScaleTo(0.98f, 0.08f);
        public void OnPressUp()    => ScaleTo(_hovering ? 1.02f : 1.00f, 0.10f);

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

        public void Init(Image ring, Image rowBody, TextMeshProUGUI label, TextMeshProUGUI sub)
        {
            _ring    = ring;
            _rowBody = rowBody;
            _label   = label;
            _sub     = sub;
        }
    }
}
