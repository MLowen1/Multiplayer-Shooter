using System.Collections.Generic;
using System.Linq;
using PurrNet;
using PurrNet.StateMachine;
using UnityEngine;

public class GameEndState : StateNode<Dictionary<PlayerID, int>>
{
    public override void Enter(Dictionary<PlayerID, int> roundWins, bool asServer)
    {
        base.Enter(asServer);

        Debug.Log($"Game has now ended!");

        var winner = roundWins.First();

        foreach (var player in roundWins)
        {
            if(player.Value > winner.Value)
                winner = player;
        }

        Debug.Log($"Game has now ended with {winner} being our champion!");
        roundWins.Clear();
    }
}
