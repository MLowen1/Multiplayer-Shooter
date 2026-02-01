using System.Collections.Generic;
using UnityEngine;
using TMPro;
using PurrNet;

public class ScoreboardUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject scoreboardPanel;
    [SerializeField] private Transform propsContainer;
    [SerializeField] private Transform seekersContainer;
    [SerializeField] private ScoreboardEntry entryPrefab;

    [Header("Team Headers")]
    [SerializeField] private TMP_Text propsHeaderText;
    [SerializeField] private TMP_Text seekersHeaderText;

    [Header("Settings")]
    [SerializeField] private KeyCode scoreboardKey = KeyCode.Tab;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private List<ScoreboardEntry> _propsEntries = new();
    private List<ScoreboardEntry> _seekersEntries = new();
    private bool _isVisible = false;

    private NetworkManager _networkManager;

    private void Start()
    {
        // Hide scoreboard initially
        if (scoreboardPanel != null)
            scoreboardPanel.SetActive(false);

        // Find NetworkManager
        _networkManager = FindFirstObjectByType<NetworkManager>();
    }

    private void Update()
    {
        // Show scoreboard while TAB is held
        if (Input.GetKeyDown(scoreboardKey))
        {
            ShowScoreboard();
        }
        else if (Input.GetKeyUp(scoreboardKey))
        {
            HideScoreboard();
        }

        // Refresh while visible
        if (_isVisible)
        {
            RefreshScoreboard();
        }
    }

    private void ShowScoreboard()
    {
        if (scoreboardPanel == null) return;

        _isVisible = true;
        scoreboardPanel.SetActive(true);
        RefreshScoreboard();

        if (showDebug)
            Debug.Log("[ScoreboardUI] Showing scoreboard");
    }

    private void HideScoreboard()
    {
        if (scoreboardPanel == null) return;

        _isVisible = false;
        scoreboardPanel.SetActive(false);

        if (showDebug)
            Debug.Log("[ScoreboardUI] Hiding scoreboard");
    }

    private void RefreshScoreboard()
    {
        // Clear existing entries
        ClearEntries();

        // Find all players in the game
        var allPlayers = FindObjectsByType<PlayerTeam>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        // Get local player ID for highlighting
        PlayerID? localPlayerID = _networkManager?.localPlayer;

        // Separate players by team
        List<PlayerTeam> props = new();
        List<PlayerTeam> seekers = new();

        foreach (var player in allPlayers)
        {
            if (player.CurrentTeam == Team.Props)
                props.Add(player);
            else if (player.CurrentTeam == Team.Seekers)
                seekers.Add(player);
        }

        // Update headers with player counts
        if (propsHeaderText != null)
            propsHeaderText.text = $"PROPS ({props.Count})";

        if (seekersHeaderText != null)
            seekersHeaderText.text = $"SEEKERS ({seekers.Count})";

        // Create entries for Props team
        foreach (var player in props)
        {
            CreateEntry(player, propsContainer, _propsEntries, localPlayerID);
        }

        // Create entries for Seekers team
        foreach (var player in seekers)
        {
            CreateEntry(player, seekersContainer, _seekersEntries, localPlayerID);
        }
    }

    private void CreateEntry(PlayerTeam player, Transform container, List<ScoreboardEntry> entryList, PlayerID? localPlayerID)
    {
        if (entryPrefab == null || container == null) return;

        // Get player info
        var networkIdentity = player.GetComponent<NetworkIdentity>();
        if (networkIdentity == null || !networkIdentity.owner.HasValue) return;

        PlayerID playerID = networkIdentity.owner.Value;

        // Get player name (use ID for now, can be replaced with actual names later)
        string playerName = $"Player {playerID}";

        // Get score data from ScoreManager
        int score = 0;
        int kills = 0;
        int deaths = 0;

        if (InstanceHandler.TryGetInstance(out ScoreManager scoreManager))
        {
            var scoreData = scoreManager.GetScoreData(playerID);
            kills = scoreData.kills;
            deaths = scoreData.deaths;
            score = scoreData.score;
        }

        // Check if this is the local player
        bool isLocalPlayer = localPlayerID.HasValue && localPlayerID.Value.Equals(playerID);

        // Create entry
        var entry = Instantiate(entryPrefab, container);
        entry.Setup(playerID, playerName, score, kills, deaths, isLocalPlayer);
        entryList.Add(entry);
    }

    private void ClearEntries()
    {
        foreach (var entry in _propsEntries)
        {
            if (entry != null)
                Destroy(entry.gameObject);
        }
        _propsEntries.Clear();

        foreach (var entry in _seekersEntries)
        {
            if (entry != null)
                Destroy(entry.gameObject);
        }
        _seekersEntries.Clear();
    }

    private void OnDestroy()
    {
        ClearEntries();
    }
}