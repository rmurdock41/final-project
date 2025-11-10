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


    private Color daySunDisc, nightSunDisc;
    private Color daySunHalo, nightSunHalo;
    private Color dayHorizon, nightHorizon;
    private Color daySkyTop, nightSkyTop;
    private Color daySkyBottom, nightSkyBottom;

    void Start()
    {
        maxLightIntensity = sunLight.intensity;


        daySunDisc = daySkybox.GetColor("_SunDiscColor");
        daySunHalo = daySkybox.GetColor("_SunHaloColor");
        dayHorizon = daySkybox.GetColor("_HorizonLineColor");
        daySkyTop = daySkybox.GetColor("_SkyGradientTop");
        daySkyBottom = daySkybox.GetColor("_SkyGradientBottom");

        nightSunDisc = nightSkybox.GetColor("_SunDiscColor");
        nightSunHalo = nightSkybox.GetColor("_SunHaloColor");
        nightHorizon = nightSkybox.GetColor("_HorizonLineColor");
        nightSkyTop = nightSkybox.GetColor("_SkyGradientTop");
        nightSkyBottom = nightSkybox.GetColor("_SkyGradientBottom");


        RenderSettings.skybox = daySkybox;
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


        Material currentSkybox = RenderSettings.skybox;
        currentSkybox.SetColor("_SunDiscColor", currentSunDisc);
        currentSkybox.SetColor("_SunHaloColor", currentSunHalo);
        currentSkybox.SetColor("_HorizonLineColor", currentHorizon);
        currentSkybox.SetColor("_SkyGradientTop", currentTop);
        currentSkybox.SetColor("_SkyGradientBottom", currentBottom);


        sunLight.intensity = Mathf.Lerp(0.1f, maxLightIntensity, blendValue);

        DynamicGI.UpdateEnvironment();
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
