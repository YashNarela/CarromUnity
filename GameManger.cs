

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    bool gameOver = false;
    bool isPaused = false;

    // TextMeshProUGUI variables for displaying scores, game over text, and instructions.
    [SerializeField]
    TextMeshProUGUI scoreTextEnemy;

    [SerializeField]
    TextMeshProUGUI scoreTextPlayer;

    [SerializeField]
    TextMeshProUGUI gameOverText;

    [SerializeField]
    TextMeshProUGUI instructionsText;

    [SerializeField]
    TextMeshProUGUI turnTimerText;

    // Game object variables for menus, strikers, turn text, and a slider.
    [SerializeField]
    GameObject instructionsMenu;

    [SerializeField]
    GameObject pauseMenu;

    [SerializeField]
    GameObject gameOverMenu;

    [SerializeField]
    GameObject playerStriker;

    [SerializeField]
    GameObject enemyStriker;

    [SerializeField]
    GameObject turnText;

    [SerializeField]
    GameObject slider;

    [SerializeField]
    Animator animator;

    TimerScript timerScript;

    // Turn timer variables
    private float turnTimeLeft = 10f;
    private bool isTurnTimerRunning = false;

    private const string FirstTimeLaunchKey = "FirstTimeLaunch";

    void Awake()
    {
        timerScript = GetComponent<TimerScript>();

        // Check if it's the first time launching the game.
        if (PlayerPrefs.GetInt(FirstTimeLaunchKey, 0) == 0)
        {
            timerScript.isTimerRunning = false;
            Time.timeScale = 0;
            instructionsMenu.SetActive(true);
            PlayerPrefs.SetInt(FirstTimeLaunchKey, 1);
        }
        else
        {
            timerScript.isTimerRunning = true;
            instructionsMenu.SetActive(false);
        }
    }

    void Start()
    {
        Time.timeScale = 1;
        BoardScript.scoreEnemy = 0;
        BoardScript.scorePlayer = 0;
        StartTurnTimer();
    }

    void Update()
    {
        if (StrikerController.playerTurn == true)
        {
            slider.SetActive(true);
            turnText.SetActive(true);
            playerStriker.SetActive(true);
            enemyStriker.SetActive(false);
        }
        else
        {
            slider.SetActive(false);
            turnText.SetActive(false);
            playerStriker.SetActive(false);
            enemyStriker.SetActive(true);
        }

        // Update turn timer
        if (isTurnTimerRunning && !isPaused && !gameOver)
        {
            turnTimeLeft -= Time.deltaTime;
            if (turnTimerText != null)
            {
                turnTimerText.text = "Turn: " + Mathf.Ceil(turnTimeLeft).ToString();

                // Change color when time is running out
                if (turnTimeLeft <= 3f)
                {
                    turnTimerText.color = Color.red;
                }
                else
                {
                    turnTimerText.color = Color.white;
                }
            }

            if (turnTimeLeft <= 0)
            {
                HandleTurnTimeout();
            }
        }

        // Check win condition based on all pieces cleared or time up
        if (CheckWinCondition() || timerScript.timeLeft <= 0)
        {
            onGameOver();
        }

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
    }

    private void LateUpdate()
    {
        if (!gameOver)
        {
            scoreTextEnemy.text = BoardScript.scoreEnemy.ToString();
            scoreTextPlayer.text = BoardScript.scorePlayer.ToString();
        }
    }

    private bool CheckWinCondition()
    {
        // Count remaining pieces on board
        GameObject[] whites = GameObject.FindGameObjectsWithTag("White");
        GameObject[] blacks = GameObject.FindGameObjectsWithTag("Black");
        GameObject queen = GameObject.FindGameObjectWithTag("Queen");

        // Game ends when all whites, all blacks, or all pieces are cleared
        bool allWhitesCleared = whites.Length == 0;
        bool allBlacksCleared = blacks.Length == 0;
        bool allPiecesCleared = allWhitesCleared && allBlacksCleared && queen == null;

        return allWhitesCleared || allBlacksCleared || allPiecesCleared;
    }

    void StartTurnTimer()
    {
        turnTimeLeft = 10f;
        isTurnTimerRunning = true;
        if (turnTimerText != null)
        {
            turnTimerText.color = Color.white;
        }
    }

    void StopTurnTimer()
    {
        isTurnTimerRunning = false;
    }

    void HandleTurnTimeout()
    {
        StopTurnTimer();

        // Switch turns when time runs out
        StrikerController.playerTurn = !StrikerController.playerTurn;

        StartTurnTimer();

        Debug.Log("Turn timeout! Switching to " + (StrikerController.playerTurn ? "Player" : "Enemy"));
    }

    public void OnShotTaken()
    {
        StopTurnTimer();
    }

    public void OnShotComplete(bool continueTurn)
    {
        // Check for failed queen cover when turn ends
        if (!continueTurn)
        {
            CheckQueenCover();
            StrikerController.playerTurn = !StrikerController.playerTurn;
        }
        StartTurnTimer();
    }

    private void CheckQueenCover()
    {
        // Check if current player failed to cover queen
        if (BoardScript.piecesPocketedThisTurn == 0) // No pieces pocketed this turn
        {
            if (BoardScript.playerNeedsToCoverQueen && StrikerController.playerTurn)
            {
                BoardScript.playerNeedsToCoverQueen = false;
                BoardScript.playerHasQueen = false;
                BoardScript.scorePlayer = Mathf.Max(0, BoardScript.scorePlayer - 50);
                RespawnQueen();
                Debug.Log("Player failed to cover queen! -50 points, Queen respawned");
            }
            else if (BoardScript.enemyNeedsToCoverQueen && !StrikerController.playerTurn)
            {
                BoardScript.enemyNeedsToCoverQueen = false;
                BoardScript.enemyHasQueen = false;
                BoardScript.scoreEnemy = Mathf.Max(0, BoardScript.scoreEnemy - 50);
                RespawnQueen();
                Debug.Log("Enemy failed to cover queen! -50 points, Queen respawned");
            }
        }
    }

    //private void RespawnQueen()
    //{
    //    // Find if queen prefab exists in the scene or create one
    //    GameObject queen = GameObject.FindGameObjectWithTag("Queen");

    //    if (queen == null)
    //    {
    //        // If no queen exists, you need to instantiate it
    //        // You'll need to assign a queen prefab in the inspector for this to work
    //        // For now, let's assume you have a queen prefab reference
    //        // GameObject queenPrefab = Resources.Load<GameObject>("QueenPrefab");
    //        // queen = Instantiate(queenPrefab);

    //        Debug.LogWarning("Queen prefab not found! You need to create a queen prefab and instantiate it here.");
    //        return;
    //    }

    //    // Reset queen to center of board
    //    queen.transform.position = new Vector3(0f, 0f, 0f);
    //    queen.SetActive(true);

    //    // Make sure queen has proper physics
    //    Rigidbody2D queenRb = queen.GetComponent<Rigidbody2D>();
    //    if (queenRb != null)
    //    {
    //        queenRb.velocity = Vector2.zero;
    //        queenRb.angularVelocity = 0f;
    //    }
    //}
    [SerializeField] private GameObject queenPrefab; // Assign prefab in inspector

    private void RespawnQueen()
    {
        GameObject queen = GameObject.FindGameObjectWithTag("Queen");

        if (queen == null)
        {
            if (queenPrefab != null)
            {
                queen = Instantiate(queenPrefab, Vector3.zero, Quaternion.identity);
                queen.tag = "Queen";
            }
            else
            {
                Debug.LogError("No Queen Prefab assigned in GameManager!");
                return;
            }
        }
        else
        {
            queen.transform.position = Vector3.zero;
            queen.SetActive(true);
        }

        Rigidbody2D queenRb = queen.GetComponent<Rigidbody2D>();
        if (queenRb != null)
        {
            queenRb.velocity = Vector2.zero;
            queenRb.angularVelocity = 0f;
        }
    }

    IEnumerator playAnimation()
    {
        animator.SetTrigger("fade");
        yield return new WaitForSeconds(1f);
    }

    void onGameOver()
    {
        gameOver = true;
        StopTurnTimer();
        gameOverMenu.SetActive(true);
        Time.timeScale = 0;
        if (BoardScript.scoreEnemy > BoardScript.scorePlayer)
        {
            gameOverText.text = "You Lose!";
        }
        else if (BoardScript.scoreEnemy < BoardScript.scorePlayer)
        {
            gameOverText.text = "You Win!";
        }
        else
        {
            gameOverText.text = "Draw!";
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
        SceneManager.LoadScene(1);
    }

    public void QuitGame()
    {
        StartCoroutine(playAnimation());
        SceneManager.LoadScene(0);
    }

    public void NextPage()
    {
        instructionsText.pageToDisplay++;
        if (instructionsText.pageToDisplay == 3)
        {
            Time.timeScale = 1;
            timerScript.isTimerRunning = true;
            instructionsMenu.SetActive(false);
        }
    }
}