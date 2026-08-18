using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.Rendering;
using Cinemachine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public Transform brush;
    public Animator brushAnimator;
    public Camera brushCamera;

    public Volume grainVolume;
    public Renderer drawingRenderer;
    public CinemachineFreeLook freeLook;

    public bool isDrawing;

    private Camera mainCamera;
    private Camera backgroundCaptureCamera;
    private Demo gestureDemo;
    private InstancedIndirectGrassRenderer grassRenderer;
    private OkamiTrailFlowerRenderer flowerRenderer;
    private OkamiTrailInkBloomRenderer inkBloomRenderer;
    private RenderTexture runtimeBackgroundTexture;
    private bool captureBrushBackground;
    private float drawingPlaneAspectOverscan = 1f;

    private void Start()
    {
        mainCamera = Camera.main;
        gestureDemo = FindObjectOfType<Demo>();
        grassRenderer = FindObjectOfType<InstancedIndirectGrassRenderer>();
        flowerRenderer = FindObjectOfType<OkamiTrailFlowerRenderer>();
        inkBloomRenderer = FindObjectOfType<OkamiTrailInkBloomRenderer>();
        FindBackgroundCaptureCamera();

        Cursor.visible = false;

        if (brushCamera != null)
            brushCamera.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!captureBrushBackground || backgroundCaptureCamera == null)
            return;

        // These effects are submitted directly to Camera.main, so the separate
        // brush-background camera would otherwise miss them in a standalone build.
        if (grassRenderer != null)
            grassRenderer.RenderNow(backgroundCaptureCamera);
        if (flowerRenderer != null)
            flowerRenderer.RenderNow(backgroundCaptureCamera);
        if (inkBloomRenderer != null)
            inkBloomRenderer.RenderNow(backgroundCaptureCamera);
    }

    void Update()
    {
        Vector3 temp = Input.mousePosition;
        temp.z = .4f;
        if(isDrawing)
            brush.position = Vector3.Lerp(brush.position, mainCamera.ScreenToWorldPoint(temp), .5f);
        ClampPosition(brush);

        if (Input.GetKeyDown(KeyCode.C))
            SetDrawingMode(true);

        if (Input.GetKeyUp(KeyCode.C))
            SetDrawingMode(false);

        if (Input.GetKeyDown(KeyCode.R))
            SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);

        if(brushAnimator != null && brushAnimator.gameObject.activeSelf)
        {
            brushAnimator.SetFloat("X", Mathf.Lerp(brushAnimator.GetFloat("X"), Input.GetAxis("Mouse X") * 1, .07f));
            brushAnimator.SetFloat("Y", Mathf.Lerp(brushAnimator.GetFloat("Y"), Input.GetAxis("Mouse Y") * 1, .07f));
            brushAnimator.SetBool("isDrawing", Input.GetMouseButton(0));
        }
    }

    void ClampPosition(Transform obj)
    {
        Vector3 pos = mainCamera.WorldToViewportPoint(obj.position);
        pos.x = Mathf.Clamp01(pos.x);
        pos.y = Mathf.Clamp01(pos.y);
        obj.position = mainCamera.ViewportToWorldPoint(pos);
    }

    public void SetDrawingMode(bool state)
    {
        if (isDrawing == state)
            return;

        Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;

        if (state == true)
        {
            EnableBrushBackgroundCapture();
            mainCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Default"));
            mainCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Interactables"));
            //brush.GetChild(0).localPosition.Set(brush.GetChild(0).localPosition.x, 0.17f, brush.GetChild(0).localPosition.z);
            brush.GetChild(0).DOLocalMoveY(0.17f, .3f).SetUpdate(true).From();
        }
        else
        {
            captureBrushBackground = false;
            if (backgroundCaptureCamera != null)
                backgroundCaptureCamera.enabled = false;
            mainCamera.cullingMask |= 1 << LayerMask.NameToLayer("Default");
            mainCamera.cullingMask |= 1 << LayerMask.NameToLayer("Interactables");
        }

        isDrawing = state;

        //determine if time is running or not
        Time.timeScale = isDrawing ? 0 : 1;
        //determine if the freelook camera is active
        freeLook.enabled = !state;

        drawingRenderer.enabled = state;
        drawingRenderer.transform.GetChild(0).gameObject.SetActive(state);
        brushCamera.gameObject.SetActive(state);

        //effects
        float effectAmount = isDrawing ? 1 : 0;
        drawingRenderer.transform.DOLocalRotate(new Vector3(isDrawing ? 60 : 90, 180,0), .5f, RotateMode.Fast).SetUpdate(true);
        DOVirtual.Float(grainVolume.weight, effectAmount, .5f, (x) => grainVolume.weight = x).SetUpdate(true);
        drawingRenderer.material.DOFloat(effectAmount, "SepiaAmount", .5f).SetUpdate(true);

        if(state == false)
        {
            if (gestureDemo != null)
                gestureDemo.TryRecognize();
        }
    }

    private void FindBackgroundCaptureCamera()
    {
        if (mainCamera == null)
            return;

        Camera[] childCameras = mainCamera.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < childCameras.Length; i++)
        {
            Camera candidate = childCameras[i];
            if (candidate != mainCamera && candidate.targetTexture != null)
            {
                backgroundCaptureCamera = candidate;
                break;
            }
        }

        if (backgroundCaptureCamera == null)
            return;

        RememberDrawingPlaneAspectOverscan();
        backgroundCaptureCamera.enabled = false;
        backgroundCaptureCamera.depth = mainCamera.depth - 1f;
        CreateBackgroundTextureForCurrentScreen();
    }

    private void RememberDrawingPlaneAspectOverscan()
    {
        if (drawingRenderer == null)
            return;

        Vector3 scale = drawingRenderer.transform.localScale;
        if (Mathf.Abs(scale.z) > 0.0001f && backgroundCaptureCamera != null && backgroundCaptureCamera.targetTexture != null)
        {
            float originalAspect = (float)backgroundCaptureCamera.targetTexture.width /
                                   backgroundCaptureCamera.targetTexture.height;
            drawingPlaneAspectOverscan = Mathf.Abs(scale.x / scale.z) / originalAspect;
        }
    }

    private void CreateBackgroundTextureForCurrentScreen()
    {
        if (backgroundCaptureCamera == null)
            return;

        // Keep the exact color/read-write format of the RenderTexture authored in
        // the scene.  Creating an ARGB32 texture with ReadWrite.Default changes
        // the texture to sRGB in a Linear-color project, which makes the existing
        // brush/sepia shader crush much of the captured sky to black in a build.
        RenderTexture sourceTexture = backgroundCaptureCamera.targetTexture;
        if (sourceTexture == null)
            return;

        int screenWidth = Mathf.Max(1, Screen.width);
        int screenHeight = Mathf.Max(1, Screen.height);
        float downscale = Mathf.Min(1f, Mathf.Min(1920f / screenWidth, 1080f / screenHeight));
        int textureWidth = Mathf.Max(1, Mathf.RoundToInt(screenWidth * downscale));
        int textureHeight = Mathf.Max(1, Mathf.RoundToInt(screenHeight * downscale));

        RenderTextureDescriptor descriptor = sourceTexture.descriptor;
        descriptor.width = textureWidth;
        descriptor.height = textureHeight;
        descriptor.msaaSamples = 1;

        runtimeBackgroundTexture = new RenderTexture(descriptor)
        {
            name = "Brush Background (Runtime)",
            filterMode = sourceTexture.filterMode,
            wrapMode = sourceTexture.wrapMode
        };
        runtimeBackgroundTexture.Create();
        backgroundCaptureCamera.targetTexture = runtimeBackgroundTexture;

        if (drawingRenderer != null)
        {
            Material drawingMaterial = drawingRenderer.material;
            if (drawingMaterial.HasProperty("Texture2D_1E41419D"))
                drawingMaterial.SetTexture("Texture2D_1E41419D", runtimeBackgroundTexture);
            if (drawingMaterial.HasProperty("_MainTex"))
                drawingMaterial.SetTexture("_MainTex", runtimeBackgroundTexture);
        }
    }

    private void EnableBrushBackgroundCapture()
    {
        if (backgroundCaptureCamera == null)
            return;

        SyncBackgroundCaptureCamera();
        ResizeDrawingPlaneForCurrentAspect();
        captureBrushBackground = true;
        backgroundCaptureCamera.enabled = true;
    }

    private void SyncBackgroundCaptureCamera()
    {
        // The scene camera used to clear neither color nor skybox.  That can look
        // acceptable in the Editor because its old RenderTexture contents survive,
        // but a standalone player starts the texture black.  Match the real camera
        // so every brush frame receives the same sky/background before geometry.
        backgroundCaptureCamera.clearFlags = mainCamera.clearFlags;
        backgroundCaptureCamera.backgroundColor = mainCamera.backgroundColor;
        backgroundCaptureCamera.allowHDR = mainCamera.allowHDR;
        backgroundCaptureCamera.fieldOfView = mainCamera.fieldOfView;
        backgroundCaptureCamera.orthographic = mainCamera.orthographic;
        backgroundCaptureCamera.orthographicSize = mainCamera.orthographicSize;
        backgroundCaptureCamera.nearClipPlane = mainCamera.nearClipPlane;
        backgroundCaptureCamera.farClipPlane = mainCamera.farClipPlane;
    }

    private void ResizeDrawingPlaneForCurrentAspect()
    {
        if (drawingRenderer == null)
            return;

        Vector3 scale = drawingRenderer.transform.localScale;
        float verticalScale = Mathf.Abs(scale.z) > 0.0001f ? scale.z : scale.y;
        scale.x = verticalScale * mainCamera.aspect * drawingPlaneAspectOverscan;
        drawingRenderer.transform.localScale = scale;
    }

    private void OnDestroy()
    {
        if (runtimeBackgroundTexture == null)
            return;

        runtimeBackgroundTexture.Release();
        Destroy(runtimeBackgroundTexture);
        runtimeBackgroundTexture = null;
    }
}
