using System.Collections.Generic;
using UnityEngine;
using PurrNet;

public class PropTransform : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject characterModel;
    [SerializeField] private GameObject humanHitbox;

    [Header("Props")]
    [SerializeField] private List<GameObject> propOptions = new();
    [SerializeField] private KeyCode transformKey = KeyCode.E;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Synced across network: -1 = human form, 0+ = prop index
    private SyncVar<int> _currentPropIndex = new(-1);

    private bool _isTransformed = false;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        // Subscribe to prop index changes
        _currentPropIndex.onChanged += OnPropIndexChanged;

        // Initialize: disable all props, enable character model
        SetAllPropsActive(false);
        if (characterModel != null)
            characterModel.SetActive(true);
        if (humanHitbox != null)
            humanHitbox.SetActive(true);

        if (showDebug)
            Debug.Log($"[PropTransform] Spawned. IsOwner: {isOwner}, Props available: {propOptions.Count}");
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _currentPropIndex.onChanged -= OnPropIndexChanged;
    }

    private void Update()
    {
        // Only owner can control transformation
        if (!isOwner) return;

        if (Input.GetKeyDown(transformKey))
        {
            if (showDebug)
                Debug.Log("[PropTransform] Transform key pressed!");

            ToggleTransform();
        }
    }

    private void ToggleTransform()
    {
        if (_isTransformed)
        {
            // Return to human form
            RequestTransform(-1);
        }
        else
        {
            // Transform into a random prop
            if (propOptions.Count > 0)
            {
                int randomIndex = Random.Range(0, propOptions.Count);
                RequestTransform(randomIndex);
            }
            else
            {
                Debug.LogWarning("[PropTransform] No props available to transform into!");
            }
        }
    }

    /// <summary>
    /// Request transformation - sends to server
    /// </summary>
    [ServerRpc]
    private void RequestTransform(int propIndex)
    {
        if (showDebug)
            Debug.Log($"[PropTransform] Server received transform request. PropIndex: {propIndex}");

        // Validate prop index
        if (propIndex >= propOptions.Count)
        {
            Debug.LogWarning($"[PropTransform] Invalid prop index: {propIndex}");
            return;
        }

        // Update synced variable - this will trigger OnPropIndexChanged on all clients
        _currentPropIndex.value = propIndex;
    }

    /// <summary>
    /// Called on ALL clients when prop index changes
    /// </summary>
    private void OnPropIndexChanged(int newPropIndex)
    {
        if (showDebug)
            Debug.Log($"[PropTransform] Prop index changed to: {newPropIndex}");

        ApplyTransformation(newPropIndex);
    }

    /// <summary>
    /// Apply the visual transformation
    /// </summary>
    private void ApplyTransformation(int propIndex)
    {
        // Disable all props first
        SetAllPropsActive(false);

        if (propIndex < 0)
        {
            // Return to human form
            if (characterModel != null)
                characterModel.SetActive(true);
            if (humanHitbox != null)
                humanHitbox.SetActive(true);

            _isTransformed = false;

            if (showDebug)
                Debug.Log("[PropTransform] Returned to human form");
        }
        else if (propIndex < propOptions.Count)
        {
            // Transform into prop
            if (characterModel != null)
                characterModel.SetActive(false);
            if (humanHitbox != null)
                humanHitbox.SetActive(false);

            // Enable the selected prop
            propOptions[propIndex].SetActive(true);

            _isTransformed = true;

            if (showDebug)
                Debug.Log($"[PropTransform] Transformed into prop: {propOptions[propIndex].name}");
        }
    }

    private void SetAllPropsActive(bool active)
    {
        foreach (var prop in propOptions)
        {
            if (prop != null)
                prop.SetActive(active);
        }
    }

    /// <summary>
    /// Transform into a specific prop by index
    /// </summary>
    public void TransformIntoProp(int propIndex)
    {
        if (!isOwner)
        {
            Debug.LogWarning("[PropTransform] Only owner can request transformation!");
            return;
        }

        RequestTransform(propIndex);
    }

    /// <summary>
    /// Return to human form
    /// </summary>
    public void ReturnToHuman()
    {
        if (!isOwner)
        {
            Debug.LogWarning("[PropTransform] Only owner can request transformation!");
            return;
        }

        RequestTransform(-1);
    }

    /// <summary>
    /// Check if currently transformed into a prop
    /// </summary>
    public bool IsTransformed => _isTransformed;

    /// <summary>
    /// Get current prop index (-1 = human)
    /// </summary>
    public int CurrentPropIndex => _currentPropIndex.value;
}