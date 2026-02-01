using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PurrNet;

public enum GamePhase
{
    WaitingToStart,
    Hiding,
    Hunting,
    RoundEnd,
    GameOver
}

public class RoundManager : NetworkBehaviour
{
    [Header("Round Settings")]
    [SerializeField] private float hidingPhaseDuration = 30f;
    [SerializeField] private float huntingPhaseDuration = 120f;
    [SerializeField] private float roundEndDuration = 5f;
    [SerializeField] private float survivalPointsInterval = 30f;

    [Header("Game Over Settings")]
    [SerializeField] private float gameOverDuration = 10f;
    [SerializeField] private string returnToSceneName = "LobbyScene";

    [Header("Respawning")]
    [SerializeField] private PlayerHealth playerPrefab;
    [SerializeField] private List<Transform> propSpawnPoints = new();
    [SerializeField] private List<Transform> seekerSpawnPoints = new();
    [SerializeField][Range(0.1f, 0.9f)] private float propPercentage = 0.5f;

    [Header("Scoring")]
    [SerializeField] private int pointsForKill = 100;
    [SerializeField] private int pointsForSurvival = 200;
    [SerializeField] private int pointsPerSurvivalInterval = 25;
    [SerializeField] private int pointsForDecoyTrick = 25;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Synced state
    private SyncVar<GamePhase> _currentPhase = new(GamePhase.WaitingToStart);
    private SyncVar<float> _phaseTimeRemaining = new(0f);
    private SyncVar<int> _currentRound = new(0);
    private SyncVar<int> _totalRounds = new(3);
    private SyncVar<Team> _roundWinner = new(Team.None);
    private SyncVar<float> _countdownTimer = new(0f);

    // Server-side tracking
    private float _nextSurvivalPointsTime;
    private List<PlayerID> _previousSeekers = new();
    private HashSet<PlayerID> _alivePropPlayers = new();

    // Events
    public event Action<GamePhase> OnPhaseChanged;
    public event Action<float> OnTimerUpdated;
    public event Action<Team> OnRoundEnded;
    public event Action<Team> OnGameEnded;
    public event Action<float> OnCountdownUpdated;

    // Public properties
    public GamePhase CurrentPhase => _currentPhase.value;
    public float TimeRemaining => _phaseTimeRemaining.value;
    public int CurrentRound => _currentRound.value;
    public int TotalRounds => _totalRounds.value;
    public Team RoundWinner => _roundWinner.value;
    public float CountdownTimer => _countdownTimer.value;

    private void Awake()
    {
        InstanceHandler.RegisterInstance(this);
    }

    protected override void OnSpawned()
    {
        base.OnSpawned();

        _currentPhase.onChanged += OnPhaseChangedCallback;
        _phaseTimeRemaining.onChanged += OnTimerChangedCallback;
        _countdownTimer.onChanged += OnCountdownChangedCallback;

        // Load round count from PlayerPrefs (set in lobby)
        if (isServer)
        {
            int savedRounds = PlayerPrefs.GetInt("TotalRounds", 3);
            _totalRounds.value = Mathf.Clamp(savedRounds, 1, 10);

            if (showDebug)
                Debug.Log($"[RoundManager] Loaded {_totalRounds.value} rounds from settings");
        }

        if (showDebug)
            Debug.Log($"[RoundManager] Spawned. IsServer: {isServer}");
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _currentPhase.onChanged -= OnPhaseChangedCallback;
        _phaseTimeRemaining.onChanged -= OnTimerChangedCallback;
        _countdownTimer.onChanged -= OnCountdownChangedCallback;
        InstanceHandler.UnregisterInstance<RoundManager>();
    }

    private void OnPhaseChangedCallback(GamePhase newPhase)
    {
        if (showDebug)
            Debug.Log($"[RoundManager] Phase changed to: {newPhase}");

        OnPhaseChanged?.Invoke(newPhase);
    }

    private void OnTimerChangedCallback(float time)
    {
        OnTimerUpdated?.Invoke(time);
    }

    private void OnCountdownChangedCallback(float time)
    {
        OnCountdownUpdated?.Invoke(time);
    }

