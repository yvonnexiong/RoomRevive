using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace RoomRevive
{
    /// <summary>
    /// Plays fixed sections of an AnimationClip without requiring an Animator Controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimationSegmentPlayer : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip animationClip;

        private PlayableGraph _graph;
        private AnimationClipPlayable _clipPlayable;
        private AnimationClip _loadedClip;
        private Coroutine _segmentRoutine;

        private void Reset()
        {
            animator = GetComponent<Animator>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
        }

        [ContextMenu("Play Animation/Play 0s to 3s")]
        public void PlayFirstSegment()
        {
            PlaySegment(0f, 3f);
        }

        [ContextMenu("Play Animation/Play 8s to 11s")]
        public void PlaySecondSegment()
        {
            PlaySegment(8f, 11f);
        }

        private void PlaySegment(float startTime, float endTime)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    $"[{nameof(AnimationSegmentPlayer)}] Enter Play Mode before testing an animation segment.",
                    this
                );
                return;
            }

            if (!isActiveAndEnabled)
            {
                Debug.LogWarning(
                    $"[{nameof(AnimationSegmentPlayer)}] The component and its GameObject must be active.",
                    this
                );
                return;
            }

            if (animator == null || animationClip == null)
            {
                Debug.LogWarning(
                    $"[{nameof(AnimationSegmentPlayer)}] Assign both Animator and Animation Clip.",
                    this
                );
                return;
            }

            if (startTime >= animationClip.length)
            {
                Debug.LogWarning(
                    $"[{nameof(AnimationSegmentPlayer)}] The clip is only {animationClip.length:0.###} seconds long; " +
                    $"it cannot start at {startTime:0.###} seconds.",
                    this
                );
                return;
            }

            float clampedEndTime = Mathf.Min(endTime, animationClip.length);

            EnsureGraph();

            if (_segmentRoutine != null)
                StopCoroutine(_segmentRoutine);

            _clipPlayable.SetTime(startTime);
            _clipPlayable.SetSpeed(1d);
            _graph.Play();
            _graph.Evaluate(0f);

            _segmentRoutine = StartCoroutine(StopAtTime(clampedEndTime));
        }

        private IEnumerator StopAtTime(float endTime)
        {
            while (_clipPlayable.IsValid() && _clipPlayable.GetTime() < endTime)
                yield return null;

            if (_clipPlayable.IsValid())
            {
                _clipPlayable.SetTime(endTime);
                _clipPlayable.SetSpeed(0d);
                _graph.Evaluate(0f);
            }

            _segmentRoutine = null;
        }

        private void EnsureGraph()
        {
            if (_graph.IsValid() && _loadedClip == animationClip)
                return;

            DestroyGraph();

            _graph = PlayableGraph.Create($"{name} Animation Segment");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            _clipPlayable = AnimationClipPlayable.Create(_graph, animationClip);
            _clipPlayable.SetApplyFootIK(false);
            _clipPlayable.SetApplyPlayableIK(false);

            AnimationPlayableOutput output =
                AnimationPlayableOutput.Create(_graph, "Animation", animator);
            output.SetSourcePlayable(_clipPlayable);

            _loadedClip = animationClip;
        }

        private void OnDisable()
        {
            DestroyGraph();
        }

        private void OnDestroy()
        {
            DestroyGraph();
        }

        private void DestroyGraph()
        {
            if (_segmentRoutine != null)
            {
                StopCoroutine(_segmentRoutine);
                _segmentRoutine = null;
            }

            if (_graph.IsValid())
                _graph.Destroy();

            _clipPlayable = default;
            _loadedClip = null;
        }
    }
}
