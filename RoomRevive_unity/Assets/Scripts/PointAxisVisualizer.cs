using UnityEngine;

public class PointAxisVisualizer : MonoBehaviour
{
    [Header("Point")]
    public Vector3 localPoint = Vector3.zero;

    [Header("Axis Settings")]
    public float axisLength = 0.5f;
    public float sphereSize = 0.04f;

    private void OnDrawGizmos()
    {
        Vector3 worldPoint = transform.TransformPoint(localPoint);

        // Draw center point
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(worldPoint, sphereSize);

        // Draw local X axis
        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            worldPoint,
            worldPoint + transform.right * axisLength
        );

        // Draw local Y axis
        Gizmos.color = Color.green;
        Gizmos.DrawLine(
            worldPoint,
            worldPoint + transform.up * axisLength
        );

        // Draw local Z axis
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(
            worldPoint,
            worldPoint + transform.forward * axisLength
        );
    }
}