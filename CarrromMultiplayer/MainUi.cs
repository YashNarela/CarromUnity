using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI;

public class MultiplayerUIManager : NetworkBehaviour
{
    [Header("Host/Join Menu")]
    [SerializeField] private GameObject hostJoinMenu;
    [SerializeField] private TMP_InputField playerCountInput;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;

    [Header("Waiting Menu")]
    [SerializeField] private GameObject waitingMenu;
    [SerializeField] private TextMeshProUGUI waitingText;
    [SerializeField] private TextMeshProUGUI connectedPlayersText;

    [Header("Dynamic UI Elements")]
    [SerializeField] private Transform scoreParent;
    [SerializeField] private GameObject scoreTextPrefab;
    [SerializeField] private Color[] playerColors = { Color.red, Color.blue, Color.green, Color.yellow };
    [SerializeField] private string[] playerNames = { "Player 1", "Player 2", "Player 3", "Player 4" };

    [Header("Game UI")]
    [SerializeField] private TextMeshProUGUI currentPlayerText;
    [SerializeField] private GameObject[] playerSpecificUI = new GameObject[4]; // Sliders, etc.

    private NetworkGameManager networkGameManager;
    private List<TextMeshProUGUI> dynamicScoreTexts = new List<TextMeshProUGUI>();

    private void Start()
    {
        networkGameManager = FindObjectOfType<NetworkGameManager>();

        // Setup initial UI state
        ShowHostJoinMenu();

        // Setup button listeners
        if (hostButton != null)
            hostButton.onClick.AddListener(OnHostButtonClicked);
        if (joinButton != null)
            joinButton.onClick.AddListener(OnJoinButtonClicked);
    }

    private void ShowHostJoinMenu()
    {
        if (hostJoinMenu != null)
            hostJoinMenu.SetActive(true);
        if (waitingMenu != null)
            waitingMenu.SetActive(false);
    }

    public void OnHostButtonClicked()
    {
        int playerCount = 2; // Default
        if (playerCountInput != null && int.TryParse(playerCountInput.text, out int inputCount))
        {
            playerCount = Mathf.Clamp(inputCount, 2, 4);
        }

        // Start hosting
        NetworkManager.Singleton.StartHost();

        // Update UI
        hostJoinMenu.SetActive(false);
        ShowWaitingMenu(playerCount);

        // Create dynamic score UI
        StartCoroutine(DelayedUISetup(playerCount));
    }

    public void OnJoinButtonClicked()
    {
        NetworkManager.Singleton.StartClient();
        hostJoinMenu.SetActive(false);
        ShowWaitingMenu(4); // Max possible players for UI setup
    }

    private void ShowWaitingMenu(int targetPlayers)
    {
        if (waitingMenu != null)
        {
            waitingMenu.SetActive(true);
            if (waitingText != null)
                waitingText.text = $"Waiting for players... (Need {targetPlayers} players)";
        }
    }

    private IEnumerator DelayedUISetup(int playerCount)
    {
        // Wait a frame to ensure network is properly initialized
        yield return new WaitForEndOfFrame();

        SetupDynamicScoreUI(playerCount);
    }

    private void SetupDynamicScoreUI(int playerCount)
    {
        // Clear existing dynamic UI
        foreach (var scoreText in dynamicScoreTexts)
        {
            if (scoreText != null)
                Destroy(scoreText.gameObject);
        }
        dynamicScoreTexts.Clear();

        // Create score UI for each player
        for (int i = 0; i < playerCount; i++)
        {
            if (scoreTextPrefab != null && scoreParent != null)
            {
                GameObject scoreObj = Instantiate(scoreTextPrefab, scoreParent);
                TextMeshProUGUI scoreText = scoreObj.GetComponent<TextMeshProUGUI>();

                if (scoreText != null)
                {
                    scoreText.text = playerNames[i] + ": 0";
                    scoreText.color = playerColors[i];
                    dynamicScoreTexts.Add(scoreText);
                }
            }
        }

        // Setup player-specific UI visibility
        SetupPlayerSpecificUI(playerCount);
    }

