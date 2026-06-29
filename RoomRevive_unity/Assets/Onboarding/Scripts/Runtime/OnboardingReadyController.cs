using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RoomRevive.Onboarding
{
    // Drives ReadyUI animations.
    // Triggered by OnEnable so it fires each time FlowController does SetActive(true).
    //
    // Sequence:
    //   t=0   all rows invisible
    //   t=0+  rows stagger in 0.07s apart, each fading over 0.4s
    //   ∞     "Transforming your kitchen" note cycles "." / ".." / "..." every 0.5s
    public class OnboardingReadyController : MonoBehaviour
    {
        [SerializeField] CanvasGroup[]   _rowGroups; // one per product row
        [SerializeField] TextMeshProUGUI _noteTmp;

        void OnEnable()
        {
            StopAllCoroutines();
            StartCoroutine(RunSequence());
        }

        void OnDisable() => StopAllCoroutines();

        public void Setup(CanvasGroup[] rowGroups, TextMeshProUGUI noteTmp)
        {
            _rowGroups = rowGroups;
            _noteTmp   = noteTmp;
        }

        IEnumerator RunSequence()
        {
            foreach (var cg in _rowGroups)
                if (cg) cg.alpha = 0f;

            yield return null; // let layout settle

            for (int i = 0; i < _rowGroups.Length; i++)
            {
                StartCoroutine(FadeRow(i));
                if (i < _rowGroups.Length - 1)
                    yield return new WaitForSeconds(0.07f);
            }

            if (_noteTmp) StartCoroutine(AnimateDots());
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
                _noteTmp.text = states[idx % 3];
                idx++;
                yield return new WaitForSeconds(0.5f);
            }
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
