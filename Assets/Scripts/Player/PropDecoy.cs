using System.Collections.Generic;
using UnityEngine;
using PurrNet;

/// <summary>
/// A decoy prop that can be shot but has no physics.
/// Must be set up as a prefab with NetworkIdentity and all prop models as children.
/// </summary>
public class PropDecoy : NetworkBehaviour
{
    [Header("Prop Visuals")]
    [SerializeField] private List<GameObject> propModels = new();

    [Header("Settings")]
    [SerializeField] private int health = 1;
    [SerializeField] private int decoyLayer = 8; // Decoy layer - no collision with players

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Synced prop index - determines which model to show
    private SyncVar<int> _propIndex = new(-1);

    // Owner tracking for scoring
    private SyncVar<PlayerID> _ownerID = new();

    public PlayerID OwnerID => _ownerID.value;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        if (showDebug)
            Debug.Log($"[PropDecoy] OnSpawned. PropIndex: {_propIndex.value}, IsServer: {isServer}");

        // Subscribe to prop index changes
        _propIndex.onChanged += OnPropIndexChanged;

        // Hide all props initially
        HideAllProps();

        // If prop index is already set, apply it
        if (_propIndex.value >= 0)
        {
            ApplyPropVisual(_propIndex.value);
        }

        // Set layer for hit detection
        SetLayerRecursive(gameObject, decoyLayer);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _propIndex.onChanged -= OnPropIndexChanged;
    }

    /// <summary>
    /// Initialize the decoy with a prop index and owner. Called by PropTransform on server.
    /// </summary>
    public void Initialize(int propIndex, PlayerID ownerID)
    {
        if (!isServer)
        {
            Debug.LogWarning("[PropDecoy] Initialize should only be called on server!");
            return;
        }

        if (showDebug)
            Debug.Log($"[PropDecoy] Initialize called with index: {propIndex}, owner: {ownerID}");

        _propIndex.value = propIndex;
        _ownerID.value = ownerID;

        // Apply immediately on server
        ApplyPropVisual(propIndex);
    }

    private void OnPropIndexChanged(int newIndex)
    {
        if (showDebug)
            Debug.Log($"[PropDecoy] PropIndex changed to: {newIndex}");

        ApplyPropVisual(newIndex);
    }

    private void HideAllProps()
    {
        foreach (var prop in propModels)
        {
            if (prop != null)
                prop.SetActive(false);
        }
    }

    private void ApplyPropVisual(int index)
    {
        if (showDebug)
            Debug.Log($"[PropDecoy] ApplyPropVisual index: {index}, propModels.Count: {propModels.Count}");

        // Hide all first
        HideAllProps();

        // Show the selected one
        if (index >= 0 && index < propModels.Count)
        {
            propModels[index].SetActive(true);

            // Make sure it has a collider for hit detection
            EnsureCollider(propModels[index]);

            // Set layer
            SetLayerRecursive(propModels[index], decoyLayer);

            if (showDebug)
                Debug.Log($"[PropDecoy] Showing prop: {propModels[index].name}");
        }
        else
        {
            Debug.LogWarning($"[PropDecoy] Invalid prop index: {index}");
        }
    }

    private void EnsureCollider(GameObject obj)
    {
        var collider = obj.GetComponentInChildren<Collider>();
        if (collider == null)
        {
            var meshRenderer = obj.GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
            {
                var box = obj.AddComponent<BoxCollider>();
                box.size = meshRenderer.localBounds.size;
                box.center = meshRenderer.localBounds.center;
            }
            else
            {
                var box = obj.AddComponent<BoxCollider>();
                box.size = Vector3.one;
            }

            if (showDebug)
                Debug.Log($"[PropDecoy] Added collider to: {obj.name}");
        }
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    /// <summary>
    /// Called when the decoy is shot.
    /// </summary>
    [ServerRpc(requireOwnership: false)]
    public void TakeDamage(int damage, RPCInfo info = default)
    {
        if (showDebug)
            Debug.Log($"[PropDecoy] Hit! Damage: {damage}");

        health -= damage;

        if (health <= 0)
        {
            if (showDebug)
                Debug.Log("[PropDecoy] Destroyed!");

            // Notify RoundManager to award points to decoy owner
            if (InstanceHandler.TryGetInstance(out RoundManager roundManager))
            {
                roundManager.OnDecoyShot(_ownerID.value);
            }

            Destroy(gameObject);
        }
    }
}