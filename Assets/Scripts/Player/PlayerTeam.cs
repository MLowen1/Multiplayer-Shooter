using System;
using UnityEngine;
using PurrNet;

public enum Team
{
    None,
    Props,
    Seekers
}

/// <summary>
/// Attach to Player Prefab to track which team this player is on.
/// </summary>
public class PlayerTeam : NetworkBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Synced team value
    private SyncVar<Team> _team = new(Team.None);

    /// <summary>
    /// Fired when this player's team changes
    /// </summary>
    public event Action<Team> OnTeamChanged;

    public Team CurrentTeam => _team.value;
    public bool IsProp => _team.value == Team.Props;
    public bool IsSeeker => _team.value == Team.Seekers;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        _team.onChanged += HandleTeamChanged;

        // If team already set (late join scenario), fire event
        if (_team.value != Team.None)
        {
            HandleTeamChanged(_team.value);
        }

        if (showDebug)
            Debug.Log($"[PlayerTeam] Spawned. IsOwner: {isOwner}, Team: {_team.value}");
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _team.onChanged -= HandleTeamChanged;
    }

    private void HandleTeamChanged(Team newTeam)
    {
        if (showDebug)
            Debug.Log($"[PlayerTeam] Team changed to: {newTeam} (IsOwner: {isOwner})");

        // Show message to local player about their team
        if (isOwner)
        {
            if (newTeam == Team.Props)
                Debug.Log("[PlayerTeam] You are a PROP! Hide from the seekers. Press 'E' to transform.");
            else if (newTeam == Team.Seekers)
                Debug.Log("[PlayerTeam] You are a SEEKER! Find and eliminate the props.");
        }

        OnTeamChanged?.Invoke(newTeam);
    }

    /// <summary>
    /// Set the player's team. Only call from server.
    /// </summary>
    public void SetTeam(Team team)
    {
        if (!isServer)
        {
            Debug.LogWarning("[PlayerTeam] Only server can set team!");
            return;
        }

        if (showDebug)
            Debug.Log($"[PlayerTeam] Server setting team to: {team}");

        _team.value = team;
    }
}