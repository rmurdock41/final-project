using UnityEngine;

public sealed class OkamiTrailDemoMover : MonoBehaviour
{
    public Vector3 center = new Vector3(0f, 0.55f, 0f);
    public float radiusX = 7f;
    public float radiusZ = 5f;
    public float speed = 0.65f;

    private float phase;

    private void Update()
    {
        phase += Time.deltaTime * speed;
        Vector3 nextPosition = center + new Vector3(
            Mathf.Sin(phase) * radiusX,
            0f,
            Mathf.Sin(phase * 2f) * radiusZ);

        Vector3 movement = nextPosition - transform.position;
        movement.y = 0f;
        if (movement.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(movement.normalized, Vector3.up);

        transform.position = nextPosition;
    }
}
