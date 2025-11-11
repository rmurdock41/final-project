using UnityEngine;

public class SkyboxController : MonoBehaviour
{
    public Material daySkybox;
    public Material nightSkybox;
    public Light sunLight;
    public float transitionSpeed = 0.5f;

    private bool isDay = true;
    private float blendValue = 1f;
    private float targetBlend = 1f;
    private float maxLightIntensity;


    private Material daySkyboxInstance;
    private Material nightSkyboxInstance;
    private Material runtimeSkybox;

    private Color daySunDisc, nightSunDisc;
    private Color daySunHalo, nightSunHalo;
    private Color dayHorizon, nightHorizon;
    private Color daySkyTop, nightSkyTop;
    private Color daySkyBottom, nightSkyBottom;

    void Start()
    {
        maxLightIntensity = sunLight.intensity;


        daySkyboxInstance = new Material(daySkybox);
        nightSkyboxInstance = new Material(nightSkybox);
        runtimeSkybox = new Material(daySkybox);

        daySunDisc = daySkyboxInstance.GetColor("_SunDiscColor");
        daySunHalo = daySkyboxInstance.GetColor("_SunHaloColor");
        dayHorizon = daySkyboxInstance.GetColor("_HorizonLineColor");
        daySkyTop = daySkyboxInstance.GetColor("_SkyGradientTop");
        daySkyBottom = daySkyboxInstance.GetColor("_SkyGradientBottom");

        nightSunDisc = nightSkyboxInstance.GetColor("_SunDiscColor");
        nightSunHalo = nightSkyboxInstance.GetColor("_SunHaloColor");
        nightHorizon = nightSkyboxInstance.GetColor("_HorizonLineColor");
        nightSkyTop = nightSkyboxInstance.GetColor("_SkyGradientTop");
        nightSkyBottom = nightSkyboxInstance.GetColor("_SkyGradientBottom");

        RenderSettings.skybox = runtimeSkybox;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.D))
        {
            SetDay();
        }
        if (Input.GetKeyDown(KeyCode.N))
        {
            SetNight();
        }

        blendValue = Mathf.Lerp(blendValue, targetBlend, Time.deltaTime * transitionSpeed);

        Color currentSunDisc = Color.Lerp(nightSunDisc, daySunDisc, blendValue);
        Color currentSunHalo = Color.Lerp(nightSunHalo, daySunHalo, blendValue);
        Color currentHorizon = Color.Lerp(nightHorizon, dayHorizon, blendValue);
        Color currentTop = Color.Lerp(nightSkyTop, daySkyTop, blendValue);
        Color currentBottom = Color.Lerp(nightSkyBottom, daySkyBottom, blendValue);


        runtimeSkybox.SetColor("_SunDiscColor", currentSunDisc);
        runtimeSkybox.SetColor("_SunHaloColor", currentSunHalo);
        runtimeSkybox.SetColor("_HorizonLineColor", currentHorizon);
        runtimeSkybox.SetColor("_SkyGradientTop", currentTop);
        runtimeSkybox.SetColor("_SkyGradientBottom", currentBottom);

        sunLight.intensity = Mathf.Lerp(0.1f, maxLightIntensity, blendValue);

        DynamicGI.UpdateEnvironment();
    }

    void OnDestroy()
    {
        if (daySkyboxInstance != null)
            Destroy(daySkyboxInstance);
        if (nightSkyboxInstance != null)
            Destroy(nightSkyboxInstance);
        if (runtimeSkybox != null)
            Destroy(runtimeSkybox);
    }

    public void SetDay()
    {
        isDay = true;
        targetBlend = 1f;
    }

    public void SetNight()
    {
        isDay = false;
        targetBlend = 0f;
    }
}