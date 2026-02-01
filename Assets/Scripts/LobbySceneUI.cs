using PurrNet;
using PurrNet.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LobbySceneUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI connectionInfoText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveButton;

    [Header("Round Settings (Host Only)")]
    [SerializeField] private GameObject roundSettingsPanel;
    [SerializeField] private TextMeshProUGUI roundCountText;
    [SerializeField] private Button decreaseRoundsButton;
    [SerializeField] private Button increaseRoundsButton;
    [SerializeField] private int minRounds = 1;
    [SerializeField] private int maxRounds = 10;
    [SerializeField] private int defaultRounds = 3;

    [Header("Player Limit")]
    [SerializeField] private int maxPlayers = 10;

    [Header("Settings")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool _isHost;
    private string _connectionMode;
    private int _currentRounds;

    private void Start()
    {
        // IMPORTANT: Unlock and show cursor when entering lobby
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Detect host status - check NetworkManager first (for returning from game), 
        // then fall back to PlayerPrefs (for first time entering lobby)
        if (NetworkManager.main != null && NetworkManager.main.isServer)
        {
            _isHost = true;
            // Update PlayerPrefs to keep it in sync
            PlayerPrefs.SetString("IsHost", "1");
        }
        else
        {
            _isHost = PlayerPrefs.GetString("IsHost", "0").Equals("1");
        }

        string roomCode = PlayerPrefs.GetString("RoomCode", "?????");
        _connectionMode = PlayerPrefs.GetString("ConnectionMode", "Online");

        // Initialize round count (load previous setting if available)
        _currentRounds = PlayerPrefs.GetInt("TotalRounds", defaultRounds);

        // Display connection info based on mode
        if (connectionInfoText != null)
        {
            if (_connectionMode == "LAN")
            {
                connectionInfoText.text = "LAN Mode\nHost IP: " + roomCode;
            }
            else
            {
                connectionInfoText.text = "Online Mode\nRoom Code: " + roomCode;
            }
        }

        // Host-only UI
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(_isHost);

        if (roundSettingsPanel != null)
            roundSettingsPanel.SetActive(_isHost);

        // Button listeners
        startGameButton?.onClick.AddListener(OnStartGameClicked);
        leaveButton?.onClick.AddListener(OnLeaveClicked);
        decreaseRoundsButton?.onClick.AddListener(DecreaseRounds);
        increaseRoundsButton?.onClick.AddListener(IncreaseRounds);

        // Subscribe to player events with correct signatures
        if (NetworkManager.main != null)
        {
            NetworkManager.main.onPlayerJoined += OnPlayerJoined;
            NetworkManager.main.onPlayerLeft += OnPlayerLeft;
        }

        UpdatePlayerCount();
        UpdateRoundCountUI();

        if (_isHost)
            SetStatus("You are the host. Start when ready!");
        else
            SetStatus("Waiting for host to start...");

        Debug.Log($"[Lobby] Started. IsHost: {_isHost}, IsServer: {NetworkManager.main?.isServer}, MaxPlayers: {maxPlayers}");
    }

    private void Update()
    {
        // Poll for disconnection
        if (NetworkManager.main == null || (!NetworkManager.main.isClient && !NetworkManager.main.isServer))
        {
            SetStatus("Disconnected!");
            Invoke(nameof(ReturnToMainMenu), 1f);
            enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.main != null)
        {
            NetworkManager.main.onPlayerJoined -= OnPlayerJoined;
            NetworkManager.main.onPlayerLeft -= OnPlayerLeft;
        }

        startGameButton?.onClick.RemoveListener(OnStartGameClicked);
        leaveButton?.onClick.RemoveListener(OnLeaveClicked);
        decreaseRoundsButton?.onClick.RemoveListener(DecreaseRounds);
        increaseRoundsButton?.onClick.RemoveListener(IncreaseRounds);
    }

    private void OnPlayerJoined(PlayerID player, bool isReconnect, bool asServer)
    {
        Debug.Log($"[Lobby] Player joined: {player}, asServer: {asServer}");

        UpdatePlayerCount();

        // Check if we're over the player limit (only host/server should handle this)
        if (_isHost && asServer)
        {
            int currentCount = GetPlayerCount();

            if (currentCount > maxPlayers)
            {
                Debug.Log($"[Lobby] Player limit reached ({currentCount}/{maxPlayers}). Player {player} cannot join.");
                SetStatus($"Lobby full! ({maxPlayers} max)");

                // Try to disconnect the player who just joined
                try
                {
                    // Try different methods PurrNet might use
                    var nm = NetworkManager.main;

                    // Method 1: Try Disconnect
                    var disconnectMethod = nm.GetType().GetMethod("Disconnect", new[] { typeof(PlayerID) });
                    if (disconnectMethod != null)
                    {
                        disconnectMethod.Invoke(nm, new object[] { player });
                        Debug.Log($"[Lobby] Disconnected player using Disconnect method");
                        return;
                    }

                    // Method 2: Try DisconnectPlayer
                    var disconnectPlayerMethod = nm.GetType().GetMethod("DisconnectPlayer", new[] { typeof(PlayerID) });
                    if (disconnectPlayerMethod != null)
                    {
                        disconnectPlayerMethod.Invoke(nm, new object[] { player });
                        Debug.Log($"[Lobby] Disconnected player using DisconnectPlayer method");
                        return;
                    }

                    // Method 3: Try RemovePlayer
                    var removePlayerMethod = nm.GetType().GetMethod("RemovePlayer", new[] { typeof(PlayerID) });
                    if (removePlayerMethod != null)
                    {
                        removePlayerMethod.Invoke(nm, new object[] { player });
                        Debug.Log($"[Lobby] Disconnected player using RemovePlayer method");
                        return;
                    }

                    Debug.LogWarning($"[Lobby] Could not find method to kick player. Lobby is over capacity!");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Lobby] Error disconnecting player: {e.Message}");
                }
            }
        }
    }

    private void OnPlayerLeft(PlayerID player, bool asServer)
    {
        UpdatePlayerCount();
        Debug.Log("Player left: " + player.ToString());
    }

    private int GetPlayerCount()
    {
        int count = 0;

        var playerModule = NetworkManager.main?.playerModule;
        if (playerModule != null)
        {
            var type = playerModule.GetType();
            var countProp = type.GetProperty("count") ?? type.GetProperty("Count") ?? type.GetProperty("playerCount");
            if (countProp != null)
            {
                count = (int)countProp.GetValue(playerModule);
            }
            else
            {
                var playersProp = type.GetProperty("players") ?? type.GetProperty("Players");
                if (playersProp != null)
                {
                    var players = playersProp.GetValue(playerModule);
                    if (players is System.Collections.ICollection collection)
                    {
                        count = collection.Count;
                    }
                }
            }
        }

        // Fallback - try to get from NetworkManager.main.players
        if (count == 0 && NetworkManager.main != null)
        {
            try
            {
                count = NetworkManager.main.players.Count;
            }
            catch
            {
                count = 1;
            }
        }

        return count;
    }

    private void UpdatePlayerCount()
    {
        int count = GetPlayerCount();

        if (playerCountText != null)
            playerCountText.text = $"Players: {count}/{maxPlayers}";
    }

    #region Round Settings

    private void IncreaseRounds()
    {
        _currentRounds = Mathf.Min(_currentRounds + 1, maxRounds);
        UpdateRoundCountUI();
    }

    private void DecreaseRounds()
    {
        _currentRounds = Mathf.Max(_currentRounds - 1, minRounds);
        UpdateRoundCountUI();
    }

    private void UpdateRoundCountUI()
    {
        if (roundCountText != null)
            roundCountText.text = _currentRounds.ToString();

        if (decreaseRoundsButton != null)
            decreaseRoundsButton.interactable = (_currentRounds > minRounds);

        if (increaseRoundsButton != null)
            increaseRoundsButton.interactable = (_currentRounds < maxRounds);
    }

    private void SaveRoundSettings()
    {
        // Save to PlayerPrefs so game scene can read it
        PlayerPrefs.SetInt("TotalRounds", _currentRounds);
        PlayerPrefs.Save();

        Debug.Log($"[Lobby] Saved round count: {_currentRounds}");
    }

    #endregion

    private void OnStartGameClicked()
    {
        if (!_isHost) return;

        // Save round settings before loading game
        SaveRoundSettings();

        SetStatus("Starting game...");

        var sceneModule = NetworkManager.main?.sceneModule;
        if (sceneModule != null)
        {
            sceneModule.LoadSceneAsync(gameSceneName);
        }
        else
        {
            Debug.LogError("SceneModule not found!");
        }
    }

    private void OnLeaveClicked()
    {
        if (NetworkManager.main != null)
        {
            if (NetworkManager.main.isServer)
                NetworkManager.main.StopServer();

            if (NetworkManager.main.isClient)
                NetworkManager.main.StopClient();
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void ReturnToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
        Debug.Log("[Lobby] " + message);
    }

    public void CopyConnectionInfo()
    {
        string info = PlayerPrefs.GetString("RoomCode", "");
        if (!string.IsNullOrEmpty(info))
        {
            GUIUtility.systemCopyBuffer = info;
            SetStatus("Copied to clipboard!");
        }
    }
}