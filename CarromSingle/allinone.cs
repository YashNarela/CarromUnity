


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BoardScript : MonoBehaviour
{
    public static int scoreEnemy = 0;
    public static int scorePlayer = 0;
    public static int piecesPocketedThisTurn = 0;
    public static bool strikerPocketedThisTurn = false;

    // Queen covering system
    public static bool playerHasQueen = false;
    public static bool enemyHasQueen = false;
    public static bool playerNeedsToCoverQueen = false;
    public static bool enemyNeedsToCoverQueen = false;

    // Pocket multipliers (4 pockets with different multipliers)
    public static float[] pocketMultipliers = { 1.0f, 1.2f, 1.5f, 2.0f };
    public static int currentMultiplierRotation = 0;

    [SerializeField]
    private int pocketIndex = 0; // Assign in inspector for each pocket (0-3)

    TextMeshProUGUI popUpText;

    private void Start()
    {
        // Find the UpdatesText object and get the TextMeshProUGUI component
        popUpText = GameObject.Find("UpdatesText").GetComponent<TextMeshProUGUI>();
    }

    IEnumerator textPopUp(string text)
    {
        // Set the text and activate the UpdatesText object
        popUpText.text = text;
        popUpText.gameObject.SetActive(true);
        yield return new WaitForSeconds(3f);
        // Deactivate the UpdatesText object after 3 seconds
        popUpText.gameObject.SetActive(false);
    }

    private float GetCurrentMultiplier()
    {
        int multiplierIndex = (pocketIndex + currentMultiplierRotation) % pocketMultipliers.Length;
        return pocketMultipliers[multiplierIndex];
    }

    public static void RotateMultipliers()
    {
        currentMultiplierRotation = (currentMultiplierRotation + 1) % pocketMultipliers.Length;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Play audio when a coin/striker enters the pocket
        GetComponent<AudioSource>().Play();

        float multiplier = GetCurrentMultiplier();

        switch (other.gameObject.tag)
        {
            case "Striker":
                strikerPocketedThisTurn = true;
                int penaltyPoints = Mathf.RoundToInt(10 * multiplier);

                if (StrikerController.playerTurn == true)
                {
                    scorePlayer = Mathf.Max(0, scorePlayer - penaltyPoints);
                    StartCoroutine(textPopUp($"Striker Lost! -{penaltyPoints} to Player (x{multiplier:F1})"));
                }
                else
                {
                    scoreEnemy = Mathf.Max(0, scoreEnemy - penaltyPoints);
                    StartCoroutine(textPopUp($"Striker Lost! -{penaltyPoints} to Enemy (x{multiplier:F1})"));
                }

                other.gameObject.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                break;

            case "Black":
                piecesPocketedThisTurn++;
                int blackPoints = Mathf.RoundToInt(10 * multiplier);

                if (StrikerController.playerTurn == true)
                {
                    scorePlayer += blackPoints;
                    StartCoroutine(textPopUp($"Black Coin! +{blackPoints} to Player (x{multiplier:F1})"));

                    // Check if player needs to cover queen
                    if (playerNeedsToCoverQueen)
                    {
                        playerNeedsToCoverQueen = false;
                        scorePlayer += 50; // Queen bonus
                        StartCoroutine(textPopUp("Queen Covered! +50 bonus to Player"));
                    }
                }
                else
                {
                    scoreEnemy += blackPoints;
                    StartCoroutine(textPopUp($"Black Coin! +{blackPoints} to Enemy (x{multiplier:F1})"));

                    // Check if enemy needs to cover queen
                    if (enemyNeedsToCoverQueen)
                    {
                        enemyNeedsToCoverQueen = false;
                        scoreEnemy += 50; // Queen bonus
                        StartCoroutine(textPopUp("Queen Covered! +50 bonus to Enemy"));
                    }
                }

                Destroy(other.gameObject);
                break;

            case "White":
                piecesPocketedThisTurn++;
                int whitePoints = Mathf.RoundToInt(20 * multiplier);

                if (StrikerController.playerTurn == true)
                {
                    scorePlayer += whitePoints;
                    StartCoroutine(textPopUp($"White Coin! +{whitePoints} to Player (x{multiplier:F1})"));

                    // Check if player needs to cover queen
                    if (playerNeedsToCoverQueen)
                    {
                        playerNeedsToCoverQueen = false;
                        scorePlayer += 50; // Queen bonus
                        StartCoroutine(textPopUp("Queen Covered! +50 bonus to Player"));
                    }
                }
                else
                {
                    scoreEnemy += whitePoints;
                    StartCoroutine(textPopUp($"White Coin! +{whitePoints} to Enemy (x{multiplier:F1})"));

                    // Check if enemy needs to cover queen
                    if (enemyNeedsToCoverQueen)
                    {
                        enemyNeedsToCoverQueen = false;
                        scoreEnemy += 50; // Queen bonus
                        StartCoroutine(textPopUp("Queen Covered! +50 bonus to Enemy"));
                    }
                }

                Destroy(other.gameObject);
                break;

            case "Queen":
                piecesPocketedThisTurn++;
                int queenPoints = Mathf.RoundToInt(50 * multiplier);

                if (StrikerController.playerTurn == true)
                {
                    if (!playerHasQueen)
                    {
                        playerHasQueen = true;
                        playerNeedsToCoverQueen = true;
                        scorePlayer += queenPoints;
                        StartCoroutine(textPopUp($"Queen Pocketed! +{queenPoints} to Player (x{multiplier:F1}) - Must Cover!"));
                    }
                }
                else
                {
                    if (!enemyHasQueen)
                    {
                        enemyHasQueen = true;
                        enemyNeedsToCoverQueen = true;
                        scoreEnemy += queenPoints;
                        StartCoroutine(textPopUp($"Queen Pocketed! +{queenPoints} to Enemy (x{multiplier:F1}) - Must Cover!"));
                    }
                }

                Destroy(other.gameObject);
                break;
        }

        // Check for failed queen cover only when turn actually ends (handled by GameManager)
        // Removed automatic queen cover check from here since it should happen at turn end, not during pocketing
    }
}