    private void Update()
    {
        if (!isServer) return;

        if (_currentPhase.value == GamePhase.Hiding || _currentPhase.value == GamePhase.Hunting)
        {
            UpdateTimer();
        }
    }

    private void UpdateTimer()
    {
        _phaseTimeRemaining.value -= Time.deltaTime;

        if (_phaseTimeRemaining.value <= 0)
        {
            if (_currentPhase.value == GamePhase.Hiding)
            {
                StartHuntingPhase();
            }
            else if (_currentPhase.value == GamePhase.Hunting)
            {
                EndRound(Team.Props);
            }
        }

        if (_currentPhase.value == GamePhase.Hunting && Time.time >= _nextSurvivalPointsTime)
        {
            AwardSurvivalIntervalPoints();
            _nextSurvivalPointsTime = Time.time + survivalPointsInterval;
        }
    }

    public void SetTotalRounds(int rounds)
    {
        if (!isServer) return;
        _totalRounds.value = Mathf.Clamp(rounds, 1, 10);

        if (showDebug)
            Debug.Log($"[RoundManager] Total rounds set to: {_totalRounds.value}");
    }

    public void StartGame()
    {
        if (!isServer) return;

        _currentRound.value = 1;
        _previousSeekers.Clear();

        if (showDebug)
            Debug.Log("[RoundManager] Game started!");

        StartHidingPhase();
    }

    public void StartHidingPhase()
    {
        if (!isServer) return;

        _currentPhase.value = GamePhase.Hiding;
        _phaseTimeRemaining.value = hidingPhaseDuration;
        _roundWinner.value = Team.None;
        _countdownTimer.value = 0f;

        TrackAlivePlayers();
        RpcSetSeekersFrozen(true);

        if (showDebug)
            Debug.Log($"[RoundManager] Hiding phase started. Duration: {hidingPhaseDuration}s");
    }

    private void StartHuntingPhase()
    {
        if (!isServer) return;

        _currentPhase.value = GamePhase.Hunting;
        _phaseTimeRemaining.value = huntingPhaseDuration;
        _nextSurvivalPointsTime = Time.time + survivalPointsInterval;

        RpcSetSeekersFrozen(false);

        if (showDebug)
            Debug.Log($"[RoundManager] Hunting phase started. Duration: {huntingPhaseDuration}s");
    }

    private void TrackAlivePlayers()
    {
        _alivePropPlayers.Clear();

        var allPlayers = FindObjectsByType<PlayerTeam>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            if (player.CurrentTeam == Team.Props)
            {
                var identity = player.GetComponent<NetworkIdentity>();
                if (identity != null && identity.owner.HasValue)
                {
                    _alivePropPlayers.Add(identity.owner.Value);
                }
            }
        }

