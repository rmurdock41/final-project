using UnityEngine;

/// <summary>
/// Migrates old standalone-player window preferences to the new 16:9 layout once.
/// Unity otherwise keeps the resolution saved by an earlier build in the registry.
/// </summary>
public static class StandaloneResolutionBootstrap
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private const string MigrationKey = "OkamiBrush.Resolution1080p.v2";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyInitialResolution()
    {
        if (PlayerPrefs.GetInt(MigrationKey, 0) == 1)
            return;

        // Keep the original borderless fullscreen presentation path.  The previous
        // migration forced a regular window, which produced uneven presentation and
        // visible tearing on some Windows/NVIDIA configurations.
        Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
        PlayerPrefs.SetInt(MigrationKey, 1);
        PlayerPrefs.Save();
    }
#endif
}
