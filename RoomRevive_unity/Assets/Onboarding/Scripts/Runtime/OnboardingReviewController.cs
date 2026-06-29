using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RoomRevive.Onboarding
{
    // Drives the Review page ("Personalizing your dream kitchen").
    // Sequence (matches HTML timing):
    //   t=0.0  progress bar starts filling (2.9s)
    //   t=0.5  row 1 fades up (0.45s); t=0.82 checkmark 1 pops (0.4s)
    //   t=1.0  row 2 fades up;         t=1.32 checkmark 2 pops
    //   t=1.5  row 3 fades up;         t=1.82 checkmark 3 pops
    //   t=2.0  row 4 fades up;         t=2.32 checkmark 4 pops
    //   t=2.7  banner swaps → "Your preferences" / "Got it — finding your kitchen"
    //   t=3.8  onComplete fires → FlowController navigates to ReadyUI
    public class OnboardingReviewController : MonoBehaviour
    {
        [SerializeField] RectTransform      _progressFillRT;  // anchorMax.x animated 0→1
        [SerializeField] TextMeshProUGUI   _titleTmp;
        [SerializeField] TextMeshProUGUI   _subtitleTmp;
        [SerializeField] CanvasGroup[]     _rowGroups;       // alpha fade, one per row
        [SerializeField] CanvasGroup[]     _checkGroups;     // alpha fade, one per checkmark
        [SerializeField] Transform[]       _checkTransforms; // scale pop, one per checkmark
        [SerializeField] TextMeshProUGUI[] _rowValueTmps;    // bound answer text

        public Action onComplete;

        public void Activate(string style, string tone, string household, string budget)
        {
            string[] values = { style, tone, household, budget };
            for (int i = 0; i < _rowValueTmps.Length && i < values.Length; i++)
                if (_rowValueTmps[i] != null)
                    _rowValueTmps[i].text = values[i];

            StopAllCoroutines();
            StartCoroutine(RunSequence());
        }

        public void Setup(RectTransform progressFillRT, TextMeshProUGUI titleTmp, TextMeshProUGUI subtitleTmp,
            CanvasGroup[] rowGroups,
            CanvasGroup[] checkGroups, Transform[] checkTransforms,
            TextMeshProUGUI[] rowValueTmps)
        {
            _progressFillRT  = progressFillRT;
            _titleTmp        = titleTmp;
            _subtitleTmp     = subtitleTmp;
            _rowGroups       = rowGroups;
            _checkGroups     = checkGroups;
            _checkTransforms = checkTransforms;
            _rowValueTmps    = rowValueTmps;
        }

        IEnumerator RunSequence()
        {
            // Reset visual state
            if (_progressFillRT) _progressFillRT.anchorMax = new Vector2(0f, 1f);
            if (_titleTmp)       _titleTmp.text    = "Personalizing your dream kitchen";
            if (_subtitleTmp)    _subtitleTmp.text = "Matching your choices to the perfect pieces";

            for (int i = 0; i < 4; i++)
            {
                if (i < _rowGroups.Length       && _rowGroups[i])       _rowGroups[i].alpha           = 0f;
                if (i < _checkGroups.Length     && _checkGroups[i])     _checkGroups[i].alpha         = 0f;
                if (i < _checkTransforms.Length && _checkTransforms[i]) _checkTransforms[i].localScale = Vector3.one * 0.4f;
            }

            yield return null; // one frame for CanvasGroup resets to propagate

            // Subtitle dots cycle while rows animate ("Matching your choices… • • •")
            var dotsRoutine = StartCoroutine(CycleSubtitleDots());

            // Progress bar fills immediately, parallel to everything else
            StartCoroutine(FillProgressBar(2.9f));

            // Brief pause before first row (matches HTML's ~500ms lead-in)
            yield return new WaitForSeconds(0.5f);

            // Rows stagger 0.5s apart; checkmark fires 0.32s after each row starts
            for (int i = 0; i < 4; i++)
            {
                StartCoroutine(AnimateRow(i));
                StartCoroutine(DelayedPop(i, 0.32f));
                if (i < 3) yield return new WaitForSeconds(0.5f);
            }

            // Row 4 started at t=2.0s; wait to reach t=2.7s
            yield return new WaitForSeconds(0.7f);
            StopCoroutine(dotsRoutine);
            if (_titleTmp)    _titleTmp.text    = "Your preferences";
            if (_subtitleTmp) _subtitleTmp.text = "Got it — finding your kitchen";

            // Wait to reach t=3.8s then advance
            yield return new WaitForSeconds(1.1f);
            onComplete?.Invoke();
        }

        IEnumerator CycleSubtitleDots()
        {
            const string base_ = "Matching your choices to the perfect pieces";
            string[] states = { base_ + "  •", base_ + "  • •", base_ + "  • • •" };
            int idx = 0;
            while (true)
            {
                if (_subtitleTmp) _subtitleTmp.text = states[idx % 3];
                idx++;
                yield return new WaitForSeconds(0.4f);
            }
        }

        IEnumerator FillProgressBar(float duration)
        {
            if (_progressFillRT == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                _progressFillRT.anchorMax = new Vector2(Mathf.Clamp01(t / duration), 1f);
                yield return null;
            }
            _progressFillRT.anchorMax = new Vector2(1f, 1f);
        }

        IEnumerator AnimateRow(int i)
        {
            var cg = i < _rowGroups.Length ? _rowGroups[i] : null;

            // Y-offset animation is omitted: setting anchoredPosition on a VLG child
            // triggers layout dirty and the group repositions everything next frame.
            // Alpha fade alone is clean and doesn't touch layout.
            float dur = 0.45f, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                if (cg) cg.alpha = EaseOut(Mathf.Clamp01(t / dur));
                yield return null;
            }
            if (cg) cg.alpha = 1f;
        }

        IEnumerator DelayedPop(int i, float delay)
        {
            yield return new WaitForSeconds(delay);

            var cg = i < _checkGroups.Length     ? _checkGroups[i]     : null;
            var tr = i < _checkTransforms.Length ? _checkTransforms[i] : null;

            float dur = 0.4f, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = EaseOut(Mathf.Clamp01(t / dur));
                if (cg) cg.alpha      = p;
                if (tr) tr.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, p);
                yield return null;
            }
            if (cg) cg.alpha      = 1f;
            if (tr) tr.localScale = Vector3.one;
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        static string Prettify(string value)
        {
            if (string.IsNullOrEmpty(value)) return "—";
            var sb  = new System.Text.StringBuilder();
            bool cap = true;
            foreach (char c in value)
            {
                sb.Append(cap ? char.ToUpper(c) : c);
                cap = c == ' ';
            }
            return sb.ToString();
        }
    }
}
