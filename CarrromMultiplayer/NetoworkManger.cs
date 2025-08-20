using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;
using UnityEngine.SceneManagement;

public class NetworkGameManager : NetworkBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private int minPlayers = 2;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI[] playerScoreTexts = new TextMeshProUGUI[4];
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private TextMeshProUGUI instructionsText;
    [SerializeField] private TextMeshProUGUI turnTimerText;
    [SerializeField] private TextMeshProUGUI currentPlayerText;

    [Header("Game Objects")]
    [SerializeField] private GameObject instructionsMenu;
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject gameOverMenu;
    [SerializeField] private GameObject[] playerStrikers = new GameObject[4];
    [SerializeField] private GameObject turnText;
    [SerializeField] private GameObject slider;
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject queenPrefab;
    [SerializeField] private GameObject hostMenu;
    [SerializeField] private TMP_InputField playerCountInput;
    [SerializeField] private GameObject waitingForPlayersMenu;
    [SerializeField] private TextMeshProUGUI connectedPlayersText;

    [Header("Player Colors")]
    [SerializeField] private Color[] playerColors = { Color.red, Color.blue, Color.green, Color.yellow };
    [SerializeField] private string[] playerNames = { "Player 1", "Player 2", "Player 3", "Player 4" };

    // Network Variables
    private NetworkVariable<int> currentPlayerIndex = new NetworkVariable<int>(0);
    private NetworkVariable<int> connectedPlayersCount = new NetworkVariable<int>(0);
    private NetworkVariable<int> targetPlayerCount = new NetworkVariable<int>(2);
    private NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(false);
    private NetworkVariable<float> turnTimeLeft = new NetworkVariable<float>(10f);
    private NetworkVariable<bool> isTurnTimerRunning = new NetworkVariable<bool>(false);

    // Player scores as network array
    private NetworkList<int> playerScores;
    private NetworkList<bool> activePlayerSlots;

    // Game state
    private bool gameOver = false;
    private bool isPaused = false;
    private TimerScript timerScript;

    // Queen covering system for multiplayer
    private NetworkList<bool> playersHaveQueen;
    private NetworkList<bool> playersNeedToCoverQueen;

    // Pocket multipliers
    private NetworkVariable<int> currentMultiplierRotation = new NetworkVariable<int>(0);

    private void Awake()
    {
        playerScores = new NetworkList<int>();
        activePlayerSlots = new NetworkList<bool>();
        playersHaveQueen = new NetworkList<bool>();
        playersNeedToCoverQueen = new NetworkList<bool>();

        timerScript = GetComponent<TimerScript>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Initialize network lists
            for (int i = 0; i < maxPlayers; i++)
            {
                playerScores.Add(0);
                activePlayerSlots.Add(false);
                playersHaveQueen.Add(false);
                playersNeedToCoverQueen.Add(false);
            }
        }

        // Subscribe to network variable changes
        currentPlayerIndex.OnValueChanged += OnCurrentPlayerChanged;
        connectedPlayersCount.OnValueChanged += OnConnectedPlayersCountChanged;
        gameStarted.OnValueChanged += OnGameStartedChanged;
        turnTimeLeft.OnValueChanged += OnTurnTimeChanged;

        if (playerScores != null)
        {
            playerScores.OnListChanged += OnPlayerScoresChanged;
        }

        UpdateUI();
    }

    void Start()
    {
        if (!IsServer) return;

        ShowHostMenu();
        Time.timeScale = 1;
    }

    void Update()
    {
        if (!IsOwner) return;

        // Update turn timer on server
        if (IsServer && isTurnTimerRunning.Value && !isPaused && !gameOver && gameStarted.Value)
        {
            turnTimeLeft.Value -= Time.deltaTime;

            if (turnTimeLeft.Value <= 0)
            {
                HandleTurnTimeoutServerRpc();
            }
        }

        // Check win condition
        if (IsServer && gameStarted.Value && (CheckWinCondition() || timerScript.timeLeft <= 0))
        {
            OnGameOverServerRpc();
        }

        // Handle pause input
        if (Input.GetKeyDown(KeyCode.Escape) && !gameOver)
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        UpdatePlayerUI();
    }

    private void ShowHostMenu()
    {
        if (hostMenu != null)
        {
            hostMenu.SetActive(true);
        }
    }

    public void StartHost()
    {
        int playerCount = 2; // Default
        if (playerCountInput != null && int.TryParse(playerCountInput.text, out int inputCount))
        {
            playerCount = Mathf.Clamp(inputCount, minPlayers, maxPlayers);
        }

        if (IsServer)
        {
            targetPlayerCount.Value = playerCount;
        }

        NetworkManager.Singleton.StartHost();
        hostMenu.SetActive(false);
        ShowWaitingMenu();

        connectedPlayersCount.Value = 1;
        activePlayerSlots[0] = true;
    }

    public void JoinAsClient()
    {
        NetworkManager.Singleton.StartClient();
        hostMenu.SetActive(false);
    }

    private void ShowWaitingMenu()
    {
        if (waitingForPlayersMenu != null)
        {
            waitingForPlayersMenu.SetActive(true);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (currentPlayerIndex != null)
            currentPlayerIndex.OnValueChanged -= OnCurrentPlayerChanged;
        if (connectedPlayersCount != null)
            connectedPlayersCount.OnValueChanged -= OnConnectedPlayersCountChanged;
        if (gameStarted != null)
            gameStarted.OnValueChanged -= OnGameStartedChanged;
        if (turnTimeLeft != null)
            turnTimeLeft.OnValueChanged -= OnTurnTimeChanged;
        if (playerScores != null)
            playerScores.OnListChanged -= OnPlayerScoresChanged;
    }

    [ServerRpc(RequireOwnership = false)]
    public void OnPlayerConnectedServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        int playerIndex = (int)clientId;

        if (playerIndex < maxPlayers)
        {
            activePlayerSlots[playerIndex] = true;
            connectedPlayersCount.Value++;

            if (connectedPlayersCount.Value >= targetPlayerCount.Value)
            {
                StartGameServerRpc();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void StartGameServerRpc()
    {
        gameStarted.Value = true;
        StartTurnTimer();

        // Initialize board state
        NetworkBoardScript.scoreEnemy = 0;
        NetworkBoardScript.scorePlayer = 0;

        StartGameClientRpc();
    }

    [ClientRpc]
    public void StartGameClientRpc()
    {
        if (waitingForPlayersMenu != null)
        {
            waitingForPlayersMenu.SetActive(false);
        }

        if (timerScript != null)
        {
            timerScript.isTimerRunning = true;
        }
    }

    private void UpdatePlayerUI()
    {
        if (!gameStarted.Value) return;

        // Show/hide appropriate striker and UI elements
        for (int i = 0; i < maxPlayers; i++)
        {
            if (playerStrikers[i] != null)
            {
                bool isCurrentPlayer = (i == currentPlayerIndex.Value) && activePlayerSlots[i];
                playerStrikers[i].SetActive(isCurrentPlayer);
            }
        }

        // Update slider and turn text visibility
        bool isMyTurn = IsMyTurn();
        if (slider != null) slider.SetActive(isMyTurn);
        if (turnText != null) turnText.SetActive(isMyTurn);

        // Update current player display
        if (currentPlayerText != null && activePlayerSlots[currentPlayerIndex.Value])
        {
            currentPlayerText.text = playerNames[currentPlayerIndex.Value] + "'s Turn";
            currentPlayerText.color = playerColors[currentPlayerIndex.Value];
        }
    }

    private bool IsMyTurn()
    {
        return NetworkManager.Singleton.LocalClientId == (ulong)currentPlayerIndex.Value;
    }

    [ServerRpc(RequireOwnership = false)]
    public void OnShotTakenServerRpc()
    {
        isTurnTimerRunning.Value = false;
    }

    [ServerRpc(RequireOwnership = false)]
    public void OnShotCompleteServerRpc(bool continueTurn, int piecesPocketed, bool strikerPocketed, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        int playerIndex = (int)clientId;

        // Check for failed queen cover when turn ends
        if (!continueTurn)
        {
            CheckQueenCoverServerRpc(playerIndex, piecesPocketed);
            SwitchToNextPlayer();
            RotateMultipliersServerRpc();
        }

        StartTurnTimer();
    }

    private void SwitchToNextPlayer()
    {
        int nextPlayer = currentPlayerIndex.Value;

        do
        {
            nextPlayer = (nextPlayer + 1) % maxPlayers;
        }
        while (!activePlayerSlots[nextPlayer]);

        currentPlayerIndex.Value = nextPlayer;
    }

    [ServerRpc(RequireOwnership = false)]
    private void CheckQueenCoverServerRpc(int playerIndex, int piecesPocketed)
    {
        if (piecesPocketed == 0 && playersNeedToCoverQueen[playerIndex])
        {
            playersNeedToCoverQueen[playerIndex] = false;
            playersHaveQueen[playerIndex] = false;
            playerScores[playerIndex] = Mathf.Max(0, playerScores[playerIndex] - 50);
            RespawnQueenServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RespawnQueenServerRpc()
    {
        RespawnQueenClientRpc();
    }

    [ClientRpc]
    private void RespawnQueenClientRpc()
    {
        GameObject queen = GameObject.FindGameObjectWithTag("Queen");

        if (queen == null && queenPrefab != null)
        {
            queen = Instantiate(queenPrefab, Vector3.zero, Quaternion.identity);
            queen.tag = "Queen";
        }
        else if (queen != null)
        {
            queen.transform.position = Vector3.zero;
            queen.SetActive(true);
        }

        if (queen != null)
        {
            Rigidbody2D queenRb = queen.GetComponent<Rigidbody2D>();
            if (queenRb != null)
            {
                queenRb.velocity = Vector2.zero;
                queenRb.angularVelocity = 0f;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RotateMultipliersServerRpc()
    {
        currentMultiplierRotation.Value = (currentMultiplierRotation.Value + 1) % 4;
        NetworkBoardScript.currentMultiplierRotation = currentMultiplierRotation.Value;
    }

    private void StartTurnTimer()
    {
        turnTimeLeft.Value = 10f;
        isTurnTimerRunning.Value = true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void HandleTurnTimeoutServerRpc()
    {
        isTurnTimerRunning.Value = false;
        SwitchToNextPlayer();
        StartTurnTimer();
    }

    private bool CheckWinCondition()
    {
        GameObject[] whites = GameObject.FindGameObjectsWithTag("White");
        GameObject[] blacks = GameObject.FindGameObjectsWithTag("Black");
        GameObject queen = GameObject.FindGameObjectWithTag("Queen");

        bool allWhitesCleared = whites.Length == 0;
        bool allBlacksCleared = blacks.Length == 0;
        bool allPiecesCleared = allWhitesCleared && allBlacksCleared && queen == null;

        return allWhitesCleared || allBlacksCleared || allPiecesCleared;
    }

    [ServerRpc(RequireOwnership = false)]
    private void OnGameOverServerRpc()
    {
        OnGameOverClientRpc();
    }

    [ClientRpc]
    private void OnGameOverClientRpc()
    {
        gameOver = true;
        isTurnTimerRunning.Value = false;
        gameOverMenu.SetActive(true);
        Time.timeScale = 0;

        // Determine winner
        int winnerIndex = 0;
        int highestScore = playerScores[0];

        for (int i = 1; i < maxPlayers; i++)
        {
            if (activePlayerSlots[i] && playerScores[i] > highestScore)
            {
                highestScore = playerScores[i];
                winnerIndex = i;
            }
        }

        gameOverText.text = playerNames[winnerIndex] + " Wins!";
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpdatePlayerScoreServerRpc(int playerIndex, int newScore)
    {
        if (playerIndex >= 0 && playerIndex < maxPlayers)
        {
            playerScores[playerIndex] = newScore;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpdateQueenStatusServerRpc(int playerIndex, bool hasQueen, bool needsToCover)
    {
        if (playerIndex >= 0 && playerIndex < maxPlayers)
        {
            playersHaveQueen[playerIndex] = hasQueen;
            playersNeedToCoverQueen[playerIndex] = needsToCover;
        }
    }

    // Event handlers
    private void OnCurrentPlayerChanged(int oldValue, int newValue)
    {
        UpdatePlayerUI();
    }

    private void OnConnectedPlayersCountChanged(int oldValue, int newValue)
    {
        if (connectedPlayersText != null)
        {
            connectedPlayersText.text = $"Players: {newValue}/{targetPlayerCount.Value}";
        }
    }

    private void OnGameStartedChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            UpdateUI();
        }
    }

    private void OnTurnTimeChanged(float oldValue, float newValue)
    {
        if (turnTimerText != null)
        {
            turnTimerText.text = "Turn: " + Mathf.Ceil(newValue).ToString();
            turnTimerText.color = newValue <= 3f ? Color.red : Color.white;
        }
    }

    private void OnPlayerScoresChanged(NetworkListEvent<int> changeEvent)
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (!gameStarted.Value) return;

        // Update score displays
        for (int i = 0; i < maxPlayers && i < playerScoreTexts.Length; i++)
        {
            if (playerScoreTexts[i] != null)
            {
                if (activePlayerSlots[i])
                {
                    playerScoreTexts[i].gameObject.SetActive(true);
                    playerScoreTexts[i].text = playerNames[i] + ": " + playerScores[i].ToString();
                    playerScoreTexts[i].color = playerColors[i];
                }
                else
                {
                    playerScoreTexts[i].gameObject.SetActive(false);
                }
            }
        }
    }

    public void ResumeGame()
    {
        isPaused = false;
        pauseMenu.SetActive(false);
        Time.timeScale = 1;
    }

    public void PauseGame()
    {
        isPaused = true;
        pauseMenu.SetActive(true);
        Time.timeScale = 0;
    }

    public void RestartGame()
    {
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(0);
    }

    // Public getters for other scripts
    public int GetCurrentPlayerIndex() => currentPlayerIndex.Value;
    public bool IsGameStarted() => gameStarted.Value;
    public int GetPlayerScore(int playerIndex) => playerIndex < playerScores.Count ? playerScores[playerIndex] : 0;
    public bool PlayerHasQueen(int playerIndex) => playerIndex < playersHaveQueen.Count && playersHaveQueen[playerIndex];
    public bool PlayerNeedsToCoverQueen(int playerIndex) => playerIndex < playersNeedToCoverQueen.Count && playersNeedToCoverQueen[playerIndex];
}