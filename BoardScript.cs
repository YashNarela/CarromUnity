

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