using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StrikerController : MonoBehaviour
{
    [SerializeField]
    float strikerSpeed = 100f;

    [SerializeField]
    float maxScale = 1f;

    [SerializeField]
    Transform strikerForceField;

    [SerializeField]
    Slider strikerSlider;

    bool isMoving;
    bool isCharging;
    float maxForceMagnitude = 30f;
    Rigidbody2D rb;

    public static bool playerTurn;
    private GameManager gameManager;

    private void Start()
    {
        playerTurn = true;
        isMoving = false;
        rb = GetComponent<Rigidbody2D>();
        gameManager = FindObjectOfType<GameManager>();
    }

    private void OnEnable()
    {
        // Reset the position of the striker when it is enabled
        ResetStrikerPosition();
        CollisionSoundManager.shouldBeStatic = true;
    }

    public void ResetStrikerPosition()
    {
        if (strikerSlider != null)
        {
            transform.position = new Vector3(strikerSlider.value, -4.57f, 0);
        }
        else
        {
            transform.position = new Vector3(0, -4.57f, 0);
        }

        if (strikerForceField != null)
        {
            strikerForceField.LookAt(transform.position);
            strikerForceField.localScale = new Vector3(0, 0, 0);
        }

        rb.velocity = Vector2.zero;
        isMoving = false;
        isCharging = false;
    }

    private void Update()
    {
        // Check if the striker has come to a near stop and is not moving
        if (rb.velocity.magnitude < 0.1f && !isMoving)
        {
            isMoving = true;
            StartCoroutine(OnMouseUp());
        }
    }

    private void OnMouseDown()
    {
        // Only allow input during player's turn
        if (!playerTurn) return;

        // If the striker is moving, disable charging and return
        if (rb.velocity.magnitude > 0.1f)
        {
            isCharging = false;
            return;
        }

        // Reset the position of the striker if it is not at the correct y-axis position
        if (transform.position.y != -4.57f)
        {
            transform.position = new Vector3(0, -4.57f, 0);
        }

        // Enable charging and show the striker force field
        isCharging = true;
        if (strikerForceField != null)
        {
            strikerForceField.gameObject.SetActive(true);
        }
    }

    private IEnumerator OnMouseUp()
    {
        isMoving = true;
        yield return new WaitForSeconds(0.1f);

        // If charging is not enabled, exit the coroutine
        if (!isCharging)
        {
            yield break;
        }

        if (strikerForceField != null)
        {
            strikerForceField.gameObject.SetActive(false);
        }
        isCharging = false;

        // Notify GameManager that shot was taken
        if (gameManager != null)
        {
            gameManager.OnShotTaken();
        }

        yield return new WaitForSeconds(0.1f);

        // Calculate the direction and magnitude of the force based on the mouse position
        Vector3 direction = transform.position - Camera.main.ScreenToWorldPoint(Input.mousePosition);
        direction.z = 0f;
        float forceMagnitude = direction.magnitude * strikerSpeed;
        forceMagnitude = Mathf.Clamp(forceMagnitude, 0f, maxForceMagnitude);

        // Apply the force to the striker
        rb.AddForce(direction.normalized * forceMagnitude, ForceMode2D.Impulse);

        CollisionSoundManager.shouldBeStatic = false;
        yield return new WaitForSeconds(0.1f);

        // Wait until the striker comes to a near stop
        yield return new WaitUntil(() => rb.velocity.magnitude < 0.1f);

        isMoving = false;

        // Check if any pieces were pocketed this turn to determine if player continues
        bool continueTurn = BoardScript.piecesPocketedThisTurn > 0 && !BoardScript.strikerPocketedThisTurn;

        // Notify GameManager about shot completion
        if (gameManager != null)
        {
            gameManager.OnShotComplete(continueTurn);
        }

        // Reset turn tracking variables after GameManager processes
        BoardScript.piecesPocketedThisTurn = 0;
        BoardScript.strikerPocketedThisTurn = false;

        gameObject.SetActive(false);
    }

    public static void SwitchTurn()
    {
        playerTurn = !playerTurn;
        Debug.Log("Turn switched to: " + (playerTurn ? "Player" : "Enemy"));
    }

    private void OnMouseDrag()
    {
        // Only allow input during player's turn
        if (!playerTurn) return;

        // If charging is not enabled, return
        if (!isCharging || strikerForceField == null)
        {
            return;
        }

        // Update the direction and scale of the striker force field based on the mouse position
        Vector3 direction = transform.position - Camera.main.ScreenToWorldPoint(Input.mousePosition);
        direction.z = 0f;
        strikerForceField.LookAt(transform.position + direction);

        float scaleValue = Vector3.Distance(transform.position, transform.position + direction / 4f);

        if (scaleValue > maxScale)
        {
            scaleValue = maxScale;
        }

        strikerForceField.localScale = new Vector3(scaleValue, scaleValue, scaleValue);
    }

    public void SetSliderX()
    {
        // Set the X position of the striker based on the slider value
        if (rb.velocity.magnitude < 0.1f && strikerSlider != null)
        {
            transform.position = new Vector3(strikerSlider.value, -4.57f, 0);
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        // Play the collision sound if the striker collides with the board
        if (other.gameObject.CompareTag("Board"))
        {
            GetComponent<AudioSource>().Play();
        }
    }
}




using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyStrikerController : MonoBehaviour
{
    Rigidbody2D rb;
    bool isMoving;
    private GameManager gameManager;

    private void Start()
    {
        isMoving = false;
        rb = GetComponent<Rigidbody2D>();
        gameManager = FindObjectOfType<GameManager>();
    }

    private void Update()
    {
        // Check if the enemy striker has come to a near stop and is not moving
        if (rb.velocity.magnitude < 0.1f && !isMoving)
        {
            isMoving = true;
            StartCoroutine(EnemyTurn());
        }
    }

    private void OnEnable()
    {
        // Reset the initial state of the enemy striker
        CollisionSoundManager.shouldBeStatic = true;
        GetComponent<SpriteRenderer>().enabled = false;
        transform.position = new Vector3(0, 3.45f, 0f);
    }

    IEnumerator EnemyTurn()
    {
        // Notify GameManager that shot was taken
        if (gameManager != null)
        {
            gameManager.OnShotTaken();
        }

        // Determine the target coin based on game logic
        // Find the closest coin to a pocket
        const int maxAttempts = 10;
        int attempts = 0;
        bool isObstructed;

        do
        {
            isObstructed = false;

            // Generate a random position within the board bounds
            float x = Random.Range(-3.24f, 3.24f);
            transform.position = new Vector3(x, 3.45f, 0f);

            // Check if the generated position is obstructed by other coins or the striker
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 0.1f);

            foreach (Collider2D collider in colliders)
            {
                if (collider.CompareTag("Black") || collider.CompareTag("White") || collider.CompareTag("Striker"))
                {
                    isObstructed = true;
                    break;
                }
            }

            attempts++;
        }
        while (isObstructed && attempts < maxAttempts);

        if (isObstructed)
        {
            Debug.Log("Failed to find a valid position for the enemy striker.");
            transform.position = new Vector3(0f, 3.45f, 0f);
            isObstructed = false;
        }

        yield return new WaitWhile(() => isObstructed);
        GetComponent<SpriteRenderer>().enabled = true;

        yield return new WaitForSeconds(2f);
        CollisionSoundManager.shouldBeStatic = false;
        yield return new WaitForSeconds(0.1f);

        // Find all available coins (both black and white)
        List<GameObject> allCoins = new List<GameObject>();
        allCoins.AddRange(GameObject.FindGameObjectsWithTag("Black"));
        allCoins.AddRange(GameObject.FindGameObjectsWithTag("White"));

        // Add queen if it exists
        GameObject queen = GameObject.FindGameObjectWithTag("Queen");
        if (queen != null)
        {
            allCoins.Add(queen);
        }

        GameObject closestCoin = null;
        float closestDistance = Mathf.Infinity;

        if (allCoins.Count == 0)
        {
            Debug.Log("No coins left");
            yield break;
        }

        // Find the closest coin to a pocket
        foreach (GameObject coin in allCoins)
        {
            float distance = Vector3.Distance(coin.transform.position, GetClosestPocket(coin.transform.position));
            if (distance < closestDistance)
            {
                closestCoin = coin;
                closestDistance = distance;
            }
        }

        // Calculate the direction and speed of the striker based on the position of the target coin and the enemy's striker.
        Vector3 targetDirection = closestCoin.transform.position - transform.position;
        targetDirection.z = 0f;
        float targetSpeed = CalculateStrikerSpeed(targetDirection.magnitude);

        // Apply the calculated force to the striker and end the enemy's turn.
        rb.AddForce(targetDirection.normalized * targetSpeed, ForceMode2D.Impulse);

        yield return new WaitForSeconds(0.1f);
        yield return new WaitUntil(() => rb.velocity.magnitude < 0.1f);

        isMoving = false;

        // Check if enemy should continue turn (same logic as player)
        bool continueTurn = BoardScript.piecesPocketedThisTurn > 0 && !BoardScript.strikerPocketedThisTurn;

        // Notify GameManager about shot completion
        if (gameManager != null)
        {
            gameManager.OnShotComplete(continueTurn);
        }

        // Reset turn tracking variables after GameManager processes
        BoardScript.piecesPocketedThisTurn = 0;
        BoardScript.strikerPocketedThisTurn = false;

        gameObject.SetActive(false);
    }

    Vector3 GetClosestPocket(Vector3 position)
    {
        // Find the closest pocket to a given position
        Vector3 closestPocket = Vector3.zero;
        float closestDistance = Mathf.Infinity;
        GameObject[] pockets = GameObject.FindGameObjectsWithTag("Pocket");

        foreach (GameObject pocket in pockets)
        {
            float distance = Vector3.Distance(position, pocket.transform.position);
            if (distance < closestDistance)
            {
                closestPocket = pocket.transform.position;
                closestDistance = distance;
            }
        }

        return closestPocket;
    }

    float CalculateStrikerSpeed(float distance)
    {
        // Calculate the speed of the striker based on the distance to the target coin
        float maxDistance = 4.0f; // Maximum distance the striker can travel
        float minSpeed = 10f; // Minimum striker speed
        float maxSpeed = 30f; // Maximum striker speed

        float speed = Mathf.Lerp(minSpeed, maxSpeed, distance / maxDistance);
        return speed;
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        // Play the collision sound if the enemy striker collides with the board
        if (other.gameObject.CompareTag("Board"))
        {
            GetComponent<AudioSource>().Play();
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SwitchScene : MonoBehaviour
{
    [SerializeField]
    Animator animator;

    private void Start()
    {
        Time.timeScale = 1;
    }

    public void SwitchToScene()
    {
        StartCoroutine(LoadScene(1));
    }

    public void ExitGame()
    {
        Application.Quit();
    }

    public IEnumerator LoadScene(int index)
    {
        // Trigger the "fade" animation.
        animator.SetTrigger("fade");

        // Wait for 1 second to allow the animation to play.
        yield return new WaitForSeconds(1f);

        // Load the specified scene by index.
        SceneManager.LoadScene(index);
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TimerScript : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI timerText;

    public float timeLeft = 120.0f;  // The time in seconds that the timer will run for
    public bool isTimerRunning; // Indicates whether the timer is currently running
    private bool isTimerSoundPlaying = false;

    void Update()
    {
        if (isTimerRunning)
        {
            timeLeft -= Time.deltaTime;  // Decrement the time left by the amount of time that has passed since the last frame
            timerText.text = Mathf.Round(timeLeft).ToString();  // Update the text to show the time left

            if (timeLeft <= 10)
            {
                timerText.color = Color.red;
                
                if (!isTimerSoundPlaying)
                {
                    // Play the AudioSource to indicate that time is running out
                    GetComponent<AudioSource>().Play();
                    isTimerSoundPlaying = true;
                }
            }

            if (timeLeft <= 0)
            {
                // Stop the AudioSource and set the timer to not running
                GetComponent<AudioSource>().Stop();
                isTimerRunning = false;
                timerText.text = "Time's Up!";
            }
        }
    }

}






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

            BoardScript.RotateMultipliers();
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