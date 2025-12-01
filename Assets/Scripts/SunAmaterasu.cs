using UnityEngine;
using DG.Tweening;
using System.Collections;

public class SunAmaterasu : MonoBehaviour
{
    public GameObject discObject;
    public GameObject raysObject;
    public float spinSpeed = 30f;

    private Renderer discRenderer;
    private Renderer raysRenderer;

    void Start()
    {

        if (discObject != null)
            discRenderer = discObject.GetComponent<Renderer>();

        if (raysObject != null)
            raysRenderer = raysObject.GetComponent<Renderer>();


        if (discRenderer != null)
        {
            SetAlpha(discRenderer, 0);
        }

        if (raysRenderer != null)
        {
            SetAlpha(raysRenderer, 0);

        }

        StartCoroutine(PlaySequence());
    }

    IEnumerator PlaySequence()
    {
        if (discRenderer != null)
        {
            discRenderer.material.DOFade(1, 1f);
        }
        yield return new WaitForSeconds(1f);


        FlashScreen();
        var skyCtrl = FindObjectOfType<SkyboxController>();
        if (skyCtrl) skyCtrl.SetDay();

        yield return new WaitForSeconds(0.2f);


        if (raysRenderer != null)
        {
            raysRenderer.material.DOFade(1, 0.5f);
        }

        yield return new WaitForSeconds(0.5f);



        StartCoroutine(RotateRays());

        yield return new WaitForSeconds(2.5f);
        Disappear();
    }

    IEnumerator RotateRays()
    {

        while (raysObject != null)
        {
            raysObject.transform.Rotate(0, 0, spinSpeed * Time.deltaTime);
            yield return null;
        }
    }

    void Disappear()
    {
        StopAllCoroutines();

        // rays 先消失
        if (raysRenderer)
            raysRenderer.material.DOFade(0, 0.5f);

        // disc 延迟 0.5 秒后再开始消失
        if (discRenderer)
            discRenderer.material.DOFade(0, 1f).SetDelay(0.2f).OnComplete(() => Destroy(gameObject));
    }

    void SetAlpha(Renderer r, float alpha)
    {
        if (r == null) return;
        Color c = r.material.color;
        c.a = alpha;
        r.material.color = c;
    }

    void FlashScreen()
    {
        GameObject go = new GameObject("AutoFlash");
        Canvas cv = go.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 100;
        UnityEngine.UI.Image img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.white;
        img.raycastTarget = false;
        img.DOFade(0, 0.5f).From(1).OnComplete(() => Destroy(go));
    }
}