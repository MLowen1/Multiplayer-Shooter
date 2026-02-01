using System;
using System.Collections.Generic;
using PurrNet;
using UnityEngine;

public class ScoreManager : NetworkBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Synced scores
    private SyncDictionary<PlayerID, PlayerScore> _scores = new();

    // Events
    public event Action<PlayerID, PlayerScore> OnScoreChanged;

    private void Awake()
    {
        InstanceHandler.RegisterInstance(this);
    }

    protected override void OnSpawned()
    {
        base.OnSpawned();

        _scores.onChanged += HandleScoreChanged;

        if (showDebug)
            Debug.Log("[ScoreManager] Spawned");
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        _scores.onChanged -= HandleScoreChanged;
        InstanceHandler.UnregisterInstance<ScoreManager>();
    }

    private void HandleScoreChanged(SyncDictionaryChange<PlayerID, PlayerScore> change)
    {
        OnScoreChanged?.Invoke(change.key, change.value);
    }

    /// <summary>
    /// Get score data for a player
    /// </summary>
    public PlayerScore GetScoreData(PlayerID playerID)
    {
        if (_scores.TryGetValue(playerID, out PlayerScore score))
            return score;

        return new PlayerScore();
    }

    /// <summary>
    /// Get all scores
    /// </summary>
    public Dictionary<PlayerID, PlayerScore> GetAllScores()
    {
        var result = new Dictionary<PlayerID, PlayerScore>();
        foreach (var kvp in _scores)
        {
            result[kvp.Key] = kvp.Value;
        }
        return result;
    }

    /// <summary>
    /// Add score to a player
    /// </summary>
    public void AddScore(PlayerID playerID, int amount)
    {
        if (!isServer) return;

        EnsurePlayerExists(playerID);

        var score = _scores[playerID];
        score.score += amount;
        _scores[playerID] = score;

        if (showDebug)
            Debug.Log($"[ScoreManager] {playerID} gained {amount} points. Total: {score.score}");
    }

    /// <summary>
    /// Record a kill
    /// </summary>
    public void AddKill(PlayerID killerID)
    {
        if (!isServer) return;

        EnsurePlayerExists(killerID);

        var score = _scores[killerID];
        score.kills++;
        _scores[killerID] = score;

        if (showDebug)
            Debug.Log($"[ScoreManager] {killerID} got a kill. Total kills: {score.kills}");
    }

    /// <summary>
    /// Record a death
    /// </summary>
    public void AddDeath(PlayerID playerID)
    {
        if (!isServer) return;

        EnsurePlayerExists(playerID);

        var score = _scores[playerID];
        score.deaths++;
        _scores[playerID] = score;

        if (showDebug)
            Debug.Log($"[ScoreManager] {playerID} died. Total deaths: {score.deaths}");
    }

    /// <summary>
    /// Initialize a player's score entry
    /// </summary>
    public void InitializePlayer(PlayerID playerID)
    {
        if (!isServer) return;

        EnsurePlayerExists(playerID);
    }

    /// <summary>
    /// Reset all scores (for new game)
    /// </summary>
    public void ResetScores()
    {
        if (!isServer) return;

        _scores.Clear();

        if (showDebug)
            Debug.Log("[ScoreManager] All scores reset!");
    }

    /// <summary>
    /// Get the player with the highest score
    /// </summary>
    public PlayerID? GetTopPlayer()
    {
        PlayerID? topPlayer = null;
        int topScore = int.MinValue;

        foreach (var kvp in _scores)
        {
            if (kvp.Value.score > topScore)
            {
                topScore = kvp.Value.score;
                topPlayer = kvp.Key;
            }
        }

        return topPlayer;
    }

    /// <summary>
    /// Get sorted leaderboard
    /// </summary>
    public List<KeyValuePair<PlayerID, PlayerScore>> GetLeaderboard()
    {
        var list = new List<KeyValuePair<PlayerID, PlayerScore>>();

        foreach (var kvp in _scores)
        {
            list.Add(kvp);
        }

        // Sort by score descending
        list.Sort((a, b) => b.Value.score.CompareTo(a.Value.score));

        return list;
    }

    private void EnsurePlayerExists(PlayerID playerID)
    {
        if (!_scores.ContainsKey(playerID))
        {
            _scores.Add(playerID, new PlayerScore());
        }
    }
}

[System.Serializable]
public struct PlayerScore
{
    public int score;
    public int kills;
    public int deaths;

    public override string ToString()
    {
        return $"Score: {score}, K: {kills}, D: {deaths}";
    }
}