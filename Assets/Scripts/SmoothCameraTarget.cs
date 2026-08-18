using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class SmoothCameraTarget : MonoBehaviour
{
    public Transform source;
    public Vector3 localOffset = new Vector3(0f, 0.72f, 0.18f);
    public float verticalSmoothTime = 0.18f;
    public float verticalDeadZone = 0.025f;

    private float smoothedWorldY;
    private float verticalVelocity;
    private bool initialized;

    private void OnEnable()
    {
        SnapToSource();
    }

    private void LateUpdate()
    {
        if (source == null) return;
        Vector3 desiredPosition = source.TransformPoint(localOffset);
        if (!initialized)
        {
            smoothedWorldY = desiredPosition.y;
            initialized = true;
        }
        if (Mathf.Abs(desiredPosition.y - smoothedWorldY) > verticalDeadZone)
        {
            smoothedWorldY = Mathf.SmoothDamp(
                smoothedWorldY,
                desiredPosition.y,
                ref verticalVelocity,
                verticalSmoothTime
            );
        }
        transform.position = new Vector3(desiredPosition.x, smoothedWorldY, desiredPosition.z);
    }

    public void SnapToSource()
    {
        if (source == null) return;
        Vector3 desiredPosition = source.TransformPoint(localOffset);
        smoothedWorldY = desiredPosition.y;
        verticalVelocity = 0f;
        initialized = true;
        transform.position = desiredPosition;
    }
}
