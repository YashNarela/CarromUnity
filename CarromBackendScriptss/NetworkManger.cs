
using UnityEngine;
using Colyseus;
using System.Collections.Generic;

public class NetworkManager : MonoBehaviour
{
    public string serverUrl = "ws://localhost:5000";
    public string roomName = "carrom_game";
    public string playerName = "";

    public GameManager gameManager;

    private ColyseusClient client;
    private ColyseusRoom<CarromGameState> room;
    public static NetworkManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (string.IsNullOrEmpty(playerName))
            playerName = "Player_" + Random.Range(1000, 9999);

        ConnectToServer();
    }

    public async void ConnectToServer()
    {
        client = new ColyseusClient(serverUrl);
        var options = new Dictionary<string, object> { ["name"] = playerName };
        room = await client.JoinOrCreate<CarromGameState>(roomName, options);
        if (gameManager != null)
            gameManager.SetRoom(room);
    }

    public bool IsConnected() => room != null;
    public ColyseusRoom<CarromGameState> GetRoom() => room;
}