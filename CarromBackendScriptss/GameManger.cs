
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Colyseus;

public class GameManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject strikerPrefab;
    public GameObject whitePrefab;
    public GameObject blackPrefab;
    public GameObject queenPrefab;

    [Header("UI")]
    public Slider powerSlider;
    public TextMeshProUGUI playerText, enemyText, turnText, updatesText;
    public Button pauseButton;
    public Canvas canvas;

    [Header("Visuals")]
    public Material myTurnMaterial, enemyTurnMaterial, defaultMaterial;

    // Board and piece constants
    const float UNITY_SIZE = 11.7657f;
    const float BACKEND_SIZE = 600f;
    const float STRIKER_MIN_X = -3.4f;
    const float STRIKER_MAX_X = 3.4f;
    const float STRIKER_Y = -4.5f;

    // Networking
    private ColyseusRoom<CarromGameState> room;
    private string mySessionId;
    private bool isMyTurn;

    // Piece management
    private Dictionary<string, GameObject> pieces = new Dictionary<string, GameObject>();
    private GameObject striker;
    private StrikerController strikerController;

    // For UI updates
    private Dictionary<string, Player> playersData = new Dictionary<string, Player>();

    void Start()
    {
        SetupUI();
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected())
        {
            SetRoom(NetworkManager.Instance.GetRoom());
        }
    }

    void SetupUI()
    {
        if (pauseButton != null)
            pauseButton.onClick.AddListener(OnPauseClicked);

        if (powerSlider != null)
        {
            powerSlider.minValue = 5f;
            powerSlider.maxValue = 100f;
            powerSlider.value = 50f;
            powerSlider.gameObject.SetActive(false);
        }
    }

    public void SetRoom(ColyseusRoom<CarromGameState> gameRoom)
    {
        room = gameRoom;
        mySessionId = room?.SessionId;
        room.OnStateChange += OnStateChange;
    }

    void OnStateChange(CarromGameState state, bool isFirstState)
    {
        if (isFirstState)
            InitializeFromState(state);
        else
            UpdateFromState(state);

        UpdateUI(state);
    }

    void InitializeFromState(CarromGameState state)
    {
        ClearAllPieces();
        pieces.Clear();
        playersData.Clear();

        foreach (string key in state.players.Keys)
        {
            playersData[key] = state.players[key];
        }

        foreach (string key in state.pieces.Keys)
        {
            var piece = state.pieces[key];
            CreateOrUpdatePiece(key, piece);
        }
    }

    void UpdateFromState(CarromGameState state)
    {
        foreach (string key in state.pieces.Keys)
        {
            var piece = state.pieces[key];
            CreateOrUpdatePiece(key, piece);
        }

        foreach (string key in state.players.Keys)
        {
            playersData[key] = state.players[key];
        }
    }

    //void CreateOrUpdatePiece(string key, CarromPiece piece)
    //{
    //    Vector3 pos = BackendToUnity(piece.x, piece.y);

    //    GameObject prefab = null;
    //    switch (piece.type)
    //    {
    //        case "striker": prefab = strikerPrefab; break;
    //        case "white": prefab = whitePrefab; break;
    //        case "black": prefab = blackPrefab; break;
    //        case "queen": prefab = queenPrefab; break;
    //    }

    //    if (!pieces.ContainsKey(key))
    //    {
    //        // For striker, always spawn at (0, -4.5f, 0)
    //        Vector3 spawnPos = piece.type == "striker" ? new Vector3(0f, -4.55f, 0f) : pos;

    //        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);
    //        obj.name = key;
    //        pieces[key] = obj;

    //        if (piece.type == "striker")
    //        {
    //            striker = obj;
    //            strikerController = striker.GetComponent<StrikerController>();
    //            if (strikerController == null)
    //                strikerController = striker.AddComponent<StrikerController>();
    //            strikerController.gameManager = this;
    //            // No need to set position again, already spawned at correct spot!
    //        }
    //    }
    //    else
    //    {
    //        // Only update position if NOT my turn, or if out of sync
    //        if (piece.type == "striker" && strikerController != null)
    //        {
    //            if (!IsMyTurn() ||
    //                Vector3.Distance(pieces[key].transform.position, pos) > 0.01f)
    //            {
    //                pieces[key].transform.position = pos;
    //            }
    //        }
    //        else
    //        {
    //            pieces[key].transform.position = pos;
    //        }
    //    }

    //    pieces[key].SetActive(!piece.isPocketed);
    //}

    void CreateOrUpdatePiece(string key, CarromPiece piece)
    {
        Vector3 pos = BackendToUnity(piece.x, piece.y);
        Vector3 spawnPos = piece.type == "striker" ? new Vector3(0f, -4.55f, 0f) : pos;

        GameObject prefab = null;
        switch (piece.type)
        {
            case "striker": prefab = strikerPrefab; break;
            case "white": prefab = whitePrefab; break;
            case "black": prefab = blackPrefab; break;
            case "queen": prefab = queenPrefab; break;
        }

        if (!pieces.ContainsKey(key))
        {
            GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);
            obj.name = key;
            pieces[key] = obj;

            if (piece.type == "striker")
            {
                striker = obj;
                strikerController = striker.GetComponent<StrikerController>();
                if (strikerController == null)
                    strikerController = striker.AddComponent<StrikerController>();
                strikerController.gameManager = this;
                // Always start at spawnPos
                striker.transform.position = spawnPos;
            }
        }
        else
        {
            // STRIKER: Only move from backend if NOT my turn or if pocketed
            if (piece.type == "striker" && strikerController != null)
            {
                if (!IsMyTurn() || piece.isPocketed)
                {
                    pieces[key].transform.position = pos;
                }
                // If it's my turn and striker is not pocketed, don't update position from backend!
            }
            else
            {
                pieces[key].transform.position = pos;
            }
        }

        pieces[key].SetActive(!piece.isPocketed);
    }


    // Now backend maps to X, Y (not Z)
    Vector3 BackendToUnity(float backendX, float backendY)
    {
        float unityX = (backendX / BACKEND_SIZE) * UNITY_SIZE - UNITY_SIZE / 2f;
        float unityY = (backendY / BACKEND_SIZE) * UNITY_SIZE - UNITY_SIZE / 2f;
        return new Vector3(unityX, unityY, 0);
    }

    Vector2 UnityToBackend(float unityX, float unityY)
    {
        float backendX = ((unityX + UNITY_SIZE / 2f) / UNITY_SIZE) * BACKEND_SIZE;
        float backendY = ((unityY + UNITY_SIZE / 2f) / UNITY_SIZE) * BACKEND_SIZE;
        return new Vector2(backendX, backendY);
    }

    float UnityToBackendX(float unityX)
    {
        return ((unityX + UNITY_SIZE / 2f) / UNITY_SIZE) * BACKEND_SIZE;
    }

    public void MoveStriker(float unityX)
    {
        if (!isMyTurn || room == null) return;
        unityX = Mathf.Clamp(unityX, STRIKER_MIN_X, STRIKER_MAX_X);
        float backendX = UnityToBackendX(unityX);
        room.Send("moveStriker", new { x = backendX });
    }

    public void ShootStriker(Vector3 dragStart, Vector3 dragEnd, float power)
    {
        if (!isMyTurn || room == null) return;
        Vector2 backendStart = UnityToBackend(dragStart.x, dragStart.y);
        Vector2 backendEnd = UnityToBackend(dragEnd.x, dragEnd.y);

        room.Send("shoot", new
        {
            dragStart = new { x = backendStart.x, y = backendStart.y },
            dragEnd = new { x = backendEnd.x, y = backendEnd.y },
            power = power
        });
    }

    void UpdateUI(CarromGameState state)
    {
        isMyTurn = state.players.ContainsKey(mySessionId) && state.players[mySessionId].isActive;
        if (turnText != null)
        {
            turnText.text = isMyTurn ? "Your Turn" : "Opponent's Turn";
            turnText.color = isMyTurn ? Color.green : Color.yellow;
        }
        if (powerSlider != null)
            powerSlider.gameObject.SetActive(isMyTurn);

        // Show/hide striker slider and striker based on turn
        if (strikerController != null && strikerController.strikerSlider != null)
            strikerController.strikerSlider.gameObject.SetActive(isMyTurn);
        if (strikerController != null)
            strikerController.gameObject.SetActive(isMyTurn);

        if (strikerController != null)
            strikerController.enabled = isMyTurn;

        foreach (var playerObj in state.players.Values)
        {
            Player player = playerObj as Player;
            if (player == null) continue; // Defensive: skip if cast fails

            if (player.sessionId == mySessionId && playerText != null)
                playerText.text = $"Score: {player.score}";
            else if (enemyText != null)
                enemyText.text = $"Opponent Score: {player.score}";
        }
        // Events
        if (state.events.Count > 0 && updatesText != null)
        {
            var evt = state.events[state.events.Count - 1];
            updatesText.text = evt.message ?? "";
        }
    }

    void OnPauseClicked()
    {
        if (room != null) room.Send("pauseGame");
    }

    void ClearAllPieces()
    {
        foreach (var obj in pieces.Values)
            Destroy(obj);
        pieces.Clear();
    }

    public bool IsMyTurn() => isMyTurn;
    public float GetPowerSliderValue() => powerSlider?.value ?? 50f;

    void OnDestroy()
    {
        if (room != null)
            room.OnStateChange -= OnStateChange;
        ClearAllPieces();
    }
}