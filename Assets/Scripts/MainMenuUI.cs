using PurrNet;
using PurrNet.Transports;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Settings")]
    [SerializeField] private string lobbySceneName = "LobbyScene";
    [SerializeField] private int codeLength = 5;

    private Component _purrTransport;
    private PropertyInfo _roomProperty;
    private bool _isConnecting;

    private void Start()
    {
        // Find PurrTransport using reflection (since exact type may vary)
        if (NetworkManager.main != null && NetworkManager.main.transport != null)
        {
            _purrTransport = NetworkManager.main.transport.GetComponentInChildren<ITransport>() as Component;

            // Try to find the room property
            if (_purrTransport != null)
            {
                var type = _purrTransport.GetType();
                _roomProperty = type.GetProperty("room") ?? type.GetProperty("Room") ?? type.GetProperty("roomName");
            }
        }

        if (_purrTransport == null)
        {
            SetStatus("Error: Transport not found!");
            return;
        }

        hostButton?.onClick.AddListener(OnHostClicked);
        joinButton?.onClick.AddListener(OnJoinClicked);

        SetStatus("Ready");
    }

    private void Update()
    {
        // Poll connection state instead of using events
        if (_isConnecting && NetworkManager.main != null)
        {
            if (NetworkManager.main.isClient && NetworkManager.main.isServer)
            {
                // Host is fully connected
                _isConnecting = false;
                OnConnectedAsHost();
            }
            else if (NetworkManager.main.isClient && !PlayerPrefs.GetString("IsHost", "0").Equals("1"))
            {
                // Client is connected
                _isConnecting = false;
                OnConnectedAsClient();
            }
        }
    }

    private void OnHostClicked()
    {
        string roomCode = GenerateRoomCode();

        // Try to set room via reflection
        if (_roomProperty != null)
        {
            _roomProperty.SetValue(_purrTransport, roomCode);
        }
        else
        {
            // Try field instead
            var field = _purrTransport.GetType().GetField("room", BindingFlags.Public | BindingFlags.Instance)
                     ?? _purrTransport.GetType().GetField("_room", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(_purrTransport, roomCode);
            }
        }

        PlayerPrefs.SetString("IsHost", "1");
        PlayerPrefs.SetString("RoomCode", roomCode);

        SetStatus("Creating room " + roomCode + "...");

        _isConnecting = true;
        NetworkManager.main.StartServer();
        NetworkManager.main.StartClient();
    }

    private void OnJoinClicked()
    {
        string roomCode = joinCodeInput?.text?.ToUpper().Trim();

        if (string.IsNullOrEmpty(roomCode))
        {
            SetStatus("Enter a room code!");
            return;
        }

        // Try to set room via reflection
        if (_roomProperty != null)
        {
            _roomProperty.SetValue(_purrTransport, roomCode);
        }
        else
        {
            var field = _purrTransport.GetType().GetField("room", BindingFlags.Public | BindingFlags.Instance)
                     ?? _purrTransport.GetType().GetField("_room", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(_purrTransport, roomCode);
            }
        }

        PlayerPrefs.SetString("IsHost", "0");
        PlayerPrefs.SetString("RoomCode", roomCode);

        SetStatus("Joining room " + roomCode + "...");

        _isConnecting = true;
        NetworkManager.main.StartClient();
    }

    private void OnConnectedAsHost()
    {
        SetStatus("Connected! Loading lobby...");
        LoadLobbyScene();
    }

    private void OnConnectedAsClient()
    {
        SetStatus("Connected! Waiting for scene...");
        // Scene will be loaded by host via PurrNet's scene sync
    }

    private void LoadLobbyScene()
    {
        // Use PurrNet's scene module to load scene for all clients
        var sceneModule = NetworkManager.main.sceneModule;
        if (sceneModule != null)
        {
            // LoadSceneAsync is the correct method
            sceneModule.LoadSceneAsync(lobbySceneName);
        }
        else
        {
            Debug.LogError("SceneModule not found!");
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
        Debug.Log("[MainMenu] " + message);
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        char[] code = new char[codeLength];
        for (int i = 0; i < codeLength; i++)
            code[i] = chars[Random.Range(0, chars.Length)];
        return new string(code);
    }
}