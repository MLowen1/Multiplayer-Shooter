using PurrNet;
using PurrNet.Transports;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System.Net;
using System.Net.Sockets;

public class MainMenuUI : MonoBehaviour
{
    [Header("Mode Selection")]
    [SerializeField] private Button onlineModeButton;
    [SerializeField] private Button lanModeButton;
    [SerializeField] private GameObject modeSelectionPanel;
    [SerializeField] private GameObject onlinePanel;
    [SerializeField] private GameObject lanPanel;

    [Header("Online UI (PurrTransport)")]
    [SerializeField] private Button onlineHostButton;
    [SerializeField] private Button onlineJoinButton;
    [SerializeField] private TMP_InputField onlineCodeInput;
    [SerializeField] private TextMeshProUGUI onlineStatusText;
    [SerializeField] private Button onlineBackButton;

    [Header("LAN UI (UDP)")]
    [SerializeField] private Button lanHostButton;
    [SerializeField] private Button lanJoinButton;
    [SerializeField] private TMP_InputField lanIpInput;
    [SerializeField] private TextMeshProUGUI lanStatusText;
    [SerializeField] private Button lanBackButton;

    [Header("Settings")]
    [SerializeField] private string lobbySceneName = "LobbyScene";
    [SerializeField] private int codeLength = 5;
    [SerializeField] private ushort lanPort = 7777;

    [Header("Transports (Assign in Inspector)")]
    [SerializeField] private PurrTransport purrTransport;
    [SerializeField] private UDPTransport udpTransport;
    

    private PropertyInfo _roomProperty;
    private bool _isConnecting;
    private bool _isLanMode;
    private GenericTransport _genericTransport;
    private FieldInfo _transportField;

