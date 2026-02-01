using System;
using UnityEngine;
using PurrNet;

public class Gun : NetworkBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private LayerMask hitLayer; // Should include PlayerOther (7) and Decoy (8)
    [SerializeField] private float range = 20f;
    [SerializeField] private int damage = 10;
    [SerializeField] private float fireCooldown = 0.1f; // Prevent rapid fire exploits

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private float _lastFireTime = 0f;

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

        // Cooldown check to prevent rapid fire
        if (Time.time - _lastFireTime < fireCooldown)
        {
            if (showDebug)
                Debug.Log("[Gun] Fire on cooldown");
            return;
        }

        // Block shooting during hiding phase
        if (InstanceHandler.TryGetInstance(out RoundManager roundManager))
        {
            if (roundManager.CurrentPhase == GamePhase.Hiding)
            {
                if (showDebug)
                    Debug.Log("[Gun] Cannot shoot during hiding phase!");
                return;
            }

            // Also block during round end and game over
            if (roundManager.CurrentPhase == GamePhase.RoundEnd ||
                roundManager.CurrentPhase == GamePhase.GameOver)
            {
                if (showDebug)
                    Debug.Log("[Gun] Cannot shoot - round is over!");
                return;
            }
        }

        _lastFireTime = Time.time;

        if (showDebug)
            Debug.Log("[Gun] Fired!");

        // First, do a raycast without layer mask to see what we hit (debug only)
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

        // Try to find PlayerHealth on the hit object or its parents (for hitting players)
        var playerHealth = hit.transform.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            // Check if player is already dead
            if (playerHealth.IsDead)
            {
                if (showDebug)
                    Debug.Log($"[Gun] Target {playerHealth.gameObject.name} is already dead, ignoring");
                return;
            }

            if (showDebug)
                Debug.Log($"[Gun] Dealing {damage} damage to player {playerHealth.gameObject.name} (current HP: {playerHealth.Health})");

            playerHealth.ChangeHealth(-damage);
            return;
        }

        // Try to find PropDecoy on the hit object or its parents (for hitting decoys)
        var decoy = hit.transform.GetComponentInParent<PropDecoy>();
        if (decoy != null)
        {
            if (showDebug)
                Debug.Log($"[Gun] Hit decoy {decoy.gameObject.name}");

            decoy.TakeDamage(damage);
            return;
        }

        if (showDebug)
            Debug.Log($"[Gun] No PlayerHealth or PropDecoy found on {hit.transform.name}");
    }

    // Visualize the raycast in editor
    private void OnDrawGizmos()
    {
        if (cameraTransform == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(cameraTransform.position, cameraTransform.forward * range);
    }
}