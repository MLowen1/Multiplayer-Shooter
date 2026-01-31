using System.Collections;
using System.Collections.Generic;
using PurrNet;
using PurrNet.StateMachine;
using UnityEngine;
using UnityEngine.Experimental.XR.Interaction;

public class RoundEndState : StateNode<PlayerID>
{
    [SerializeField] private int amountOfRounds = 3;
    [SerializeField] private StateNode spawningState;

    private int _roundCount = 0;
    private WaitForSeconds _delay = new(3f);

    private Dictionary<PlayerID, int> _roundWins = new();

    public override void Enter(bool asServer)
    {
        base.Enter(asServer);

        if(!asServer)
            return;

        Debug.Log("Round has ended with no winner!");

        CheckForGameEnd();
    }

    public override void Enter(PlayerID winner, bool asServer)
    {
        base.Enter(asServer);

        if(!asServer)
            return;

        if(!_roundWins.ContainsKey(winner))
            _roundWins.Add(winner, 0);
        _roundWins[winner]++;
        Debug.Log($"{winner} has won the round!");

        CheckForGameEnd();
    }

    private void CheckForGameEnd()
    {
        _roundCount++;
        if (_roundCount > amountOfRounds)
        {
            machine.Next(_roundWins);
            return;
        }

        StartCoroutine(DelayNextState());
    }

    private IEnumerator DelayNextState()
    {
        yield return _delay;
        machine.SetState(spawningState);
    }
}
