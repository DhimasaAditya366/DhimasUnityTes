using System.Collections;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public enum GameState
{
    Preparing,
    Playing,
    GameOver
}

public enum TeamRole
{
    Attacker,
    Defender
}

public enum MatchResult
{
    None,
    PlayerWin,
    EnemyWin,
    Draw
}

public interface ITeamRoleHandler
{
    void OnTeamRoleChanged(TeamRole newRole);
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI References")]
    public TextMeshProUGUI timeDisplayText;
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI enemyNameText;
    public Slider playerEnergySlider;
    public Slider enemyEnergySlider;

    [Header("Energy Settings")]
    [SerializeField] private float maxEnergy = 6f;
    [SerializeField] private float regenerationRate = 0.5f; // 0.5 per second
    public float currentPlayerEnergy = 0f; // Start at 0
    public float currentEnemyEnergy = 0f; // Start at 0

    [Header("Player Settings")]
    public string playerName = "Player";
    public string enemyName = "Enemy";

    [Header("Debug Settings")]
    public bool isDebugMode = false;

    [Header("Game Settings")]
    public float normalMatchDuration = 140f;
    public float debugMatchDuration = 5f;
    public int totalMatches = 3;
    public Color playerColor = Color.blue;
    public Color enemyColor = Color.red;

    [Header("Pause State")]
    private bool isPaused = false;
    public bool IsPaused => isPaused;

    [Header("Team References")]
    public TeamManager playerTeamManager;
    public TeamManager enemyTeamManager;

    [Header("Match State")]
    [SerializeField] private int currentMatch = 1;
    [SerializeField] private float currentMatchTime;
    [SerializeField] private GameState currentGameState;

    [Header("Team Roles")]
    public TeamRole playerRole;
    public TeamRole enemyRole;

    [Header("Score System")]
    public int playerScore = 0;
    public int enemyScore = 0;
    [SerializeField] private TextMeshProUGUI playerScoreText;
    [SerializeField] private TextMeshProUGUI enemyScoreText;

    [Header("Result UI")]
    public GameObject resultPanel;
    public TextMeshProUGUI matchResultText;
    [SerializeField] private string playerWinText = "Victory!";
    [SerializeField] private string playerLoseText = "Defeat...";
    [SerializeField] private string drawText = "Draw";

    private MatchResult currentMatchResult = MatchResult.None;

    private float matchDuration => isDebugMode ? debugMatchDuration : normalMatchDuration;
    public delegate void MatchStateHandler();
    public static event MatchStateHandler OnMatchStart;

    // Properties
    public int CurrentMatch => currentMatch;
    public float CurrentMatchTime => currentMatchTime;
    public GameState CurrentGameState => currentGameState;
    public TeamRole PlayerRole => playerRole;
    public TeamRole EnemyRole => enemyRole;
    public float CurrentPlayerEnergy => currentPlayerEnergy;
    public float CurrentEnemyEnergy => currentEnemyEnergy;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeGame();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void InitializeGame()
    {
        // Reset all game states
        isPaused = false;
        Time.timeScale = 1f;
        currentGameState = GameState.Playing;

        // Reset roles
        playerRole = TeamRole.Attacker;
        enemyRole = TeamRole.Defender;

        // Find and assign UI references
        FindUIReferences();

        // Initialize energy system
        InitializeEnergySystem();

        // Start the first match
        currentMatch = 1;
        InitializeMatch();

        // Initialize scores
        playerScore = 0;
        enemyScore = 0;
        UpdateScoreDisplay();

        // Make sure result panel is hidden at start
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
    }

    private void FindUIReferences()
    {
        timeDisplayText = GameObject.FindGameObjectWithTag("TimeDisplay")?.GetComponent<TextMeshProUGUI>();
        playerNameText = GameObject.FindGameObjectWithTag("PlayerNameText")?.GetComponent<TextMeshProUGUI>();
        enemyNameText = GameObject.FindGameObjectWithTag("EnemyNameText")?.GetComponent<TextMeshProUGUI>();
        playerEnergySlider = GameObject.FindGameObjectWithTag("PlayerEnergySlider")?.GetComponent<Slider>();
        enemyEnergySlider = GameObject.FindGameObjectWithTag("EnemyEnergySlider")?.GetComponent<Slider>();

        // Find team managers if needed
        playerTeamManager = GameObject.FindGameObjectWithTag("PlayerTeam")?.GetComponent<TeamManager>();
        enemyTeamManager = GameObject.FindGameObjectWithTag("EnemyTeam")?.GetComponent<TeamManager>();
    }

    private void InitializeEnergySystem()
    {
        if (playerEnergySlider != null)
        {
            playerEnergySlider.minValue = 0;
            playerEnergySlider.maxValue = maxEnergy;
            currentPlayerEnergy = 0f;
            playerEnergySlider.value = currentPlayerEnergy;
        }

        if (enemyEnergySlider != null)
        {
            enemyEnergySlider.minValue = 0;
            enemyEnergySlider.maxValue = maxEnergy;
            currentEnemyEnergy = 0f;
            enemyEnergySlider.value = currentEnemyEnergy;
        }
    }

