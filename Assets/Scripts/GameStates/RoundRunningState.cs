using System.Collections.Generic;
using PurrNet.StateMachine;
using UnityEngine;

public class RoundRunningState : StateNode<List<PlayerHealth>>
{
    private int _playersAlive;

    public override void Enter(List<PlayerHealth> data, bool asServer)
    {
        base.Enter(data, asServer);


        if(!asServer)
            return;

        _playersAlive = data.Count;
        foreach (var player in data)
        {
            player.OnDeath_Server += OnPlayerDeath;
        }
    }

    private void OnPlayerDeath(PlayerHealth deadPlayer)
    {
        deadPlayer.OnDeath_Server -= OnPlayerDeath;
        _playersAlive--;

        if(_playersAlive <= 1)
        {
            Debug.Log($"Someone won the round!");
        }
    }
}
