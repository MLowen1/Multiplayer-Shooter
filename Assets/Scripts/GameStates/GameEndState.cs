using System.Collections.Generic;
using System.Linq;
using PurrNet;
using PurrNet.StateMachine;
using UnityEngine;

public class GameEndState : StateNode
{
    public override void Enter(bool asServer)
    {
        base.Enter(asServer);

        Debug.Log($"Game has now ended!");
    }
}
