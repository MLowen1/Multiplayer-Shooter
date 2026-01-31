using System.Collections.Generic;
using PurrNet;
using PurrNet.Modules;
using PurrNet.StateMachine;
using UnityEngine;

public class PlayerSpawningState : StateNode
{
    [SerializeField] private PlayerHealth playerPrefab;
    [SerializeField] private List<Transform> spawnPoints = new();

    public override void Enter(bool asServer)
    {
        base.Enter(asServer);

        // Only server spawns players
        if (!asServer)
            return;

        // Get scene modules
        if (!networkManager.TryGetModule(out ScenesModule scenesModule, true))
        {
            Debug.LogError("ScenesModule not found!");
            return;
        }

        if (!networkManager.TryGetModule(out ScenePlayersModule scenePlayersModule, true))
        {
            Debug.LogError("ScenePlayersModule not found!");
            return;
        }

        // Get current scene ID
        if (!scenesModule.TryGetSceneID(gameObject.scene, out var sceneID))
        {
            Debug.LogError("Could not get SceneID for current scene!");
            return;
        }

        // Get players in THIS scene
        if (!scenePlayersModule.TryGetPlayersInScene(sceneID, out var playersInScene))
        {
            Debug.LogError("Could not get players in scene!");
            return;
        }

        Debug.Log($"[PlayerSpawning] Spawning {playersInScene.Count} players...");

        DespawnPlayers();

        var spawnedPlayers = SpawnPlayers();

        Debug.Log($"[PlayerSpawning] All {spawnedPlayers.Count} players spawned!");

        // Move to next state with the spawned players
        machine.Next(spawnedPlayers);
    }

    private List<PlayerHealth> SpawnPlayers()
    {
        var spawnedPlayers = new List<PlayerHealth>();
        int currentSpawnIndex = 0;

        foreach (var player in networkManager.players)
        {
            var spawnPoint = spawnPoints[currentSpawnIndex];
            var newPlayer = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
            newPlayer.GiveOwnership(player);
            spawnedPlayers.Add(newPlayer);

            Debug.Log($"[PlayerSpawning] Spawned player for {player}");

            currentSpawnIndex++;
            if (currentSpawnIndex >= spawnPoints.Count)
                currentSpawnIndex = 0;
        }

        return spawnedPlayers;
    }

    private void DespawnPlayers()
    {
        var allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            Destroy(player.gameObject);
        }
    }

    public override void Exit(bool asServer)
    {
        base.Exit(asServer);
    }
}