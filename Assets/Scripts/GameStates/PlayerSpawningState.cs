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

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    public override void Enter(bool asServer)
    {
        base.Enter(asServer);

        // Only server spawns players
        if (!asServer)
            return;

        if (showDebug)
            Debug.Log("[PlayerSpawning] Starting spawn process...");

        // Clean up any existing players and decoys
        DespawnPlayers();
        DespawnDecoys();

        // Get all connected players
        var players = new List<PlayerID>(networkManager.players);

        if (players.Count == 0)
        {
            Debug.LogError("[PlayerSpawning] No players to spawn!");
            return;
        }

        // Assign teams
        var teamAssignments = AssignTeams(players);

        // Spawn players with their teams
        var spawnedPlayers = SpawnPlayersWithTeams(teamAssignments);

        if (showDebug)
        {
            int propCount = 0, seekerCount = 0;
            foreach (var team in teamAssignments.Values)
            {
                if (team == Team.Props) propCount++;
                else if (team == Team.Seekers) seekerCount++;
            }
            Debug.Log($"[PlayerSpawning] Spawned {spawnedPlayers.Count} players: {propCount} Props, {seekerCount} Seekers");
        }

        // Start the round (hiding phase)
        if (InstanceHandler.TryGetInstance(out RoundManager roundManager))
        {
            // If this is round 1, call StartGame, otherwise just start hiding phase
            if (roundManager.CurrentRound <= 1)
            {
                roundManager.StartGame();
            }
            else
            {
                roundManager.StartHidingPhase();
            }
        }

        // Move to next state with the spawned players
        machine.Next(spawnedPlayers);
    }

    private Dictionary<PlayerID, Team> AssignTeams(List<PlayerID> players)
    {
        var assignments = new Dictionary<PlayerID, Team>();

        // Get previous seekers from RoundManager (if available)
        List<PlayerID> previousSeekers = new();
        if (InstanceHandler.TryGetInstance(out RoundManager roundManager))
        {
            previousSeekers = roundManager.GetPreviousSeekers();
            if (showDebug && previousSeekers.Count > 0)
                Debug.Log($"[PlayerSpawning] Previous seekers: {previousSeekers.Count}");
        }

        // Create a copy to shuffle if needed
        var playerList = new List<PlayerID>(players);

        if (randomizeTeams)
        {
            // Fisher-Yates shuffle
            for (int i = playerList.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (playerList[i], playerList[j]) = (playerList[j], playerList[i]);
            }
        }

        // Calculate how many should be props
        int propCount = Mathf.Max(1, Mathf.RoundToInt(playerList.Count * propPercentage));

        // Ensure at least 1 seeker if more than 1 player
        if (playerList.Count > 1 && propCount >= playerList.Count)
            propCount = playerList.Count - 1;

        int seekerCount = playerList.Count - propCount;

        // Separate players into those who were seekers last round and those who weren't
        var canBeSeeker = new List<PlayerID>();
        var mustBeProp = new List<PlayerID>(); // Previous seekers should be props this round

        foreach (var player in playerList)
        {
            if (previousSeekers.Contains(player))
            {
                mustBeProp.Add(player);
            }
            else
            {
                canBeSeeker.Add(player);
            }
        }

        // Assign teams
        // First, assign previous seekers as props
        foreach (var player in mustBeProp)
        {
            assignments[player] = Team.Props;
            if (showDebug)
                Debug.Log($"[PlayerSpawning] Assigned {player} to Props (was seeker last round)");
        }

        // Then assign from the remaining players
        int propsAssigned = mustBeProp.Count;
        int seekersAssigned = 0;

        foreach (var player in canBeSeeker)
        {
            Team team;

            // Do we still need more props?
            if (propsAssigned < propCount)
            {
                team = Team.Props;
                propsAssigned++;
            }
            // Do we still need seekers?
            else if (seekersAssigned < seekerCount)
            {
                team = Team.Seekers;
                seekersAssigned++;
            }
            // Fallback to props if something went wrong
            else
            {
                team = Team.Props;
            }

            assignments[player] = team;

            if (showDebug)
                Debug.Log($"[PlayerSpawning] Assigned {player} to team {team}");
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

            // Get appropriate spawn point based on team
            Transform spawnPoint = GetSpawnPoint(team, ref propSpawnIndex, ref seekerSpawnIndex);

            if (spawnPoint == null)
            {
                Debug.LogError($"[PlayerSpawning] No spawn point available for {playerID}!");
                continue;
            }

            // Spawn the player
            var newPlayer = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
            newPlayer.GiveOwnership(playerID);

            // Set the player's team
            var playerTeam = newPlayer.GetComponent<PlayerTeam>();
            if (playerTeam != null)
            {
                playerTeam.SetTeam(team);
            }
            else
            {
                Debug.LogWarning($"[PlayerSpawning] PlayerTeam component not found on player prefab!");
            }

            spawnedPlayers.Add(newPlayer);

            if (showDebug)
                Debug.Log($"[PlayerSpawning] Spawned {playerID} as {team} at {spawnPoint.name}");
        }

        return spawnedPlayers;
    }

    private Transform GetSpawnPoint(Team team, ref int propIndex, ref int seekerIndex)
    {
        // Try to use team-specific spawn points first
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

        // Fallback: use any available spawn point
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
        var allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var player in allPlayers)
        {
            Destroy(player.gameObject);
        }

        if (showDebug && allPlayers.Length > 0)
            Debug.Log($"[PlayerSpawning] Despawned {allPlayers.Length} existing players");
    }

    private void DespawnDecoys()
    {
        var allDecoys = FindObjectsByType<PropDecoy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var decoy in allDecoys)
        {
            Destroy(decoy.gameObject);
        }

        if (showDebug && allDecoys.Length > 0)
            Debug.Log($"[PlayerSpawning] Despawned {allDecoys.Length} existing decoys");
    }

    public override void Exit(bool asServer)
    {
        base.Exit(asServer);
    }
}