    private void SetupPlayerSpecificUI(int playerCount)
    {
        for (int i = 0; i < playerSpecificUI.Length; i++)
        {
            if (playerSpecificUI[i] != null)
            {
                // Only show UI elements for active players
                playerSpecificUI[i].SetActive(i < playerCount);
            }
        }
    }

    public void UpdateScoreUI(int playerIndex, int score)
    {
        if (playerIndex < dynamicScoreTexts.Count && dynamicScoreTexts[playerIndex] != null)
        {
            dynamicScoreTexts[playerIndex].text = playerNames[playerIndex] + ": " + score.ToString();
        }
    }

    public void UpdateCurrentPlayerUI(int currentPlayerIndex)
    {
        if (currentPlayerText != null && currentPlayerIndex < playerNames.Length)
        {
            currentPlayerText.text = playerNames[currentPlayerIndex] + "'s Turn";
            currentPlayerText.color = playerColors[currentPlayerIndex];
        }

        // Show/hide player-specific UI based on current turn
        UpdatePlayerSpecificUIVisibility(currentPlayerIndex);
    }

    private void UpdatePlayerSpecificUIVisibility(int currentPlayerIndex)
    {
        // Get local player index (this would need to be set when player connects)
        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        bool isMyTurn = (ulong)currentPlayerIndex == localClientId;

        for (int i = 0; i < playerSpecificUI.Length; i++)
        {
            if (playerSpecificUI[i] != null)
            {
                // Only show UI for the current player
                bool shouldShow = (i == currentPlayerIndex) && isMyTurn;
                playerSpecificUI[i].SetActive(shouldShow);
            }
        }
    }

    public void UpdateConnectedPlayersText(int connectedCount, int targetCount)
    {
        if (connectedPlayersText != null)
        {
            connectedPlayersText.text = $"Players Connected: {connectedCount}/{targetCount}";
        }
    }

    public void OnGameStarted()
    {
        if (waitingMenu != null)
            waitingMenu.SetActive(false);

        // Enable game UI
        EnableGameUI();
    }

    private void EnableGameUI()
    {
        // Enable score displays
        foreach (var scoreText in dynamicScoreTexts)
        {
            if (scoreText != null)
                scoreText.gameObject.SetActive(true);
        }

        // Enable other game UI elements
        if (currentPlayerText != null)
            currentPlayerText.gameObject.SetActive(true);
    }

    // Called by NetworkGameManager when network variables change
    public void OnNetworkVariableChanged()
    {
        if (networkGameManager == null) return;

        // Update UI based on current game state
        StartCoroutine(UpdateUINextFrame());
    }

    private IEnumerator UpdateUINextFrame()
    {
        yield return new WaitForEndOfFrame();

        if (networkGameManager != null && networkGameManager.IsGameStarted())
        {
            // Update current player display
            UpdateCurrentPlayerUI(networkGameManager.GetCurrentPlayerIndex());

            // Update scores
            for (int i = 0; i < dynamicScoreTexts.Count; i++)
            {
                UpdateScoreUI(i, networkGameManager.GetPlayerScore(i));
            }
        }
    }

    // Public methods for external scripts
    public void ShowScoreUI(bool show)
    {
        foreach (var scoreText in dynamicScoreTexts)
        {
            if (scoreText != null)
                scoreText.gameObject.SetActive(show);
        }
    }

    public void SetPlayerCountUI(int count)
    {
        SetupDynamicScoreUI(count);
    }

    public Color GetPlayerColor(int playerIndex)
    {
        return playerIndex < playerColors.Length ? playerColors[playerIndex] : Color.white;
    }

    public string GetPlayerName(int playerIndex)
    {
        return playerIndex < playerNames.Length ? playerNames[playerIndex] : "Player " + (playerIndex + 1);
    }
}