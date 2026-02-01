using UnityEngine;
using UnityEngine.UI;
using PurrNet;

public class CrosshairUI : MonoBehaviour
{
    [Header("Crosshair Settings")]
    [SerializeField] private Image crosshairImage;
    [SerializeField] private float size = 20f;
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField] private float thickness = 2f;
    [SerializeField] private float gap = 5f;

    [Header("Dynamic Crosshair")]
    [SerializeField] private bool useDynamicCrosshair = false;
    [SerializeField] private float expandAmount = 5f;
    [SerializeField] private float expandSpeed = 10f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private RectTransform _rectTransform;
    private PlayerTeam _localPlayerTeam;
    private bool _isSeeker = false;
    private float _currentExpand = 0f;

    private void Awake()
    {
        if (crosshairImage != null)
        {
            _rectTransform = crosshairImage.GetComponent<RectTransform>();
        }

        // Initially hide crosshair until we know the player's team
        SetCrosshairVisible(false);
    }

    private void Start()
    {
        // Try to find local player's team
        StartCoroutine(WaitForLocalPlayer());
    }

    private System.Collections.IEnumerator WaitForLocalPlayer()
    {
        // Wait until we find the local player
        while (_localPlayerTeam == null)
        {
            var allPlayers = FindObjectsByType<PlayerTeam>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                var identity = player.GetComponent<NetworkIdentity>();
                if (identity != null && identity.isOwner)
                {
                    _localPlayerTeam = player;

                    // Subscribe to team changes
                    UpdateCrosshairVisibility();

                    if (showDebug)
                        Debug.Log($"[Crosshair] Found local player. Team: {_localPlayerTeam.CurrentTeam}");

                    yield break;
                }
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    private void Update()
    {
        // Keep checking for team changes (in case player spawns as different team)
        if (_localPlayerTeam != null)
        {
            bool shouldBeSeeker = (_localPlayerTeam.CurrentTeam == Team.Seekers);
            if (shouldBeSeeker != _isSeeker)
            {
                UpdateCrosshairVisibility();
            }
        }
        else
        {
            // Try to find local player again if lost (e.g., respawn)
            TryFindLocalPlayer();
        }

        // Dynamic crosshair expansion when shooting
        if (useDynamicCrosshair && _isSeeker)
        {
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                _currentExpand = expandAmount;
            }

            _currentExpand = Mathf.Lerp(_currentExpand, 0f, Time.deltaTime * expandSpeed);
            UpdateCrosshairSize();
        }
    }

    private void TryFindLocalPlayer()
    {
        var allPlayers = FindObjectsByType<PlayerTeam>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            var identity = player.GetComponent<NetworkIdentity>();
            if (identity != null && identity.isOwner)
            {
                _localPlayerTeam = player;
                UpdateCrosshairVisibility();
                return;
            }
        }
    }

    private void UpdateCrosshairVisibility()
    {
        if (_localPlayerTeam == null)
        {
            SetCrosshairVisible(false);
            return;
        }

        _isSeeker = (_localPlayerTeam.CurrentTeam == Team.Seekers);
        SetCrosshairVisible(_isSeeker);

        if (showDebug)
            Debug.Log($"[Crosshair] Visibility updated. IsSeeker: {_isSeeker}");
    }

    private void SetCrosshairVisible(bool visible)
    {
        if (crosshairImage != null)
        {
            crosshairImage.gameObject.SetActive(visible);
        }
    }

    private void UpdateCrosshairSize()
    {
        if (_rectTransform != null)
        {
            float currentSize = size + _currentExpand;
            _rectTransform.sizeDelta = new Vector2(currentSize, currentSize);
        }
    }

    /// <summary>
    /// Call this to temporarily expand the crosshair (e.g., when shooting)
    /// </summary>
    public void ExpandCrosshair()
    {
        if (useDynamicCrosshair)
        {
            _currentExpand = expandAmount;
        }
    }
}