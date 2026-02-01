using UnityEngine;
using TMPro;
using PurrNet;

public class GameHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private GameObject roundEndPanel;
    [SerializeField] private TMP_Text roundEndTitleText;
    [SerializeField] private TMP_Text roundEndMessageText;

    [Header("Settings")]
    [SerializeField] private Color hidingPhaseColor = Color.yellow;
    [SerializeField] private Color huntingPhaseColor = Color.red;
    [SerializeField] private Color waitingColor = Color.white;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private RoundManager _roundManager;
    private bool _isGameOver = false;

    private void Awake()
    {
        InstanceHandler.RegisterInstance(this);
    }

    private void OnDestroy()
    {
        InstanceHandler.UnregisterInstance<GameHUD>();
        UnsubscribeFromEvents();
    }

    private void Start()
    {
        if (roundEndPanel != null)
            roundEndPanel.SetActive(false);

        StartCoroutine(WaitForRoundManager());
    }

    private System.Collections.IEnumerator WaitForRoundManager()
    {
        while (!InstanceHandler.TryGetInstance(out _roundManager))
        {
            yield return new WaitForSeconds(0.5f);
        }

        SubscribeToEvents();
        UpdateUI();

        if (showDebug)
            Debug.Log("[GameHUD] Connected to RoundManager");
    }

    private void SubscribeToEvents()
    {
        if (_roundManager == null) return;

        _roundManager.OnPhaseChanged += OnPhaseChanged;
        _roundManager.OnTimerUpdated += OnTimerUpdated;
        _roundManager.OnRoundEnded += OnRoundEnded;
        _roundManager.OnGameEnded += OnGameEnded;
        _roundManager.OnCountdownUpdated += OnCountdownUpdated;
    }

    private void UnsubscribeFromEvents()
    {
        if (_roundManager == null) return;

        _roundManager.OnPhaseChanged -= OnPhaseChanged;
        _roundManager.OnTimerUpdated -= OnTimerUpdated;
        _roundManager.OnRoundEnded -= OnRoundEnded;
        _roundManager.OnGameEnded -= OnGameEnded;
        _roundManager.OnCountdownUpdated -= OnCountdownUpdated;
    }

    private void OnPhaseChanged(GamePhase newPhase)
    {
        if (showDebug)
            Debug.Log($"[GameHUD] Phase changed to: {newPhase}");

        UpdatePhaseText(newPhase);

        if (newPhase != GamePhase.RoundEnd && newPhase != GamePhase.GameOver)
        {
            if (roundEndPanel != null)
                roundEndPanel.SetActive(false);

            _isGameOver = false;
        }
    }

    private void OnTimerUpdated(float timeRemaining)
    {
        UpdateTimerText(timeRemaining);
    }

    private void OnCountdownUpdated(float countdown)
    {
        UpdateCountdownText(countdown);
    }

    private void OnRoundEnded(Team winner)
    {
        if (showDebug)
            Debug.Log($"[GameHUD] Round ended. Winner: {winner}");

        _isGameOver = false;
        ShowRoundEndPanel(winner, false);
    }

    private void OnGameEnded(Team winner)
    {
        if (showDebug)
            Debug.Log($"[GameHUD] Game ended. Winner: {winner}");

        _isGameOver = true;
        ShowRoundEndPanel(winner, true);
    }

    private void UpdateUI()
    {
        if (_roundManager == null) return;

        UpdatePhaseText(_roundManager.CurrentPhase);
        UpdateTimerText(_roundManager.TimeRemaining);
        UpdateRoundText();
    }

    private void UpdatePhaseText(GamePhase phase)
    {
        if (phaseText == null) return;

        switch (phase)
        {
            case GamePhase.WaitingToStart:
                phaseText.text = "WAITING...";
                phaseText.color = waitingColor;
                break;
            case GamePhase.Hiding:
                phaseText.text = "HIDING PHASE";
                phaseText.color = hidingPhaseColor;
                break;
            case GamePhase.Hunting:
                phaseText.text = "HUNTING PHASE";
                phaseText.color = huntingPhaseColor;
                break;
            case GamePhase.RoundEnd:
                phaseText.text = "ROUND OVER";
                phaseText.color = waitingColor;
                break;
            case GamePhase.GameOver:
                phaseText.text = "GAME OVER";
                phaseText.color = waitingColor;
                break;
        }

        UpdateRoundText();
    }

    private void UpdateTimerText(float timeRemaining)
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);

        timerText.text = $"{minutes:00}:{seconds:00}";

        if (timeRemaining <= 10f && _roundManager?.CurrentPhase == GamePhase.Hunting)
        {
            timerText.color = (Mathf.FloorToInt(timeRemaining * 2) % 2 == 0) ? Color.red : Color.white;
        }
        else
        {
            timerText.color = Color.white;
        }
    }

    private void UpdateRoundText()
    {
        if (roundText == null || _roundManager == null) return;

        roundText.text = $"Round {_roundManager.CurrentRound} / {_roundManager.TotalRounds}";
    }

    private void UpdateCountdownText(float countdown)
    {
        if (roundEndMessageText == null || _roundManager == null) return;

        // Only update if we're in round end or game over
        if (_roundManager.CurrentPhase != GamePhase.RoundEnd &&
            _roundManager.CurrentPhase != GamePhase.GameOver)
            return;

        int seconds = Mathf.CeilToInt(countdown);

        // Get base message based on winner
        string baseMessage = GetWinMessage(_roundManager.RoundWinner);

        // Add countdown
        if (_isGameOver)
        {
            roundEndMessageText.text = $"{baseMessage}\n\n<size=70%>Returning to lobby in {seconds}...</size>";
        }
        else
        {
            roundEndMessageText.text = $"{baseMessage}\n\n<size=70%>Next round in {seconds}...</size>";
        }
    }

    private string GetWinMessage(Team winner)
    {
        bool localPlayerWon = false;
        var allPlayers = FindObjectsByType<PlayerTeam>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            var identity = player.GetComponent<NetworkIdentity>();
            if (identity != null && identity.isOwner)
            {
                localPlayerWon = (player.CurrentTeam == winner);
                break;
            }
        }

        if (winner == Team.Props)
        {
            return localPlayerWon ? "PROPS WIN!\nYou survived!" : "PROPS WIN!\nTime ran out!";
        }
        else
        {
            return localPlayerWon ? "SEEKERS WIN!\nAll props eliminated!" : "SEEKERS WIN!\nYou were found!";
        }
    }

    private void ShowRoundEndPanel(Team winner, bool isGameOver)
    {
        if (roundEndPanel == null) return;

        roundEndPanel.SetActive(true);

        if (roundEndTitleText != null)
        {
            roundEndTitleText.text = isGameOver ? "GAME OVER" : "ROUND OVER";
        }

        if (roundEndMessageText != null)
        {
            bool localPlayerWon = false;
            var allPlayers = FindObjectsByType<PlayerTeam>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                var identity = player.GetComponent<NetworkIdentity>();
                if (identity != null && identity.isOwner)
                {
                    localPlayerWon = (player.CurrentTeam == winner);
                    break;
                }
            }

            string winMessage;
            if (winner == Team.Props)
            {
                winMessage = localPlayerWon ? "PROPS WIN!\nYou survived!" : "PROPS WIN!\nTime ran out!";
                roundEndMessageText.color = localPlayerWon ? Color.green : Color.red;
            }
            else
            {
                winMessage = localPlayerWon ? "SEEKERS WIN!\nAll props eliminated!" : "SEEKERS WIN!\nYou were found!";
                roundEndMessageText.color = localPlayerWon ? Color.green : Color.red;
            }

            // Initial message with "calculating..."
            if (isGameOver)
            {
                roundEndMessageText.text = $"{winMessage}\n\n<size=70%>Returning to lobby...</size>";
            }
            else
            {
                roundEndMessageText.text = $"{winMessage}\n\n<size=70%>Next round starting...</size>";
            }
        }
    }
}