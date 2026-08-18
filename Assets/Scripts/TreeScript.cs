using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[SelectionBase]
public class TreeScript : MonoBehaviour
{
    private Collider treeCollider;
    public Transform originalForm;
    public Transform cutForm;
    private Rigidbody pieceRb;
    public ParticleSystem smokeParticle;

    private void Start()
    {
        if (cutForm.gameObject.activeSelf)
            cutForm.gameObject.SetActive(false);
        treeCollider = GetComponent<Collider>();
        pieceRb = cutForm.GetChild(0).GetComponent<Rigidbody>();
    }
    public void Slash()
    {
        treeCollider.enabled = false;
        originalForm.gameObject.SetActive(false);
        cutForm.gameObject.SetActive(true);
        pieceRb.AddForce(Vector3.right * 3, ForceMode.Impulse);
        smokeParticle.Play();
    }
}