    private void Start()
    {
        currentMatch = 1;
        InitializeMatch();
    }

    private void InitializeMatch()
    {
        // Clear soldiers from both teams
        if (playerTeamManager != null)
        {
            playerTeamManager.ClearAllSoldiers();
        }
        if (enemyTeamManager != null)
        {
            enemyTeamManager.ClearAllSoldiers();
        }

        SetTeamRoles();
        UpdateNameDisplays();
        currentMatchTime = matchDuration;
        currentGameState = GameState.Playing;

        // Start energy from 0 at beginning of each match
        ResetEnergy();

        Debug.Log($"Match initialized with duration: {matchDuration} seconds (Debug Mode: {isDebugMode})");
        OnMatchStart?.Invoke();
        Debug.Log("Match started - triggering ball spawn");

        currentMatchResult = MatchResult.None;

        // Hide result panel during matches
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
    }

    private void ResetEnergy()
    {
        // Reset to 0 instead of max
        currentPlayerEnergy = 0f;
        currentEnemyEnergy = 0f;
        UpdateEnergyUI();
    }

    private void SetTeamRoles()
    {
        // Keep existing role setting code
        TeamRole oldPlayerRole = playerRole;
        TeamRole oldEnemyRole = enemyRole;

        playerRole = currentMatch % 2 == 1 ? TeamRole.Attacker : TeamRole.Defender;
        enemyRole = playerRole == TeamRole.Attacker ? TeamRole.Defender : TeamRole.Attacker;

        Debug.Log($"Match {currentMatch}: Player Role = {playerRole}, Enemy Role = {enemyRole}");

        // Update name displays with roles
        UpdateNameDisplays();

        // Keep existing TeamManager updates
        if (oldPlayerRole != playerRole || oldEnemyRole != enemyRole)
        {
            if (playerTeamManager != null)
            {
                playerTeamManager.UpdateTeamRole(playerRole);
                Debug.Log($"Updated player team to {playerRole}");
            }
            else
            {
                Debug.LogWarning("Player TeamManager is not assigned!");
            }

            if (enemyTeamManager != null)
            {
                enemyTeamManager.UpdateTeamRole(enemyRole);
                Debug.Log($"Updated enemy team to {enemyRole}");
            }
            else
            {
                Debug.LogWarning("Enemy TeamManager is not assigned!");
            }
        }
    }

    private void UpdateScoreDisplay()
    {
        if (playerScoreText != null)
        {
            playerScoreText.text = $"{playerScore}";
        }
        if (enemyScoreText != null)
        {
            enemyScoreText.text = $"{enemyScore}";
        }
    }

    private void UpdateNameDisplays()
    {
        if (playerNameText != null)
        {
            playerNameText.text = $"{playerName} ({playerRole})";
        }
        else
        {
            Debug.LogWarning("Player name text component is not assigned!");
        }

        if (enemyNameText != null)
        {
            enemyNameText.text = $"{enemyName} ({enemyRole})";
        }
        else
        {
            Debug.LogWarning("Enemy name text component is not assigned!");
        }
    }

    private void Update()
    {
        if (currentGameState == GameState.Playing && !isPaused)
        {
            UpdateMatchTimer();
            RegenerateEnergy();
        }
    }

    private void RegenerateEnergy()
    {
        if (isPaused) return;  // Add this check to prevent energy changes while paused

        // Regenerate player energy
        if (currentPlayerEnergy < maxEnergy)
        {
            currentPlayerEnergy = Mathf.Min(maxEnergy, currentPlayerEnergy + (regenerationRate * Time.deltaTime));
        }

        // Regenerate enemy energy
        if (currentEnemyEnergy < maxEnergy)
        {
            currentEnemyEnergy = Mathf.Min(maxEnergy, currentEnemyEnergy + (regenerationRate * Time.deltaTime));
        }

        UpdateEnergyUI();
    }

    public void UpdateEnergyUI()
    {
        if (playerEnergySlider != null)
        {
            playerEnergySlider.value = currentPlayerEnergy;
        }

        if (enemyEnergySlider != null)
        {
            enemyEnergySlider.value = currentEnemyEnergy;
        }
    }

    public bool TryConsumePlayerEnergy(float amount)
    {
        if (isPaused) return false;  // Prevent energy consumption while paused

        if (currentPlayerEnergy >= amount)
        {
            currentPlayerEnergy -= amount;
            UpdateEnergyUI();
            return true;
        }
        return false;
    }

    public bool TryConsumeEnemyEnergy(float amount)
    {
        if (isPaused) return false;  // Prevent energy consumption while paused

        if (currentEnemyEnergy >= amount)
        {
            currentEnemyEnergy -= amount;
            UpdateEnergyUI();
            return true;
        }
        return false;
    }

