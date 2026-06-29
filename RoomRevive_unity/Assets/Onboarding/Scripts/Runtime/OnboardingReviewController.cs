using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RoomRevive.Onboarding
{
    // Drives the Preferences Review screen (Phase 8).
    // Call Activate(style, tone, household, budget) from OnboardingFlowController
    // when navigating to this page. onComplete fires when ready to advance to Build B.
    public class OnboardingReviewController : MonoBehaviour
    {
        [SerializeField] Image             _progressFill;
        [SerializeField] TextMeshProUGUI   _titleTmp;
        [SerializeField] TextMeshProUGUI   _dotsTmp;
        [SerializeField] TextMeshProUGUI   _subtitleTmp;
        [SerializeField] CanvasGroup[]     _rowGroups;     // 4 entries, alpha=0 initially
        [SerializeField] TextMeshProUGUI[] _rowValueTmps;  // 4 entries, answer text

        // Fired when animation + bridge are both done. Wire in OnboardingFlowController.
        public Action onComplete;

        bool _animDone;
        bool _coreDone;

        // Called by FlowController when entering the review page.
        public void Activate(string style, string tone, string household, string budget)
        {
            _animDone = false;
            _coreDone = true; // Phase 9 will set false and fire SetBridgeDone() via bridge callback

            string[] values = { style, tone, household, budget };
            for (int i = 0; i < _rowValueTmps.Length && i < values.Length; i++)
                if (_rowValueTmps[i] != null)
                    _rowValueTmps[i].text = Prettify(values[i]);

            // Reset visual state
            if (_progressFill != null) _progressFill.fillAmount = 0f;
            if (_dotsTmp      != null) { _dotsTmp.text = ""; _dotsTmp.gameObject.SetActive(true); }
            if (_subtitleTmp  != null) _subtitleTmp.gameObject.SetActive(false);
            foreach (var cg in _rowGroups) if (cg != null) cg.alpha = 0f;

            StopAllCoroutines();
            StartCoroutine(RunSequence());
        }

        // Phase 9: call this when OnboardingBridge fires onSelectionReceived.
        public void SetBridgeDone() => _coreDone = true;

        IEnumerator RunSequence()
        {
            StartCoroutine(AnimateDots());
            StartCoroutine(FillProgressBar(2.7f));

            yield return new WaitForSeconds(0.6f);

            for (int i = 0; i < _rowGroups.Length; i++)
            {
                if (_rowGroups[i] != null) yield return StartCoroutine(FadeRow(i));
                yield return new WaitForSeconds(0.45f);
            }

            // Banner swap
            if (_titleTmp   != null) _titleTmp.text = "Your preferences";
            if (_dotsTmp    != null) _dotsTmp.gameObject.SetActive(false);
            if (_subtitleTmp != null) _subtitleTmp.gameObject.SetActive(true);

            _animDone = true;
            yield return new WaitUntil(() => _animDone && _coreDone);
            Debug.Log("[OnboardingReview] Complete — transition to Build B");
            onComplete?.Invoke();
        }

        IEnumerator AnimateDots()
        {
            string[] frames = { "", ".", "..", "..." };
            int i = 0;
            while (!_animDone && _dotsTmp != null && _dotsTmp.gameObject.activeInHierarchy)
            {
                _dotsTmp.text = frames[i++ % frames.Length];
                yield return new WaitForSeconds(0.4f);
            }
        }

        IEnumerator FillProgressBar(float duration)
        {
            if (_progressFill == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                _progressFill.fillAmount = Mathf.Clamp01(t / duration);
                yield return null;
            }
            _progressFill.fillAmount = 1f;
        }

        IEnumerator FadeRow(int index)
        {
            var cg = _rowGroups[index];
            if (cg == null) yield break;
            float dur = 0.3f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                cg.alpha = 1f - (1f - p) * (1f - p); // ease-out
                yield return null;
            }
            cg.alpha = 1f;
        }

        // Called from editor build script to wire serialized refs.
        public void Setup(Image progressFill, TextMeshProUGUI titleTmp,
            TextMeshProUGUI dotsTmp, TextMeshProUGUI subtitleTmp,
            CanvasGroup[] rowGroups, TextMeshProUGUI[] rowValueTmps)
        {
            _progressFill = progressFill;
            _titleTmp     = titleTmp;
            _dotsTmp      = dotsTmp;
            _subtitleTmp  = subtitleTmp;
            _rowGroups    = rowGroups;
            _rowValueTmps = rowValueTmps;
        }

        static string Prettify(string value)
        {
            if (string.IsNullOrEmpty(value)) return "—";
            var sb  = new System.Text.StringBuilder();
            bool cap = true;
            foreach (char c in value)
            {
                sb.Append(cap ? char.ToUpper(c) : c);
                cap = (c == ' ');
            }
            return sb.ToString();
        }
    }
}
