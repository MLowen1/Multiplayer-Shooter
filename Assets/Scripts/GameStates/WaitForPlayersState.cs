using System.Collections;
using PurrNet;
using PurrNet.Modules;
using PurrNet.StateMachine;
using UnityEngine;

public class WaitForPlayersState : StateNode
{
    [SerializeField] private int minPlayers = 2;
    [SerializeField] private float waitAfterAllLoaded = 1f; // Extra wait to ensure everyone is ready

    private ScenePlayersModule _scenePlayersModule;
    private SceneID _currentSceneID;

    public override void Enter(bool asServer)
    {
        base.Enter(asServer);

        // Only server controls game flow
        if (!asServer)
            return;

        StartCoroutine(WaitForPlayersToLoadScene());
    }

    private IEnumerator WaitForPlayersToLoadScene()
    {
        // Get the scene module
        if (!networkManager.TryGetModule(out ScenesModule scenesModule, true))
        {
            Debug.LogError("ScenesModule not found!");
            yield break;
        }

        // Get scene players module
        if (!networkManager.TryGetModule(out ScenePlayersModule scenePlayersModule, true))
        {
            Debug.LogError("ScenePlayersModule not found!");
            yield break;
        }

        _scenePlayersModule = scenePlayersModule;

        // Get the current scene ID
        if (!scenesModule.TryGetSceneID(gameObject.scene, out _currentSceneID))
        {
            Debug.LogError("Could not get SceneID for current scene!");
            yield break;
        }

        Debug.Log($"[WaitForPlayers] Waiting for {minPlayers} players to load into scene...");

        // Wait for minimum players to load into THIS scene
        while (GetPlayersInScene() < minPlayers)
        {
            Debug.Log($"[WaitForPlayers] Players in scene: {GetPlayersInScene()}/{minPlayers}");
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log($"[WaitForPlayers] All {GetPlayersInScene()} players loaded! Waiting {waitAfterAllLoaded}s for safety...");

        // Extra wait to ensure all clients are fully ready
        yield return new WaitForSeconds(waitAfterAllLoaded);

        Debug.Log("[WaitForPlayers] Starting game!");
        machine.Next();
    }

    private int GetPlayersInScene()
    {
        if (_scenePlayersModule == null)
            return 0;

        if (_scenePlayersModule.TryGetPlayersInScene(_currentSceneID, out var players))
        {
            return players.Count;
        }

        return 0;
    }

    public override void Exit(bool asServer)
    {
        base.Exit(asServer);
        StopAllCoroutines();
    }
}