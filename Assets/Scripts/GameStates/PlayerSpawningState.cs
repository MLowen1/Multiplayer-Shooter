using System;
using System.Collections;
using System.Collections.Generic;
using PurrNet;
using PurrNet.Modules;
using PurrNet.StateMachine;
using UnityEngine;

public class PlayerSpawningState : StateNode
{
    [Header("Player Prefab")]
    [SerializeField] private PlayerHealth playerPrefab;

    [Header("Spawn Points")]
    [SerializeField] private List<Transform> propSpawnPoints = new();
    [SerializeField] private List<Transform> seekerSpawnPoints = new();

    [Header("Team Settings")]
    [SerializeField][Range(0.1f, 0.9f)] private float propPercentage = 0.5f;
    [SerializeField] private bool randomizeTeams = true;

    [Header("Spawning")]
    [SerializeField] private float spawnDelay = 0.1f;
    [SerializeField] private bool useStaggeredSpawning = true;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private List<PlayerHealth> _spawnedPlayers = new();

    public override void Enter(bool asServer)
    {
        base.Enter(asServer);

        if (!asServer)
            return;

        if (showDebug)
            Debug.Log("[PlayerSpawning] Starting spawn process...");

        _spawnedPlayers.Clear();

        // Clean up any existing players first
        DespawnPlayers();

        if (useStaggeredSpawning)
        {
            // Use NetworkManager's MonoBehaviour to run coroutine
            if (NetworkManager.main != null)
            {
                NetworkManager.main.StartCoroutine(SpawnPlayersStaggered());
            }
            else
            {
                Debug.LogError("[PlayerSpawning] NetworkManager.main is null! Falling back to synchronous spawning.");
                SpawnPlayersSynchronous();
            }
        }
        else
        {
            SpawnPlayersSynchronous();
        }
    }

    private void SpawnPlayersSynchronous()
    {
        if (showDebug)
            Debug.Log("[PlayerSpawning] Using synchronous spawning...");

        var players = new List<PlayerID>(networkManager.players);

        if (players.Count == 0)
        {
            Debug.LogError("[PlayerSpawning] No players to spawn!");
            machine.Next(new List<PlayerHealth>());
            return;
        }

        var teamAssignments = AssignTeams(players);
        _spawnedPlayers = SpawnPlayersWithTeams(teamAssignments);

        if (showDebug)
        {
            int propCount = 0, seekerCount = 0;
            foreach (var team in teamAssignments.Values)
            {
                if (team == Team.Props) propCount++;
                else if (team == Team.Seekers) seekerCount++;
            }
            Debug.Log($"[PlayerSpawning] Spawned {_spawnedPlayers.Count} players: {propCount} Props, {seekerCount} Seekers");
        }

        // Notify RoundManager to start the game
        NotifyRoundManagerAndProceed();
    }

    private IEnumerator SpawnPlayersStaggered()
    {
        if (showDebug)
            Debug.Log("[PlayerSpawning] Using staggered spawning...");

        // Wait a frame for any cleanup
        yield return null;

        List<PlayerID> players;
        try
        {
            players = new List<PlayerID>(networkManager.players);
            if (showDebug)
                Debug.Log($"[PlayerSpawning] Found {players.Count} players to spawn");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerSpawning] Error getting players: {e.Message}");
            machine.Next(new List<PlayerHealth>());
            yield break;
        }

        if (players.Count == 0)
        {
            Debug.LogError("[PlayerSpawning] No players to spawn!");
            machine.Next(new List<PlayerHealth>());
            yield break;
        }

