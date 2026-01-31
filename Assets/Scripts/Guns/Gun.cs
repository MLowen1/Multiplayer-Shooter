using System;
using UnityEngine;
using PurrNet;

public class Gun : NetworkBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private LayerMask hitLayer;
    [SerializeField] private float range = 20f;
    [SerializeField] private int damage = 10;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    protected override void OnSpawned()
    {
        base.OnSpawned();
        enabled = isOwner;

        if (isOwner && showDebug)
        {
            Debug.Log($"[Gun] Spawned. HitLayer mask: {hitLayer.value}");
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Mouse0))
            return;

        if (showDebug)
            Debug.Log("[Gun] Fired!");

        // First, do a raycast without layer mask to see what we hit
        if (showDebug && Physics.Raycast(cameraTransform.position, cameraTransform.forward, out var debugHit, range))
        {
            Debug.Log($"[Gun] Raycast (no mask) hit: {debugHit.transform.name} on layer {debugHit.transform.gameObject.layer}");
        }

        // Now do the actual raycast with layer mask
        if (!Physics.Raycast(cameraTransform.position, cameraTransform.forward, out var hit, range, hitLayer))
        {
            if (showDebug)
                Debug.Log("[Gun] Raycast with hitLayer mask hit nothing");
            return;
        }

        if (showDebug)
            Debug.Log($"[Gun] Hit {hit.transform.name} on layer {hit.transform.gameObject.layer}");

        // Try to find PlayerHealth on the hit object or its parents
        var playerHealth = hit.transform.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null)
        {
            if (showDebug)
                Debug.Log($"[Gun] No PlayerHealth found on {hit.transform.name} or its parents");
            return;
        }

        if (showDebug)
            Debug.Log($"[Gun] Dealing {damage} damage to {playerHealth.gameObject.name}");

        // Call the server RPC to deal damage
        playerHealth.ChangeHealth(-damage);
    }

    // Visualize the raycast in editor
    private void OnDrawGizmos()
    {
        if (cameraTransform == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(cameraTransform.position, cameraTransform.forward * range);
    }
}