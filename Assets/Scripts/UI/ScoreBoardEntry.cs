using UnityEngine;
using TMPro;
using PurrNet;

public class ScoreboardEntry : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text killsText;
    [SerializeField] private TMP_Text deathsText;

    [Header("Highlight")]
    [SerializeField] private GameObject localPlayerHighlight;

    private PlayerID _playerID;

    public PlayerID PlayerID => _playerID;

    public void Setup(PlayerID playerID, string playerName, int score, int kills, int deaths, bool isLocalPlayer)
    {
        _playerID = playerID;

        if (playerNameText != null)
            playerNameText.text = playerName;

        if (scoreText != null)
            scoreText.text = score.ToString();

        if (killsText != null)
            killsText.text = kills.ToString();

        if (deathsText != null)
            deathsText.text = deaths.ToString();

        // Highlight local player's row
        if (localPlayerHighlight != null)
            localPlayerHighlight.SetActive(isLocalPlayer);
    }

    public void UpdateScore(int score, int kills, int deaths)
    {
        if (scoreText != null)
            scoreText.text = score.ToString();

        if (killsText != null)
            killsText.text = kills.ToString();

        if (deathsText != null)
            deathsText.text = deaths.ToString();
    }
}