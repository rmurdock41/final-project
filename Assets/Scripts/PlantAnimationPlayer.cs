using System.Collections;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;

public class PlantAnimationPlayer : MonoBehaviour
{
    private AlembicStreamPlayer alembicPlayer;
    public float shrinkDuration = 2f; 

    void Start()
    {
        alembicPlayer = GetComponent<AlembicStreamPlayer>();
        if (alembicPlayer == null)
        {
            Debug.LogError("PlantAnimationPlayer requires an AlembicStreamPlayer component.", this);
            enabled = false;
            return;
        }

        alembicPlayer.CurrentTime = 0f;
        StartCoroutine(PlayAnimation());
    }

    private IEnumerator PlayAnimation()
    {
        float duration = alembicPlayer.Duration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            alembicPlayer.CurrentTime = elapsed;
            elapsed += Time.deltaTime;
            yield return null;
        }

        alembicPlayer.CurrentTime = duration;

        yield return new WaitForSeconds(3f);

        yield return StartCoroutine(ShrinkAndDestroy());
    }

    private IEnumerator ShrinkAndDestroy()
    {
        if (shrinkDuration <= 0f)
        {
            Destroy(gameObject);
            yield break;
        }

        Vector3 originalScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkDuration;
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
            yield return null;
        }

        Destroy(gameObject);
    }
}
