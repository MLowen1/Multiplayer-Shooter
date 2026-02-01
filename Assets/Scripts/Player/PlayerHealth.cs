using UnityEngine;
using PurrNet;
using System;

public class PlayerHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int selfLayer, otherLayer;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private SyncVar<int> _health = new(100);
    private SyncVar<bool> _isDead = new(false);

    public Action<PlayerID> OnDeath_Server;

    public int Health => _health.value;
    public int MaxHealth => maxHealth;
    public bool IsDead => _isDead.value;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        // Reset state on spawn (server only sets these)
        if (isServer)
        {
            _isDead.value = false;
            _health.value = maxHealth;
        }

        var actualLayer = isOwner ? selfLayer : otherLayer;
        SetLayerRecursive(gameObject, actualLayer);

        if (showDebug)
            Debug.Log($"[PlayerHealth] Spawned. IsOwner: {isOwner}, IsServer: {isServer}, Layer: {actualLayer}, Health: {_health.value}");

        if (isOwner)
        {
            InstanceHandler.GetInstance<MainGameView>()?.UpdateHealth(_health.value);
            _health.onChanged += OnHealthChanged;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (isOwner)
            _health.onChanged -= OnHealthChanged;
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
        // Only process on server
        if (!isServer)
        {
            if (showDebug)
                Debug.Log("[PlayerHealth] ChangeHealth called but not server, ignoring");
            return;
        }

        // Prevent processing if already dead
        if (_isDead.value)
        {
            if (showDebug)
                Debug.Log($"[PlayerHealth] Ignoring damage - player already dead. Health: {_health.value}");
            return;
        }

        // Only Props can take damage - Seekers are immune
        if (amount < 0) // Only check for damage, not healing
        {
            var playerTeam = GetComponent<PlayerTeam>();
            if (playerTeam != null && playerTeam.CurrentTeam == Team.Seekers)
            {
                if (showDebug)
                    Debug.Log($"[PlayerHealth] Ignoring damage - Seekers cannot take damage");
                return;
            }
        }

        int previousHealth = _health.value;
        _health.value += amount;

        // Clamp health
        _health.value = Mathf.Clamp(_health.value, 0, maxHealth);

        if (showDebug)
            Debug.Log($"[PlayerHealth] ChangeHealth: {previousHealth} + ({amount}) = {_health.value}. IsDead: {_isDead.value}");

        // Check for death - must be at 0 or below AND not already dead
        if (_health.value <= 0 && !_isDead.value)
        {
            if (showDebug)
                Debug.Log($"[PlayerHealth] Player died! Health: {_health.value}, Processing death...");

            // Mark as dead IMMEDIATELY before any other processing
            _isDead.value = true;

            ProcessDeath(info.sender);
        }
    }

    private void ProcessDeath(PlayerID killerID)
    {
        if (showDebug)
            Debug.Log($"[PlayerHealth] ProcessDeath called. Killer: {killerID}, Victim: {owner.Value}");

        // Track kills and deaths in ScoreManager
        if (InstanceHandler.TryGetInstance(out ScoreManager scoreManager))
        {
            scoreManager.AddKill(killerID);
            if (owner.HasValue)
                scoreManager.AddDeath(owner.Value);

            if (showDebug)
                Debug.Log($"[PlayerHealth] Score updated - Kill for {killerID}, Death for {owner.Value}");
        }

        // Notify RoundManager if a prop was killed
        var playerTeam = GetComponent<PlayerTeam>();
        if (playerTeam != null)
        {
            if (showDebug)
                Debug.Log($"[PlayerHealth] Player team: {playerTeam.CurrentTeam}");

            if (playerTeam.CurrentTeam == Team.Props)
            {
                if (InstanceHandler.TryGetInstance(out RoundManager roundManager))
                {
                    if (owner.HasValue)
                    {
                        if (showDebug)
                            Debug.Log($"[PlayerHealth] Notifying RoundManager of prop death");
                        roundManager.OnPropKilled(killerID, owner.Value);
                    }
                }
                else
                {
                    if (showDebug)
                        Debug.LogWarning("[PlayerHealth] RoundManager not found!");
                }
            }
        }

        if (owner.HasValue)
            OnDeath_Server?.Invoke(owner.Value);

        if (showDebug)
            Debug.Log($"[PlayerHealth] Destroying player object");

        // Destroy the networked object - server will propagate to clients
        Destroy(gameObject);
    }
}