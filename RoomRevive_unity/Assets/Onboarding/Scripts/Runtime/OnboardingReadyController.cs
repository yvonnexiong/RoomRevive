using System.Collections;
using UnityEngine;
using TMPro;

namespace RoomRevive.Onboarding
{
    // Drives ReadyUI animations.
    // Triggered by OnEnable so it fires each time FlowController does SetActive(true).
    //
    // Sequence:
    //   t=0     all rows invisible, dots start cycling
    //   waiting BindData() called by FlowController when selection JSON arrives
    //           (or _dataTimeoutSeconds elapses — falls back to placeholder names)
    //   then    rows stagger in 0.07s apart, each fading over 0.4s
    public class OnboardingReadyController : MonoBehaviour
    {
        [SerializeField] CanvasGroup[]     _rowGroups;   // one per product row
        [SerializeField] TextMeshProUGUI[] _productTmps; // right-side name TMP per row
        [SerializeField] TextMeshProUGUI   _noteTmp;

        [Tooltip("Seconds to wait for selection data before showing placeholder names.")]
        [SerializeField] float _dataTimeoutSeconds = 4f;

        bool _dataReady;

        void OnEnable()
        {
            _dataReady = false;
            StopAllCoroutines();
            StartCoroutine(RunSequence());
        }

        void OnDisable() => StopAllCoroutines();

        public void Setup(CanvasGroup[] rowGroups, TextMeshProUGUI[] productTmps, TextMeshProUGUI noteTmp)
        {
            _rowGroups   = rowGroups;
            _productTmps = productTmps;
            _noteTmp     = noteTmp;
        }

        // Called by OnboardingFlowController when the bridge receives onboarding_selection.json.
        public void BindData(SelectionRow[] rows)
        {
            if (_productTmps != null)
                for (int i = 0; i < rows.Length && i < _productTmps.Length; i++)
                    if (_productTmps[i] && !string.IsNullOrEmpty(rows[i].name))
                        _productTmps[i].text = rows[i].name;
            _dataReady = true;
        }

        IEnumerator RunSequence()
        {
            foreach (var cg in _rowGroups)
                if (cg) cg.alpha = 0f;

            yield return null; // let layout settle

            if (_noteTmp) StartCoroutine(AnimateDots());

            // Wait for real data or timeout — whichever comes first
            float elapsed = 0f;
            while (!_dataReady && elapsed < _dataTimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < _rowGroups.Length; i++)
            {
                StartCoroutine(FadeRow(i));
                if (i < _rowGroups.Length - 1)
                    yield return new WaitForSeconds(0.07f);
            }
        }

        IEnumerator FadeRow(int i)
        {
            var cg = _rowGroups[i];
            if (cg == null) yield break;
            float dur = 0.4f, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                cg.alpha = EaseOut(Mathf.Clamp01(t / dur));
                yield return null;
            }
            cg.alpha = 1f;
        }

        IEnumerator AnimateDots()
        {
            string base_ = "Transforming your kitchen";
            string[] states = { base_ + " .", base_ + " ..", base_ + " ..." };
            int idx = 0;
            while (true)
            {
                if (_noteTmp) _noteTmp.text = states[idx % 3];
                idx++;
                yield return new WaitForSeconds(0.5f);
            }
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