    private void UpdateMatchTimer()
    {
        if (currentMatchTime > 0)
        {
            currentMatchTime -= Time.deltaTime;

            // Clamp the time to not go below 0
            currentMatchTime = Mathf.Max(0f, currentMatchTime);

            // Update the UI text
            if (timeDisplayText != null)
            {
                timeDisplayText.text = FormatTime(currentMatchTime);
            }

            // Check for match end when time reaches 0
            if (currentMatchTime <= 0)
            {
                EndMatch();
            }
        }
    }

    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);

        if (minutes > 0)
        {
            return $"{minutes} Minute {seconds} Second";
        }
        else
        {
            return $"{seconds} Second";
        }
    }

    public void HandleBallReachedGate(bool isPlayerTeam)
    {
        // Determine if the scoring team was attacking
        bool isScoringTeamAttacker = (isPlayerTeam && playerRole == TeamRole.Attacker) ||
                                    (!isPlayerTeam && enemyRole == TeamRole.Attacker);

        if (isScoringTeamAttacker)
        {
            // Award point to the scoring team
            if (isPlayerTeam)
            {
                playerScore++;
                currentMatchResult = MatchResult.PlayerWin;

                // Play player victory particles at the gate position
                if (VictoryParticleManager.Instance != null)
                {
                    // Get the gate position from the TeamStructure
                    Transform gate = TeamStructure.FindTargetStructure(TeamRole.Attacker, true, TeamStructure.StructureType.Gate);
                    if (gate != null)
                    {
                        VictoryParticleManager.Instance.PlayVictoryParticles(true, gate.position);
                    }
                }
            }
            else
            {
                enemyScore++;
                currentMatchResult = MatchResult.EnemyWin;

                // Play enemy victory particles at the gate position
                if (VictoryParticleManager.Instance != null)
                {
                    // Get the gate position from the TeamStructure
                    Transform gate = TeamStructure.FindTargetStructure(TeamRole.Attacker, false, TeamStructure.StructureType.Gate);
                    if (gate != null)
                    {
                        VictoryParticleManager.Instance.PlayVictoryParticles(false, gate.position);
                    }
                }
            }
            UpdateScoreDisplay();
            EndMatch();
        }
    }

    public void HandleDefenderCatch(bool isPlayerTeamDefending)
    {
        if (isPlayerTeamDefending)
        {
            // Player team was defending and caught enemy attacker
            playerScore++;
            currentMatchResult = MatchResult.PlayerWin;
            Debug.Log("Player team (defending) caught enemy attacker - Player wins match!");
        }
        else
        {
            // Enemy team was defending and caught player attacker
            enemyScore++;
            currentMatchResult = MatchResult.EnemyWin;
            Debug.Log("Enemy team (defending) caught player attacker - Enemy wins match!");
        }

        UpdateScoreDisplay();
        EndMatch();
    }

    private void EndMatch()
    {
        currentGameState = GameState.GameOver;

        // If match ended in timeout and no winner, it's a draw
        if (currentMatchResult == MatchResult.None && currentMatchTime <= 0)
        {
            currentMatchResult = MatchResult.Draw;
            playerScore++;
            enemyScore++;
            UpdateScoreDisplay();
        }

        if (currentMatch < totalMatches)
        {
            // Don't show result panel, just start next match
            StartCoroutine(StartNextMatchCoroutine());
        }
        else
        {
            // Only show result panel after all matches
            ShowFinalResult();
        }
    }

    private void ShowFinalResult()
    {
        if (resultPanel != null && matchResultText != null)
        {
            string resultMessage;
            if (playerScore > enemyScore)
            {
                resultMessage = playerWinText;
                Debug.Log("Player wins the game!");
            }
            else if (enemyScore > playerScore)
            {
                resultMessage = playerLoseText;
                Debug.Log("Enemy wins the game!");
            }
            else
            {
                resultMessage = drawText;
                Debug.Log("The game is a draw!");
            }

            matchResultText.text = resultMessage;
            resultPanel.SetActive(true);
        }
    }

    private void StartPenaltyGame()
    {
        // Empty function for now - will implement penalty game logic later
        Debug.Log("Starting Penalty Game...");
    }

    private IEnumerator StartNextMatchCoroutine()
    {
        // Wait a short time before starting next match
        yield return new WaitForSeconds(2f);
        StartNextMatch();
    }

    public void StartNextMatch()
    {
        if (currentMatch < totalMatches)
        {
            currentMatch++;
            Debug.Log($"Starting match {currentMatch}");
            InitializeMatch();
        }
    }


    public float GetMatchProgress()
    {
        return 1f - (currentMatchTime / matchDuration);
    }

    public string GetMatchDisplay()
    {
        return $"Match {currentMatch}/{totalMatches}";
    }

    public void TogglePause()
    {
        // Don't process any other input this frame
        if (EventSystem.current.IsPointerOverGameObject())
        {
            isPaused = !isPaused;
            Time.timeScale = isPaused ? 0f : 1f;

            // Notify all active soldiers about pause state
            if (playerTeamManager != null)
            {
                playerTeamManager.SetTeamPauseState(isPaused);
            }
            if (enemyTeamManager != null)
            {
                enemyTeamManager.SetTeamPauseState(isPaused);
            }
        }
    }
}