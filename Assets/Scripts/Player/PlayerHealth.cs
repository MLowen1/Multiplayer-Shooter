using UnityEngine;
using PurrNet;
using System;
using UnityEngine.SocialPlatforms.Impl;

public class PlayerHealth : NetworkBehaviour
{
    [SerializeField] private SyncVar<int> health = new(100);
    [SerializeField] private int selfLayer, otherLayer;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    public Action<PlayerID> OnDeath_Server;

    public int Health => health.value;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        var actualLayer = isOwner ? selfLayer : otherLayer;
        SetLayerRecursive(gameObject, actualLayer);

        if (showDebug)
            Debug.Log($"[PlayerHealth] Spawned. IsOwner: {isOwner}, Layer set to: {actualLayer}, Health: {health.value}");

        if (isOwner)
        {
            InstanceHandler.GetInstance<MainGameView>()?.UpdateHealth(health.value);
            health.onChanged += OnHealthChanged;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (isOwner)
            health.onChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(int newHealth)
    {
        if (showDebug)
            Debug.Log($"[PlayerHealth] Health changed to: {newHealth}");

        InstanceHandler.GetInstance<MainGameView>()?.UpdateHealth(newHealth);
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    [ServerRpc(requireOwnership: false)]
    public void ChangeHealth(int amount, RPCInfo info = default)
    {
        if (showDebug)
            Debug.Log($"[PlayerHealth] ChangeHealth called on server. Amount: {amount}, Current health: {health.value}");

        health.value += amount;

        if (showDebug)
            Debug.Log($"[PlayerHealth] New health: {health.value}");

        if (health.value <= 0)
        {
            if (showDebug)
                Debug.Log($"[PlayerHealth] Player died! Invoking OnDeath_Server and destroying...");

            if(InstanceHandler.TryGetInstance(out ScoreManager scoreManager))
            {
                scoreManager.AddKill(info.sender);
                if(owner.HasValue)
                    scoreManager.AddDeath(owner.Value);
            }
            OnDeath_Server?.Invoke(owner.Value);

            // Destroy the networked object - server will propagate to clients
            Destroy(gameObject);
        }
    }
}