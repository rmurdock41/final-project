using UnityEngine;

public class ParticleFadeOut : MonoBehaviour
{
    public float fadeDuration = 1f;        
    public float scaleDuration = 0.8f;    
    public float rotationSpeed = 180f;
    public float startDelay = 0f;

    private MeshRenderer meshRenderer;
    private float timer = 0;
    private Vector3 initialScale;
    private Color initialColor;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        initialScale = transform.localScale;

        if (meshRenderer != null && meshRenderer.material != null)
        {

            meshRenderer.material = new Material(meshRenderer.material);
            initialColor = meshRenderer.material.color;
        }
    }

    void Update()
    {

        if (startDelay > 0)
        {
            startDelay -= Time.deltaTime;
            return;
        }
        timer += Time.deltaTime;

        if (meshRenderer != null && meshRenderer.material != null)
        {
            float alpha = 1f - (timer / fadeDuration);
            Color color = initialColor;
            color.a = Mathf.Clamp01(alpha);
            meshRenderer.material.color = color;
        }

        if (timer < scaleDuration)
        {
            float scale = 1f - (timer / scaleDuration);
            transform.localScale = initialScale * Mathf.Max(scale, 0.1f);
        }


        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);

        if (timer >= fadeDuration)
        {
            Destroy(gameObject);
        }
    }
}