using System.Collections.Generic;
using UnityEngine;
using PurrNet;

public class PropTransform : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject characterModel;
    [SerializeField] private PlayerTeam playerTeam;
    [SerializeField] private Gun gun;
    [SerializeField] private GameObject gunModel;
    [SerializeField] private GameObject cameraMimic;

    [Header("Props")]
    [SerializeField] private List<GameObject> propOptions = new();
    [SerializeField] private KeyCode transformKey = KeyCode.E;
    [SerializeField] private int maxTransforms = 3;

    [Header("Decoys")]
    [SerializeField] private PropDecoy decoyPrefab;
    [SerializeField] private KeyCode decoyKey = KeyCode.Q;
    [SerializeField] private int maxDecoysPerProp = 3;
    [SerializeField] private float decoyPlaceDistance = 3f;

    [Header("Third Person Camera")]
    [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0f, 2f, -4f);
    [SerializeField] private Transform cameraPivot;

    [Header("Layers")]
    [SerializeField] private int selfLayer = 6;
    [SerializeField] private int otherLayer = 7;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Synced across network
    private SyncVar<int> _currentPropIndex = new(-1);
    private SyncVar<int> _transformsRemaining = new(3);
    private SyncVar<int> _decoysRemaining = new(3);

    private bool _isTransformed = false;
    private Vector3 _originalCameraLocalPos;
    private bool _cameraPositionSaved = false;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        // Get references if not assigned
        if (playerTeam == null)
            playerTeam = GetComponent<PlayerTeam>();
        if (gun == null)
            gun = GetComponentInChildren<Gun>();

        // Subscribe to sync var changes
        _currentPropIndex.onChanged += OnPropIndexChanged;
        _transformsRemaining.onChanged += OnTransformsRemainingChanged;
        _decoysRemaining.onChanged += OnDecoysRemainingChanged;

        // Subscribe to team changes
        if (playerTeam != null)
            playerTeam.OnTeamChanged += OnTeamChanged;

        // Initialize: disable all props, enable character model
        SetAllPropsActive(false);
        if (characterModel != null)
            characterModel.SetActive(true);

        // Ensure all props have colliders
        EnsureCollidersExist();

        // Save original camera position for switching perspectives
        if (isOwner && cameraPivot != null)
        {
            _originalCameraLocalPos = cameraPivot.localPosition;
            _cameraPositionSaved = true;
        }

        if (showDebug)
            Debug.Log($"[PropTransform] Spawned. IsOwner: {isOwner}, Props available: {propOptions.Count}");

        // Check if already assigned to Props team (for late sync)
        if (playerTeam != null && playerTeam.CurrentTeam == Team.Props)
        {
            OnTeamChanged(Team.Props);
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _currentPropIndex.onChanged -= OnPropIndexChanged;
        _transformsRemaining.onChanged -= OnTransformsRemainingChanged;
        _decoysRemaining.onChanged -= OnDecoysRemainingChanged;

        if (playerTeam != null)
            playerTeam.OnTeamChanged -= OnTeamChanged;
    }

    /// <summary>
    /// Make sure character model and all props have colliders for hit detection
    /// </summary>
    private void EnsureCollidersExist()
    {
        // Check character model has a collider
        if (characterModel != null)
        {
            var collider = characterModel.GetComponentInChildren<Collider>();
            if (collider == null)
            {
                // Add a capsule collider as fallback
                var capsule = characterModel.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0, 1f, 0);
                capsule.radius = 0.3f;
                capsule.height = 2f;
                if (showDebug)
                    Debug.Log("[PropTransform] Added fallback collider to character model");
            }
        }

        // Check each prop has a collider
        foreach (var prop in propOptions)
        {
            if (prop == null) continue;

            var collider = prop.GetComponentInChildren<Collider>();
            if (collider == null)
            {
                // Add a box collider based on mesh bounds
                var meshRenderer = prop.GetComponentInChildren<MeshRenderer>();
                if (meshRenderer != null)
                {
                    var box = prop.AddComponent<BoxCollider>();
                    box.size = meshRenderer.localBounds.size;
                    box.center = meshRenderer.localBounds.center;
                }
                else
                {
                    var box = prop.AddComponent<BoxCollider>();
                    box.size = Vector3.one;
                }
                if (showDebug)
                    Debug.Log($"[PropTransform] Added fallback collider to prop: {prop.name}");
            }
        }
    }

    private void OnTeamChanged(Team newTeam)
    {
        if (showDebug)
            Debug.Log($"[PropTransform] Team changed to: {newTeam}");

        if (newTeam == Team.Props)
        {
            // Disable gun script for props
            if (gun != null)
                gun.enabled = false;

            // Hide gun model and camera mimic for props
            SetGunAndMimicVisible(false);

            // Auto-transform into a random prop on spawn (server only)
            if (isServer && _currentPropIndex.value < 0)
            {
                AutoTransformOnSpawn();
            }

            // Switch to third person camera (owner only)
            if (isOwner)
            {
                SetThirdPersonCamera(true);
            }
        }
        else if (newTeam == Team.Seekers)
        {
            // Enable gun for seekers
            if (gun != null && isOwner)
                gun.enabled = true;

            // Show gun model and camera mimic for seekers
            SetGunAndMimicVisible(true);

            // First person for seekers
            if (isOwner)
            {
                SetThirdPersonCamera(false);
            }
        }
    }

    private void SetGunAndMimicVisible(bool visible)
    {
        if (gunModel != null)
            gunModel.SetActive(visible);

        if (cameraMimic != null)
            cameraMimic.SetActive(visible);

        if (showDebug)
            Debug.Log($"[PropTransform] Gun/Mimic visibility set to: {visible}");
    }

    private void AutoTransformOnSpawn()
    {
        if (propOptions.Count > 0)
        {
            int randomIndex = Random.Range(0, propOptions.Count);
            _currentPropIndex.value = randomIndex;
            _transformsRemaining.value = maxTransforms;
            _decoysRemaining.value = maxDecoysPerProp;

            if (showDebug)
                Debug.Log($"[PropTransform] Auto-transformed into prop index: {randomIndex}");
        }
    }

    private void SetThirdPersonCamera(bool thirdPerson)
    {
        if (cameraPivot == null || !_cameraPositionSaved) return;

        if (thirdPerson)
        {
            cameraPivot.localPosition = thirdPersonOffset;
            if (showDebug)
                Debug.Log("[PropTransform] Switched to third person camera");
        }
        else
        {
            cameraPivot.localPosition = _originalCameraLocalPos;
            if (showDebug)
                Debug.Log("[PropTransform] Switched to first person camera");
        }
    }

    private void Update()
    {
        if (!isOwner) return;

        if (playerTeam == null || playerTeam.CurrentTeam != Team.Props) return;

        if (Input.GetKeyDown(transformKey))
        {
            TryTransform();
        }

        if (Input.GetKeyDown(decoyKey))
        {
            TryPlaceDecoy();
        }
    }

    private void TryTransform()
    {
        if (_transformsRemaining.value <= 0)
        {
            if (showDebug)
                Debug.Log("[PropTransform] No transforms remaining!");
            return;
        }

        if (propOptions.Count == 0)
        {
            Debug.LogWarning("[PropTransform] No props available!");
            return;
        }

        int newIndex = Random.Range(0, propOptions.Count);
        if (propOptions.Count > 1)
        {
            while (newIndex == _currentPropIndex.value)
            {
                newIndex = Random.Range(0, propOptions.Count);
            }
        }

        RequestTransform(newIndex);
    }

    private void TryPlaceDecoy()
    {
        if (showDebug)
            Debug.Log("[PropTransform] TryPlaceDecoy called");

        if (decoyPrefab == null)
        {
            Debug.LogWarning("[PropTransform] No decoy prefab assigned!");
            return;
        }

        if (_currentPropIndex.value < 0)
        {
            if (showDebug)
                Debug.Log("[PropTransform] Must be transformed to place decoys!");
            return;
        }

        if (_decoysRemaining.value <= 0)
        {
            if (showDebug)
                Debug.Log("[PropTransform] No decoys remaining!");
            return;
        }

        Vector3 placePosition = transform.position + transform.forward * decoyPlaceDistance;
        Quaternion placeRotation = transform.rotation;

        if (showDebug)
            Debug.Log($"[PropTransform] Requesting decoy at {placePosition}");

        RequestPlaceDecoy(_currentPropIndex.value, placePosition, placeRotation);
    }

    [ServerRpc]
    private void RequestTransform(int propIndex)
    {
        if (showDebug)
            Debug.Log($"[PropTransform] Server received transform request. PropIndex: {propIndex}");

        if (playerTeam != null && playerTeam.CurrentTeam != Team.Props)
        {
            Debug.LogWarning("[PropTransform] Server rejected - not on Props team!");
            return;
        }

        if (_transformsRemaining.value <= 0)
        {
            Debug.LogWarning("[PropTransform] Server rejected - no transforms remaining!");
            return;
        }

        if (propIndex < 0 || propIndex >= propOptions.Count)
        {
            Debug.LogWarning($"[PropTransform] Invalid prop index: {propIndex}");
            return;
        }

        _currentPropIndex.value = propIndex;
        _transformsRemaining.value--;
        _decoysRemaining.value = maxDecoysPerProp;

        if (showDebug)
            Debug.Log($"[PropTransform] Transformed! Remaining transforms: {_transformsRemaining.value}");
    }

    [ServerRpc]
    private void RequestPlaceDecoy(int propIndex, Vector3 position, Quaternion rotation)
    {
        if (showDebug)
            Debug.Log($"[PropTransform] Server received decoy request. PropIndex: {propIndex}, Position: {position}");

        if (_decoysRemaining.value <= 0)
        {
            Debug.LogWarning("[PropTransform] Server rejected - no decoys remaining!");
            return;
        }

        if (propIndex < 0 || propIndex >= propOptions.Count)
        {
            Debug.LogWarning("[PropTransform] Invalid prop index for decoy!");
            return;
        }

        // Spawn decoy
        var decoy = Instantiate(decoyPrefab, position, rotation);

        if (showDebug)
            Debug.Log($"[PropTransform] Decoy instantiated: {decoy.name}");

        // Get owner ID for scoring
        var identity = GetComponent<NetworkIdentity>();
        PlayerID ownerID = identity.owner ?? default;

        // Set which prop to show and who owns it
        decoy.Initialize(propIndex, ownerID);

        _decoysRemaining.value--;

        if (showDebug)
            Debug.Log($"[PropTransform] Decoy placed! Remaining: {_decoysRemaining.value}");
    }

    private void OnPropIndexChanged(int newPropIndex)
    {
        if (showDebug)
            Debug.Log($"[PropTransform] Prop index changed to: {newPropIndex}");

        ApplyTransformation(newPropIndex);
    }

    private void OnTransformsRemainingChanged(int remaining)
    {
        if (showDebug && isOwner)
            Debug.Log($"[PropTransform] Transforms remaining: {remaining}");
    }

    private void OnDecoysRemainingChanged(int remaining)
    {
        if (showDebug && isOwner)
            Debug.Log($"[PropTransform] Decoys remaining: {remaining}");
    }

    private void ApplyTransformation(int propIndex)
    {
        // Disable all props first
        SetAllPropsActive(false);

        // Determine which layer to use
        int targetLayer = isOwner ? selfLayer : otherLayer;

        if (propIndex < 0)
        {
            // Human form
            if (characterModel != null)
            {
                characterModel.SetActive(true);
                SetLayerRecursive(characterModel, targetLayer);
            }

            _isTransformed = false;

            if (showDebug)
                Debug.Log("[PropTransform] Returned to human form");
        }
        else if (propIndex < propOptions.Count)
        {
            // Prop form
            if (characterModel != null)
                characterModel.SetActive(false);

            propOptions[propIndex].SetActive(true);
            SetLayerRecursive(propOptions[propIndex], targetLayer);

            _isTransformed = true;

            if (showDebug)
                Debug.Log($"[PropTransform] Now disguised as: {propOptions[propIndex].name}");
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

    private void SetAllPropsActive(bool active)
    {
        foreach (var prop in propOptions)
        {
            if (prop != null)
                prop.SetActive(active);
        }
    }

    // Public properties
    public bool IsTransformed => _isTransformed;
    public int CurrentPropIndex => _currentPropIndex.value;
    public int TransformsRemaining => _transformsRemaining.value;
    public int DecoysRemaining => _decoysRemaining.value;
    public bool CanTransform => playerTeam != null && playerTeam.CurrentTeam == Team.Props;
    public List<GameObject> PropOptions => propOptions;
}