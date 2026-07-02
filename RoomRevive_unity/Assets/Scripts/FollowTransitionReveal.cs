using UnityEngine;

/// <summary>
/// Drives this object's position + scale to match the SplatManager transition reveal sphere, so an
/// effect (e.g. the Intersection Reveal Sphere) grows exactly with the transition cutout.
/// Radius → scale uses the sphere's "radius at scale 1" (auto-read from IntersectionRevealSphereEffect
/// if present, else the field below).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class FollowTransitionReveal : MonoBehaviour
{
    [Tooltip("SplatManager whose transition reveal we follow. Auto-found if empty.")]
    public SplatManager splatManager;

    [Header("Follow")]
    public bool followPosition = true;
    public bool followRadius = true;

    [Tooltip("Only follow while a transition is actually animating. When it ends, this stops updating " +
             "(leaving the effect at its last size), and optionally hides it.")]
    public bool onlyWhileTransitioning = true;

    [Tooltip("Deactivate this GameObject when no transition is running.")]
    public bool hideWhenIdle = false;

    [Tooltip("World radius the mesh has at localScale 1. Matches IntersectionRevealSphereEffect.sphereMeshRadius (0.5).")]
    public float radiusAtScaleOne = 0.5f;

    IntersectionRevealSphereEffect _effect;

    void OnEnable()
    {
        _effect = GetComponent<IntersectionRevealSphereEffect>();
        ResolveManager();
    }

    void LateUpdate()
    {
        if (splatManager == null) ResolveManager();
        if (splatManager == null) return;

        bool transitioning = splatManager.IsTransitioning;

        if (onlyWhileTransitioning && !transitioning)
        {
            if (hideWhenIdle && gameObject.activeSelf) gameObject.SetActive(false);
            return;
        }

        if (hideWhenIdle && !gameObject.activeSelf) gameObject.SetActive(true);

        if (followPosition)
            transform.position = splatManager.LastTransitionCenterWorld;

        if (followRadius)
        {
            float unit = _effect != null ? Mathf.Max(0.0001f, _effect.sphereMeshRadius) : Mathf.Max(0.0001f, radiusAtScaleOne);
            float scale = splatManager.LastTransitionRadius / unit;
            transform.localScale = Vector3.one * scale;
        }
    }

    void ResolveManager()
    {
#if UNITY_2022_2_OR_NEWER
        if (splatManager == null) splatManager = FindFirstObjectByType<SplatManager>();
#else
        if (splatManager == null) splatManager = FindObjectOfType<SplatManager>();
#endif
    }
}
