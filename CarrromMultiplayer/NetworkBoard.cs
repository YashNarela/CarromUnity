using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;

public class NetworkBoardScript : NetworkBehaviour
{
    // Keep static variables for backward compatibility
    public static int scoreEnemy = 0;
    public static int scorePlayer = 0;
    public static int piecesPocketedThisTurn = 0;
    public static bool strikerPocketedThisTurn = false;

    // Queen covering system - now handled by NetworkGameManager
    public static bool playerHasQueen = false;
    public static bool enemyHasQueen = false;
    public static bool playerNeedsToCoverQueen = false;
    public static bool enemyNeedsToCoverQueen = false;

    // Pocket multipliers
    public static float[] pocketMultipliers = { 1.0f, 1.2f, 1.5f, 2.0f };
    public static int currentMultiplierRotation = 0;

    [SerializeField]
    private int pocketIndex = 0;

    private TextMeshProUGUI popUpText;
    private NetworkGameManager networkGameManager;

    private void Start()
    {
        popUpText = GameObject.Find("UpdatesText").GetComponent<TextMeshProUGUI>();
        networkGameManager = FindObjectOfType<NetworkGameManager>();
    }

    IEnumerator textPopUp(string text)
    {
        if (popUpText != null)
        {
            popUpText.text = text;
            popUpText.gameObject.SetActive(true);
            yield return new WaitForSeconds(3f);
            popUpText.gameObject.SetActive(false);
        }
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
        if (!IsServer) return; // Only server processes pocketing

        // Play audio when a coin/striker enters the pocket
        PlayPocketSoundClientRpc();

        float multiplier = GetCurrentMultiplier();
        int currentPlayerIndex = networkGameManager.GetCurrentPlayerIndex();

        switch (other.gameObject.tag)
        {
            case "Striker":
                HandleStrikerPocketedServerRpc(multiplier, currentPlayerIndex);
                other.gameObject.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                break;

            case "Black":
                HandleBlackCoinPocketedServerRpc(multiplier, currentPlayerIndex);
                DestroyPieceClientRpc(other.gameObject.GetComponent<NetworkObject>().NetworkObjectId);
                break;

            case "White":
                HandleWhiteCoinPocketedServerRpc(multiplier, currentPlayerIndex);
                DestroyPieceClientRpc(other.gameObject.GetComponent<NetworkObject>().NetworkObjectId);
                break;

            case "Queen":
                HandleQueenPocketedServerRpc(multiplier, currentPlayerIndex);
                DestroyPieceClientRpc(other.gameObject.GetComponent<NetworkObject>().NetworkObjectId);
                break;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void HandleStrikerPocketedServerRpc(float multiplier, int playerIndex)
    {
        strikerPocketedThisTurn = true;
        int penaltyPoints = Mathf.RoundToInt(10 * multiplier);

        int currentScore = networkGameManager.GetPlayerScore(playerIndex);
        int newScore = Mathf.Max(0, currentScore - penaltyPoints);

        networkGameManager.UpdatePlayerScoreServerRpc(playerIndex, newScore);

        string playerName = GetPlayerName(playerIndex);
        ShowPopUpClientRpc($"Striker Lost! -{penaltyPoints} to {playerName} (x{multiplier:F1})");
    }

    [ServerRpc(RequireOwnership = false)]
    private void HandleBlackCoinPocketedServerRpc(float multiplier, int playerIndex)
    {
        piecesPocketedThisTurn++;
        int blackPoints = Mathf.RoundToInt(10 * multiplier);

        int currentScore = networkGameManager.GetPlayerScore(playerIndex);
        int newScore = currentScore + blackPoints;

        networkGameManager.UpdatePlayerScoreServerRpc(playerIndex, newScore);

        string playerName = GetPlayerName(playerIndex);
        ShowPopUpClientRpc($"Black Coin! +{blackPoints} to {playerName} (x{multiplier:F1})");

        // Check if player needs to cover queen
        if (networkGameManager.PlayerNeedsToCoverQueen(playerIndex))
        {
            networkGameManager.UpdateQueenStatusServerRpc(playerIndex, true, false);
            networkGameManager.UpdatePlayerScoreServerRpc(playerIndex, newScore + 50);
            ShowPopUpClientRpc($"Queen Covered! +50 bonus to {playerName}");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void HandleWhiteCoinPocketedServerRpc(float multiplier, int playerIndex)
    {
        piecesPocketedThisTurn++;
        int whitePoints = Mathf.RoundToInt(20 * multiplier);

        int currentScore = networkGameManager.GetPlayerScore(playerIndex);
        int newScore = currentScore + whitePoints;

        networkGameManager.UpdatePlayerScoreServerRpc(playerIndex, newScore);

        string playerName = GetPlayerName(playerIndex);
        ShowPopUpClientRpc($"White Coin! +{whitePoints} to {playerName} (x{multiplier:F1})");

        // Check if player needs to cover queen
        if (networkGameManager.PlayerNeedsToCoverQueen(playerIndex))
        {
            networkGameManager.UpdateQueenStatusServerRpc(playerIndex, true, false);
            networkGameManager.UpdatePlayerScoreServerRpc(playerIndex, newScore + 50);
            ShowPopUpClientRpc($"Queen Covered! +50 bonus to {playerName}");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void HandleQueenPocketedServerRpc(float multiplier, int playerIndex)
    {
        piecesPocketedThisTurn++;
        int queenPoints = Mathf.RoundToInt(50 * multiplier);

        if (!networkGameManager.PlayerHasQueen(playerIndex))
        {
            networkGameManager.UpdateQueenStatusServerRpc(playerIndex, true, true);

            int currentScore = networkGameManager.GetPlayerScore(playerIndex);
            int newScore = currentScore + queenPoints;
            networkGameManager.UpdatePlayerScoreServerRpc(playerIndex, newScore);

            string playerName = GetPlayerName(playerIndex);
            ShowPopUpClientRpc($"Queen Pocketed! +{queenPoints} to {playerName} (x{multiplier:F1}) - Must Cover!");
        }
    }

    [ClientRpc]
    private void PlayPocketSoundClientRpc()
    {
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.Play();
        }
    }

    [ClientRpc]
    private void ShowPopUpClientRpc(string message)
    {
        StartCoroutine(textPopUp(message));
    }

    [ClientRpc]
    private void DestroyPieceClientRpc(ulong networkObjectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject))
        {
            if (networkObject != null && networkObject.gameObject != null)
            {
                Destroy(networkObject.gameObject);
            }
        }
    }

    private string GetPlayerName(int playerIndex)
    {
        string[] playerNames = { "Player 1", "Player 2", "Player 3", "Player 4" };
        return playerIndex < playerNames.Length ? playerNames[playerIndex] : "Player " + (playerIndex + 1);
    }
}