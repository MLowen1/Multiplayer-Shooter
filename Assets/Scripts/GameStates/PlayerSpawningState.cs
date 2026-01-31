using System.Collections.Generic;
using PurrNet.StateMachine;
using UnityEngine;

public class PlayerSpawningState : StateNode
{
    [SerializeField] private PlayerHealth playerPrefab;
    [SerializeField] private List<Transform> spawnPoints = new();

    // When we enter this state we want to go through all of the players in the game and we want to spawn a player prefab for each of them. 
    public override void Enter(bool asServer)
    {
        base.Enter(asServer);

        // If we are not running as a server then return.
        if (!asServer)
            return;

        var spawnedPlayers = new List<PlayerHealth>();

        int currentSpawnIndex = 0;

        foreach (var player in networkManager.players)
        {
            var spawnPoint = spawnPoints[currentSpawnIndex];
            var newPlayer = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
            newPlayer.GiveOwnership(player);
            spawnedPlayers.Add(newPlayer);
            currentSpawnIndex++;

            if (currentSpawnIndex >= spawnPoints.Count)
                currentSpawnIndex = 0;
        }

       //machine.Next(spawnedPlayers);
    }

    public override void Exit(bool asServer)
    {
        base.Exit(asServer);
    }
}
