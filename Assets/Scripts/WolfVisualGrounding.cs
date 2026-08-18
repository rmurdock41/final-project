using Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(200)]
public sealed class WolfVisualGrounding : MonoBehaviour
{
    public Transform visualRoot;
    public Animator animator;
    public AnimationCurve walkGrounding;
    public float fullWalkBlend = 0.1f;
    public float maxVerticalCorrection = 0.25f;
    public float idleGroundBias = -0.115f;
    public float walkGroundBias = 0f;
    public Vector3 cameraTargetOffset = new Vector3(0f, 0.72f, 0.18f);

    [SerializeField]
    private float baseVisualLocalY;

    private void OnEnable()
    {
        if (Application.isPlaying) EnsureCameraTarget();
    }

    public void Configure(Transform modelRoot, Animator modelAnimator, AnimationCurve groundingCurve)
    {
        visualRoot = modelRoot;
        animator = modelAnimator;
        walkGrounding = groundingCurve;
        baseVisualLocalY = visualRoot != null ? visualRoot.localPosition.y : 0f;
    }

    private void LateUpdate()
    {
        ApplyGrounding();
    }

    private void EnsureCameraTarget()
    {
        Transform targetTransform = transform.Find("CameraTarget");
        if (targetTransform == null)
        {
            GameObject targetObject = new GameObject("CameraTarget");
            targetTransform = targetObject.transform;
            targetTransform.SetParent(transform, false);
        }

        SmoothCameraTarget smoothTarget = targetTransform.GetComponent<SmoothCameraTarget>();
        if (smoothTarget == null) smoothTarget = targetTransform.gameObject.AddComponent<SmoothCameraTarget>();
        smoothTarget.source = transform;
        smoothTarget.localOffset = cameraTargetOffset;
        smoothTarget.verticalSmoothTime = 0.18f;
        smoothTarget.verticalDeadZone = 0.025f;
        smoothTarget.SnapToSource();

        foreach (CinemachineVirtualCameraBase camera in FindObjectsOfType<CinemachineVirtualCameraBase>())
        {
            if (camera.gameObject.name != "ThirdPersonCamera") continue;
            camera.Follow = targetTransform;
            camera.LookAt = targetTransform;
        }
    }

    public float ApplyGrounding()
    {
        if (visualRoot == null || animator == null || walkGrounding == null || walkGrounding.length == 0) return 0f;

        Vector3 visualPosition = visualRoot.localPosition;
        visualPosition.y = baseVisualLocalY;
        visualRoot.localPosition = visualPosition;
        return 0f;
    }
}