        if (showDebug)
            Debug.Log($"[RoundManager] Tracking {_alivePropPlayers.Count} prop players");
    }

    public void OnPropKilled(PlayerID killerID, PlayerID victimID)
    {
        if (!isServer) return;

        if (showDebug)
            Debug.Log($"[RoundManager] Prop killed! Killer: {killerID}, Victim: {victimID}");

        if (InstanceHandler.TryGetInstance(out ScoreManager scoreManager))
        {
            scoreManager.AddScore(killerID, pointsForKill);
        }

        _alivePropPlayers.Remove(victimID);

        if (_alivePropPlayers.Count == 0)
        {
            EndRound(Team.Seekers);
        }
    }

    public void OnDecoyShot(PlayerID decoyOwnerID)
    {
        if (!isServer) return;

        if (showDebug)
            Debug.Log($"[RoundManager] Decoy shot! Owner: {decoyOwnerID}");

        if (InstanceHandler.TryGetInstance(out ScoreManager scoreManager))
        {
            scoreManager.AddScore(decoyOwnerID, pointsForDecoyTrick);
        }
    }

    private void AwardSurvivalIntervalPoints()
    {
        if (!InstanceHandler.TryGetInstance(out ScoreManager scoreManager)) return;

        foreach (var playerID in _alivePropPlayers)
        {
            scoreManager.AddScore(playerID, pointsPerSurvivalInterval);
        }

        if (showDebug)
            Debug.Log($"[RoundManager] Awarded {pointsPerSurvivalInterval} survival points to {_alivePropPlayers.Count} props");
    }

    private void EndRound(Team winner)
    {
        if (!isServer) return;

        _currentPhase.value = GamePhase.RoundEnd;
        _roundWinner.value = winner;

        if (showDebug)
            Debug.Log($"[RoundManager] Round {_currentRound.value} ended! Winner: {winner}");

        if (winner == Team.Props && InstanceHandler.TryGetInstance(out ScoreManager scoreManager))
        {
            foreach (var playerID in _alivePropPlayers)
            {
                scoreManager.AddScore(playerID, pointsForSurvival);
            }
        }

        StorePreviousSeekers();
        RpcRoundEnded(winner);
        StartCoroutine(HandleRoundEndCountdown());
    }

    private void StorePreviousSeekers()
    {
        _previousSeekers.Clear();

        var allPlayers = FindObjectsByType<PlayerTeam>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            if (player.CurrentTeam == Team.Seekers)
            {
                var identity = player.GetComponent<NetworkIdentity>();
                if (identity != null && identity.owner.HasValue)
                {
                    _previousSeekers.Add(identity.owner.Value);
                }
            }
        }

        if (showDebug)
            Debug.Log($"[RoundManager] Stored {_previousSeekers.Count} previous seekers");
    }

    private IEnumerator HandleRoundEndCountdown()
    {
        float countdown = roundEndDuration;

        while (countdown > 0)
        {
            _countdownTimer.value = countdown;
            yield return new WaitForSeconds(1f);
            countdown -= 1f;
        }

        _countdownTimer.value = 0f;

        if (_currentRound.value >= _totalRounds.value)
        {
            EndGame();
        }
        else
        {
            StartNextRound();
        }
    }

    private void StartNextRound()
    {
        if (!isServer) return;

        _currentRound.value++;

        if (showDebug)
            Debug.Log($"[RoundManager] Starting round {_currentRound.value}");

        // Despawn all existing players and decoys
        DespawnAllPlayers();
        DespawnAllDecoys();

        // Respawn players with new teams
        RespawnPlayers();

        // Start hiding phase
        StartHidingPhase();
    }

    private void DespawnAllPlayers()
    {
        var allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            Destroy(player.gameObject);
        }

        if (showDebug)
            Debug.Log($"[RoundManager] Despawned {allPlayers.Length} players");
    }

    private void DespawnAllDecoys()
    {
        var allDecoys = FindObjectsByType<PropDecoy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var decoy in allDecoys)
        {
            Destroy(decoy.gameObject);
        }

        if (showDebug)
            Debug.Log($"[RoundManager] Despawned {allDecoys.Length} decoys");
    }

    private void RespawnPlayers()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[RoundManager] Player prefab not assigned!");
            return;
        }

        var players = new List<PlayerID>(NetworkManager.main.players);
        var teamAssignments = AssignTeams(players);

        int propSpawnIndex = 0;
        int seekerSpawnIndex = 0;

        foreach (var kvp in teamAssignments)
        {
            PlayerID playerID = kvp.Key;
            Team team = kvp.Value;

            Transform spawnPoint = GetSpawnPoint(team, ref propSpawnIndex, ref seekerSpawnIndex);

            if (spawnPoint == null)
            {
                Debug.LogError($"[RoundManager] No spawn point for {playerID}!");
                continue;
            }

            var newPlayer = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
            newPlayer.GiveOwnership(playerID);

            var playerTeam = newPlayer.GetComponent<PlayerTeam>();
            if (playerTeam != null)
            {
                playerTeam.SetTeam(team);
            }

            if (showDebug)
                Debug.Log($"[RoundManager] Respawned {playerID} as {team}");
        }
    }

    private Dictionary<PlayerID, Team> AssignTeams(List<PlayerID> players)
    {
        var assignments = new Dictionary<PlayerID, Team>();

        // Shuffle players
        for (int i = players.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (players[i], players[j]) = (players[j], players[i]);
        }

        int propCount = Mathf.Max(1, Mathf.RoundToInt(players.Count * propPercentage));
        if (players.Count > 1 && propCount >= players.Count)
            propCount = players.Count - 1;

        int seekerCount = players.Count - propCount;

        var canBeSeeker = new List<PlayerID>();
        var mustBeProp = new List<PlayerID>();

        foreach (var player in players)
        {
            if (_previousSeekers.Contains(player))
                mustBeProp.Add(player);
            else
                canBeSeeker.Add(player);
        }

        foreach (var player in mustBeProp)
        {
            assignments[player] = Team.Props;
        }

        int propsAssigned = mustBeProp.Count;
        int seekersAssigned = 0;

        foreach (var player in canBeSeeker)
        {
            Team team;
            if (propsAssigned < propCount)
            {
                team = Team.Props;
                propsAssigned++;
            }
            else if (seekersAssigned < seekerCount)
            {
                team = Team.Seekers;
                seekersAssigned++;
            }
            else
            {
                team = Team.Props;
            }

            assignments[player] = team;
        }

        return assignments;
    }

    private Transform GetSpawnPoint(Team team, ref int propIndex, ref int seekerIndex)
    {
        if (team == Team.Props && propSpawnPoints.Count > 0)
        {
            var point = propSpawnPoints[propIndex % propSpawnPoints.Count];
            propIndex++;
            return point;
        }
        else if (team == Team.Seekers && seekerSpawnPoints.Count > 0)
        {
            var point = seekerSpawnPoints[seekerIndex % seekerSpawnPoints.Count];
            seekerIndex++;
            return point;
        }

        if (propSpawnPoints.Count > 0)
        {
            var point = propSpawnPoints[propIndex % propSpawnPoints.Count];
            propIndex++;
            return point;
        }

        return null;
    }

    private void EndGame()
    {
        _currentPhase.value = GamePhase.GameOver;

        Team overallWinner = DetermineOverallWinner();

        if (showDebug)
            Debug.Log($"[RoundManager] Game over! Overall winner: {overallWinner}");

        RpcGameEnded(overallWinner);
        StartCoroutine(ReturnToLobbyWithCountdown());
    }

    private IEnumerator ReturnToLobbyWithCountdown()
    {
        float countdown = gameOverDuration;

        while (countdown > 0)
        {
            _countdownTimer.value = countdown;
            yield return new WaitForSeconds(1f);
            countdown -= 1f;
        }

        _countdownTimer.value = 0f;

        if (showDebug)
            Debug.Log($"[RoundManager] Returning to {returnToSceneName}");

        var sceneModule = NetworkManager.main?.sceneModule;
        if (sceneModule != null)
        {
            sceneModule.LoadSceneAsync(returnToSceneName);
        }
        else
        {
            RpcReturnToLobby();
        }
    }

    [ObserversRpc]
    private void RpcReturnToLobby()
    {
        SceneManager.LoadScene(returnToSceneName);
    }

    private Team DetermineOverallWinner()
    {
        return _roundWinner.value;
    }

    public List<PlayerID> GetPreviousSeekers()
    {
        return new List<PlayerID>(_previousSeekers);
    }

    [ObserversRpc]
    private void RpcSetSeekersFrozen(bool frozen)
    {
        var allPlayers = FindObjectsByType<PlayerTeam>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            var identity = player.GetComponent<NetworkIdentity>();
            if (identity != null && identity.isOwner && player.CurrentTeam == Team.Seekers)
            {
                var controller = player.GetComponent<PlayerController>();
                if (controller != null)
                {
                    controller.SetFrozen(frozen);
                }
            }
        }

        if (showDebug)
            Debug.Log($"[RoundManager] Seekers frozen: {frozen}");
    }

    [ObserversRpc]
    private void RpcRoundEnded(Team winner)
    {
        OnRoundEnded?.Invoke(winner);
    }

    [ObserversRpc]
    private void RpcGameEnded(Team winner)
    {
        OnGameEnded?.Invoke(winner);
    }
}