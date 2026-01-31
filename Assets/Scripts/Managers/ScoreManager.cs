using PurrNet;
using UnityEngine;

public class ScoreManager : NetworkBehaviour
{
    [SerializeField] private SyncDictionary<PlayerID, ScoreData> scores = new();

    private void Awake()
    {
        InstanceHandler.RegisterInstance(this);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        InstanceHandler.UnregisterInstance<ScoreManager>();
    }

    public void AddKill(PlayerID playerID)
    {
        CheckForDictionaryEntry(playerID);

        var scoreData = scores[playerID];
        scoreData.kills++;
        scores[playerID] = scoreData;
    }

    public void AddDeath(PlayerID playerID)
    {
        CheckForDictionaryEntry(playerID);

        var scoreData = scores[playerID];
        scoreData.deaths++;
        scores[playerID] = scoreData;
    }

    private void CheckForDictionaryEntry(PlayerID playerID)
    {
        if(!scores.ContainsKey(playerID))
            scores.Add(playerID, new ScoreData());
    }

    [System.Serializable]
    public struct ScoreData
    {
        // others: score per min for time survived, round wins, additional points if detective hits decoy
        public int kills;
        public int deaths;
    }
}