    private void Start()
    {
        // Find the GenericTransport on NetworkManager
        if (NetworkManager.main != null)
        {
            _genericTransport = NetworkManager.main.GetComponent<GenericTransport>();

            // Get the private transport field via reflection
            if (_genericTransport != null)
            {
                var type = _genericTransport.GetType();
                _transportField = type.GetField("_transport", BindingFlags.NonPublic | BindingFlags.Instance)
                                ?? type.GetField("transport", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            // Try to find room property on PurrTransport
            if (purrTransport != null)
            {
                var type = purrTransport.GetType();
                _roomProperty = type.GetProperty("room") ?? type.GetProperty("Room") ?? type.GetProperty("roomName");
            }
        }

        // Setup mode selection buttons
        onlineModeButton?.onClick.AddListener(OnOnlineModeSelected);
        lanModeButton?.onClick.AddListener(OnLanModeSelected);

        // Setup online buttons
        onlineHostButton?.onClick.AddListener(OnOnlineHostClicked);
        onlineJoinButton?.onClick.AddListener(OnOnlineJoinClicked);
        onlineBackButton?.onClick.AddListener(OnBackClicked);

        // Setup LAN buttons
        lanHostButton?.onClick.AddListener(OnLanHostClicked);
        lanJoinButton?.onClick.AddListener(OnLanJoinClicked);
        lanBackButton?.onClick.AddListener(OnBackClicked);

        // Show mode selection initially
        ShowModeSelection();

        // Pre-fill LAN IP input with hint
        if (lanIpInput != null)
        {
            lanIpInput.text = "";
        }
    }

    private void Update()
    {
        if (_isConnecting && NetworkManager.main != null)
        {
            bool isHost = PlayerPrefs.GetString("IsHost", "0").Equals("1");

            if (isHost && NetworkManager.main.isClient && NetworkManager.main.isServer)
            {
                _isConnecting = false;
                OnConnectedAsHost();
            }
            else if (!isHost && NetworkManager.main.isClient)
            {
                _isConnecting = false;
                OnConnectedAsClient();
            }
        }
    }

    #region Mode Selection

    private void ShowModeSelection()
    {
        modeSelectionPanel?.SetActive(true);
        onlinePanel?.SetActive(false);
        lanPanel?.SetActive(false);
    }

    private void OnOnlineModeSelected()
    {
        _isLanMode = false;
        modeSelectionPanel?.SetActive(false);
        onlinePanel?.SetActive(true);
        lanPanel?.SetActive(false);
        SetOnlineStatus("Ready - Online Mode");
    }

    private void OnLanModeSelected()
    {
        _isLanMode = true;
        modeSelectionPanel?.SetActive(false);
        onlinePanel?.SetActive(false);
        lanPanel?.SetActive(true);
        SetLanStatus("Ready - LAN Mode\nYour IP: " + GetLocalIPAddress());
    }

    private void OnBackClicked()
    {
        ShowModeSelection();
    }

    #endregion

    #region Online Mode (PurrTransport)

    private void OnOnlineHostClicked()
    {
        if (purrTransport == null)
        {
            SetOnlineStatus("Error: PurrTransport not assigned!");
            return;
        }

        // Switch to PurrTransport
        SetActiveTransport(purrTransport as ITransport);

        string roomCode = GenerateRoomCode();

        // Set room via reflection
        if (_roomProperty != null)
        {
            _roomProperty.SetValue(purrTransport, roomCode);
        }
        else
        {
            var field = purrTransport.GetType().GetField("room", BindingFlags.Public | BindingFlags.Instance)
                     ?? purrTransport.GetType().GetField("_room", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(purrTransport, roomCode);
        }

        PlayerPrefs.SetString("IsHost", "1");
        PlayerPrefs.SetString("RoomCode", roomCode);
        PlayerPrefs.SetString("ConnectionMode", "Online");

        SetOnlineStatus("Creating room " + roomCode + "...");

        _isConnecting = true;
        NetworkManager.main.StartServer();
        NetworkManager.main.StartClient();
    }

    private void OnOnlineJoinClicked()
    {
        if (purrTransport == null)
        {
            SetOnlineStatus("Error: PurrTransport not assigned!");
            return;
        }

        string roomCode = onlineCodeInput?.text?.ToUpper().Trim();

        if (string.IsNullOrEmpty(roomCode))
        {
            SetOnlineStatus("Enter a room code!");
            return;
        }

        // Switch to PurrTransport
        SetActiveTransport(purrTransport as ITransport);

        // Set room via reflection
        if (_roomProperty != null)
        {
            _roomProperty.SetValue(purrTransport, roomCode);
        }
        else
        {
            var field = purrTransport.GetType().GetField("room", BindingFlags.Public | BindingFlags.Instance)
                     ?? purrTransport.GetType().GetField("_room", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(purrTransport, roomCode);
        }

        PlayerPrefs.SetString("IsHost", "0");
        PlayerPrefs.SetString("RoomCode", roomCode);
        PlayerPrefs.SetString("ConnectionMode", "Online");

        SetOnlineStatus("Joining room " + roomCode + "...");

        _isConnecting = true;
        NetworkManager.main.StartClient();
    }

    private void SetOnlineStatus(string message)
    {
        if (onlineStatusText != null)
            onlineStatusText.text = message;
        Debug.Log("[MainMenu-Online] " + message);
    }

    #endregion

    #region LAN Mode (UDP)

    private void OnLanHostClicked()
    {
        if (udpTransport == null)
        {
            SetLanStatus("Error: UDP Transport not assigned!");
            return;
        }

        // Switch to UDP Transport
        SetActiveTransport(udpTransport);

        // Configure UDP transport
        udpTransport.serverPort = lanPort;

        string localIP = GetLocalIPAddress();

        PlayerPrefs.SetString("IsHost", "1");
        PlayerPrefs.SetString("RoomCode", localIP);
        PlayerPrefs.SetString("ConnectionMode", "LAN");

        SetLanStatus("Starting server...\nYour IP: " + localIP + "\nPort: " + lanPort);

        _isConnecting = true;
        NetworkManager.main.StartServer();
        NetworkManager.main.StartClient();
    }

    private void OnLanJoinClicked()
    {
        if (udpTransport == null)
        {
            SetLanStatus("Error: UDP Transport not assigned!");
            return;
        }

        string ipAddress = lanIpInput?.text?.Trim();

        if (string.IsNullOrEmpty(ipAddress))
        {
            SetLanStatus("Enter the host's IP address!");
            return;
        }

        // Switch to UDP Transport
        SetActiveTransport(udpTransport);

        // Configure UDP transport
        udpTransport.address = ipAddress;
        udpTransport.serverPort = lanPort;

        PlayerPrefs.SetString("IsHost", "0");
        PlayerPrefs.SetString("RoomCode", ipAddress);
        PlayerPrefs.SetString("ConnectionMode", "LAN");

        SetLanStatus("Connecting to " + ipAddress + ":" + lanPort + "...");

        _isConnecting = true;
        NetworkManager.main.StartClient();
    }

    private void SetLanStatus(string message)
    {
        if (lanStatusText != null)
            lanStatusText.text = message;
        Debug.Log("[MainMenu-LAN] " + message);
    }

    #endregion

    #region Shared Methods

    private void SetActiveTransport(ITransport transport)
    {
        if (_genericTransport == null || transport == null) return;

        // Try to set transport via reflection since the property is read-only
        if (_transportField != null)
        {
            _transportField.SetValue(_genericTransport, transport);
            Debug.Log("Transport switched to: " + transport.GetType().Name);
        }
        else
        {
            // Fallback: Try to find and set via serialized field name
            var type = _genericTransport.GetType();
            var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (typeof(ITransport).IsAssignableFrom(field.FieldType))
                {
                    field.SetValue(_genericTransport, transport);
                    Debug.Log("Transport switched via fallback to: " + transport.GetType().Name);
                    return;
                }
            }

            Debug.LogWarning("Could not switch transport - field not found");
        }
    }

    private void OnConnectedAsHost()
    {
        string status = "Connected! Loading lobby...";
        if (_isLanMode)
            SetLanStatus(status);
        else
            SetOnlineStatus(status);

        LoadLobbyScene();
    }

    private void OnConnectedAsClient()
    {
        string status = "Connected! Waiting for scene...";
        if (_isLanMode)
            SetLanStatus(status);
        else
            SetOnlineStatus(status);
    }

    private void LoadLobbyScene()
    {
        var sceneModule = NetworkManager.main.sceneModule;
        if (sceneModule != null)
        {
            sceneModule.LoadSceneAsync(lobbySceneName);
        }
        else
        {
            Debug.LogError("SceneModule not found!");
        }
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        char[] code = new char[codeLength];
        for (int i = 0; i < codeLength; i++)
            code[i] = chars[Random.Range(0, chars.Length)];
        return new string(code);
    }

    private string GetLocalIPAddress()
    {
        try
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address.ToString() ?? "Unable to get IP";
            }
        }
        catch
        {
            // Fallback method
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "Unable to get IP";
        }
    }

    #endregion
}