        Dictionary<PlayerID, Team> teamAssignments;
        try
        {
            teamAssignments = AssignTeams(players);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerSpawning] Error assigning teams: {e.Message}");
            machine.Next(new List<PlayerHealth>());
            yield break;
        }

        int propSpawnIndex = 0;
        int seekerSpawnIndex = 0;
        int spawnedCount = 0;

        foreach (var kvp in teamAssignments)
        {
            PlayerID playerID = kvp.Key;
            Team team = kvp.Value;

            try
            {
                Transform spawnPoint = GetSpawnPoint(team, ref propSpawnIndex, ref seekerSpawnIndex);

                if (spawnPoint == null)
                {
                    Debug.LogError($"[PlayerSpawning] No spawn point for {playerID}!");
                    continue;
                }

                var newPlayer = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);

                if (newPlayer != null)
                {
                    newPlayer.GiveOwnership(playerID);

                    var playerTeam = newPlayer.GetComponent<PlayerTeam>();
                    if (playerTeam != null)
                    {
                        playerTeam.SetTeam(team);
                    }

                    _spawnedPlayers.Add(newPlayer);
                    spawnedCount++;

                    if (showDebug)
                        Debug.Log($"[PlayerSpawning] Spawned {playerID} as {team} ({spawnedCount}/{players.Count})");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerSpawning] Error spawning {playerID}: {e.Message}");
            }

            // Delay between spawns
            if (spawnDelay > 0)
                yield return new WaitForSeconds(spawnDelay);
            else
                yield return null;
        }

        if (showDebug)
            Debug.Log($"[PlayerSpawning] Finished spawning {_spawnedPlayers.Count} players");

        // Small delay for network sync
        yield return new WaitForSeconds(0.3f);

        // Notify RoundManager and proceed
        NotifyRoundManagerAndProceed();
    }

    private void NotifyRoundManagerAndProceed()
    {
        // Tell RoundManager to start the game
        if (InstanceHandler.TryGetInstance(out RoundManager roundManager))
        {
            if (showDebug)
                Debug.Log("[PlayerSpawning] Notifying RoundManager to start game");
            roundManager.StartGame();
        }
        else
        {
            Debug.LogWarning("[PlayerSpawning] RoundManager not found!");
        }

        // Move to next state
        machine.Next(_spawnedPlayers);
    }

    private Dictionary<PlayerID, Team> AssignTeams(List<PlayerID> players)
    {
        var assignments = new Dictionary<PlayerID, Team>();
        var playerList = new List<PlayerID>(players);

        if (randomizeTeams)
        {
            for (int i = playerList.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (playerList[i], playerList[j]) = (playerList[j], playerList[i]);
            }
        }

        int propCount = Mathf.Max(1, Mathf.RoundToInt(playerList.Count * propPercentage));
        if (playerList.Count > 1 && propCount >= playerList.Count)
            propCount = playerList.Count - 1;

        for (int i = 0; i < playerList.Count; i++)
        {
            Team team = (i < propCount) ? Team.Props : Team.Seekers;
            assignments[playerList[i]] = team;

            if (showDebug)
                Debug.Log($"[PlayerSpawning] Assigned {playerList[i]} to {team}");
        }

        return assignments;
    }

    private List<PlayerHealth> SpawnPlayersWithTeams(Dictionary<PlayerID, Team> teamAssignments)
    {
        var spawnedPlayers = new List<PlayerHealth>();
        int propSpawnIndex = 0;
        int seekerSpawnIndex = 0;

        foreach (var kvp in teamAssignments)
        {
            PlayerID playerID = kvp.Key;
            Team team = kvp.Value;

            try
            {
                Transform spawnPoint = GetSpawnPoint(team, ref propSpawnIndex, ref seekerSpawnIndex);

                if (spawnPoint == null)
                {
                    Debug.LogError($"[PlayerSpawning] No spawn point for {playerID}!");
                    continue;
                }

                var newPlayer = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
                newPlayer.GiveOwnership(playerID);

                var playerTeam = newPlayer.GetComponent<PlayerTeam>();
                if (playerTeam != null)
                {
                    playerTeam.SetTeam(team);
                }

                spawnedPlayers.Add(newPlayer);

                if (showDebug)
                    Debug.Log($"[PlayerSpawning] Spawned {playerID} as {team}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerSpawning] Error spawning {playerID}: {e.Message}");
            }
        }

        return spawnedPlayers;
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
        else if (seekerSpawnPoints.Count > 0)
        {
            var point = seekerSpawnPoints[seekerIndex % seekerSpawnPoints.Count];
            seekerIndex++;
            return point;
        }

        return null;
    }

    private void DespawnPlayers()
    {
        try
        {
            var allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                if (player != null && player.gameObject != null)
                {
                    Destroy(player.gameObject);
                }
            }

            if (showDebug && allPlayers.Length > 0)
                Debug.Log($"[PlayerSpawning] Despawned {allPlayers.Length} existing players");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerSpawning] Error despawning: {e.Message}");
        }
    }

    public override void Exit(bool asServer)
    {
        base.Exit(asServer);
    }
}