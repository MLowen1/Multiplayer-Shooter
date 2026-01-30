using PurrNet;
using PurrNet.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LobbySceneUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI roomCodeText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveButton;

    [Header("Settings")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool _isHost;
    private int _lastPlayerCount;

    private void Start()
    {
        _isHost = PlayerPrefs.GetString("IsHost", "0").Equals("1");
        string roomCode = PlayerPrefs.GetString("RoomCode", "?????");

        if (roomCodeText != null)
            roomCodeText.text = "Room Code: " + roomCode;

        if (startGameButton != null)
            startGameButton.gameObject.SetActive(_isHost);

        startGameButton?.onClick.AddListener(OnStartGameClicked);
        leaveButton?.onClick.AddListener(OnLeaveClicked);

        // Subscribe to player events with correct signatures
        if (NetworkManager.main != null)
        {
            NetworkManager.main.onPlayerJoined += OnPlayerJoined;
            NetworkManager.main.onPlayerLeft += OnPlayerLeft;
        }

        UpdatePlayerCount();

        if (_isHost)
            SetStatus("You are the host. Start when ready!");
        else
            SetStatus("Waiting for host to start...");
    }

    private void Update()
    {
        // Poll for disconnection
        if (NetworkManager.main == null || (!NetworkManager.main.isClient && !NetworkManager.main.isServer))
        {
            SetStatus("Disconnected!");
            Invoke(nameof(ReturnToMainMenu), 1f);
            enabled = false; // Stop polling
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.main != null)
        {
            NetworkManager.main.onPlayerJoined -= OnPlayerJoined;
            NetworkManager.main.onPlayerLeft -= OnPlayerLeft;
        }
    }

    // Correct signature: (PlayerID player, bool isReconnect, bool asServer)
    private void OnPlayerJoined(PlayerID player, bool isReconnect, bool asServer)
    {
        UpdatePlayerCount();
        Debug.Log("Player joined: " + player.ToString());
    }

    // Correct signature: (PlayerID player, bool asServer)
    private void OnPlayerLeft(PlayerID player, bool asServer)
    {
        UpdatePlayerCount();
        Debug.Log("Player left: " + player.ToString());
    }

    private void UpdatePlayerCount()
    {
        int count = 1; // Default to at least 1 (us)

        // Try to get player count from PlayersManager
        var playerModule = NetworkManager.main?.playerModule;
        if (playerModule != null)
        {
            // Try different property names for player count
            var type = playerModule.GetType();
            var countProp = type.GetProperty("count") ?? type.GetProperty("Count") ?? type.GetProperty("playerCount");
            if (countProp != null)
            {
                count = (int)countProp.GetValue(playerModule);
            }
            else
            {
                // Try to get players collection and count it
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

        if (playerCountText != null)
            playerCountText.text = "Players: " + count;

        _lastPlayerCount = count;
    }

    private void OnStartGameClicked()
    {
        if (!_isHost) return;

        SetStatus("Starting game...");

        // Use PurrNet's scene module to load game scene for all clients
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

    public void CopyRoomCode()
    {
        string code = PlayerPrefs.GetString("RoomCode", "");
        if (!string.IsNullOrEmpty(code))
        {
            GUIUtility.systemCopyBuffer = code;
            SetStatus("Code copied!");
        }
    }
}