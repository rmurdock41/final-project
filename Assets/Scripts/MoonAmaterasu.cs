using UnityEngine;
using DG.Tweening;
using System.Collections;

public class MoonAmaterasu : MonoBehaviour
{
    public GameObject moonObject;  
    public float displayTime = 0.5f; 

    private Renderer moonRenderer;

    void Start()
    {
        if (moonObject != null)
            moonRenderer = moonObject.GetComponent<Renderer>();

        if (moonRenderer != null)
        {
            SetAlpha(moonRenderer, 0);
        }

        StartCoroutine(PlaySequence());
    }

    IEnumerator PlaySequence()
    {

        if (moonRenderer != null)
        {
            moonRenderer.material.DOFade(1, 0.2f);
        }
        yield return new WaitForSeconds(0.2f);

        var skyCtrl = FindObjectOfType<SkyboxController>();
        if (skyCtrl) skyCtrl.SetNight();


        yield return new WaitForSeconds(displayTime);

        Disappear();
    }

    void Disappear()
    {
        if (moonRenderer)
            moonRenderer.material.DOFade(0, 1f).OnComplete(() => Destroy(gameObject));
    }

    void SetAlpha(Renderer r, float alpha)
    {
        if (r == null) return;
        Color c = r.material.color;
        c.a = alpha;
        r.material.color = c;
    }
}