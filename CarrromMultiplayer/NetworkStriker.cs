using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

public class NetworkStrikerController : NetworkBehaviour
{
    [SerializeField] private float strikerSpeed = 100f;
    [SerializeField] private float maxScale = 1f;
    [SerializeField] private Transform strikerForceField;
    [SerializeField] private Slider strikerSlider;
    [SerializeField] private int playerIndex = 0; // Set in inspector for each player striker

    private bool isMoving;
    private bool isCharging;
    private float maxForceMagnitude = 30f;
    private Rigidbody2D rb;
    private NetworkGameManager networkGameManager;

    // Network variables for striker state
    private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Vector2> networkVelocity = new NetworkVariable<Vector2>();

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        networkGameManager = FindObjectOfType<NetworkGameManager>();

        // Set striker color based on player index
        SetStrikerColor();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            networkPosition.Value = transform.position;
        }

        // Subscribe to network variable changes
        networkPosition.OnValueChanged += OnPositionChanged;
        networkVelocity.OnValueChanged += OnVelocityChanged;
    }

    private void SetStrikerColor()
    {
        Color[] playerColors = { Color.red, Color.blue, Color.green, Color.yellow };
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && playerIndex < playerColors.Length)
        {
            spriteRenderer.color = playerColors[playerIndex];
        }
    }

    private void OnEnable()
    {
        ResetStrikerPosition();
        CollisionSoundManager.shouldBeStatic = true;
    }

    public void ResetStrikerPosition()
    {
        Vector3 resetPosition;

        // Set different positions based on player index
        switch (playerIndex)
        {
            case 0: // Bottom
                resetPosition = new Vector3(0, -4.57f, 0);
                break;
            case 1: // Top
                resetPosition = new Vector3(0, 4.57f, 0);
                break;
            case 2: // Left
                resetPosition = new Vector3(-4.57f, 0, 0);
                break;
            case 3: // Right
                resetPosition = new Vector3(4.57f, 0, 0);
                break;
            default:
                resetPosition = new Vector3(0, -4.57f, 0);
                break;
        }

        transform.position = resetPosition;

        if (IsOwner)
        {
            UpdatePositionServerRpc(resetPosition);
        }

        if (strikerForceField == null)
            Debug.LogError($"{gameObject.name}: strikerForceField is NOT assigned!");
        else
        {
            strikerForceField.LookAt(transform.position);
            strikerForceField.localScale = Vector3.zero;
        }
        rb.velocity = Vector2.zero;
        isMoving = false;
        isCharging = false;
    }

    private void Update()
    {
        // Only the owner can control this striker
        if (!IsOwner) return;

        // Check if it's this player's turn
        if (!IsMyTurn()) return;

        // Check if the striker has come to a near stop
        if (rb.velocity.magnitude < 0.1f && !isMoving)
        {
            isMoving = true;
            StartCoroutine(OnMouseUp());
        }
    }

    private bool IsMyTurn()
    {
        return networkGameManager != null &&
               networkGameManager.GetCurrentPlayerIndex() == playerIndex &&
               networkGameManager.IsGameStarted();
    }

    private void OnMouseDown()
    {
        // Only allow input if it's this player's turn and they own this striker
        if (!IsOwner || !IsMyTurn()) return;

        // If the striker is moving, disable charging and return
        if (rb.velocity.magnitude > 0.1f)
        {
            isCharging = false;
            return;
        }

        // Reset the position of the striker if needed
        ResetToCorrectPosition();

        // Enable charging and show the striker force field
        isCharging = true;
        if (strikerForceField != null)
        {
            strikerForceField.gameObject.SetActive(true);
        }
    }

    private void ResetToCorrectPosition()
    {
        Vector3 correctPosition = GetCorrectPosition();
        if (Vector3.Distance(transform.position, correctPosition) > 0.1f)
        {
            transform.position = correctPosition;
            UpdatePositionServerRpc(correctPosition);
        }
    }

    private Vector3 GetCorrectPosition()
    {
        float sliderValue = strikerSlider != null ? strikerSlider.value : 0f;

        switch (playerIndex)
        {
            case 0: // Bottom
                return new Vector3(sliderValue, -4.57f, 0);
            case 1: // Top
                return new Vector3(sliderValue, 4.57f, 0);
            case 2: // Left
                return new Vector3(-4.57f, sliderValue, 0);
            case 3: // Right
                return new Vector3(4.57f, sliderValue, 0);
            default:
                return new Vector3(sliderValue, -4.57f, 0);
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

        // Notify NetworkGameManager that shot was taken
        if (networkGameManager != null)
        {
            networkGameManager.OnShotTakenServerRpc();
        }

        yield return new WaitForSeconds(0.1f);

        // Calculate the direction and magnitude of the force based on the mouse position
        Vector3 direction = transform.position - Camera.main.ScreenToWorldPoint(Input.mousePosition);
        direction.z = 0f;
        float forceMagnitude = direction.magnitude * strikerSpeed;
        forceMagnitude = Mathf.Clamp(forceMagnitude, 0f, maxForceMagnitude);

        // Apply the force to the striker
        Vector2 force = direction.normalized * forceMagnitude;
        rb.AddForce(force, ForceMode2D.Impulse);

        // Update network variables
        UpdateVelocityServerRpc(rb.velocity);

        CollisionSoundManager.shouldBeStatic = false;
        yield return new WaitForSeconds(0.1f);

        // Wait until the striker comes to a near stop
        yield return new WaitUntil(() => rb.velocity.magnitude < 0.1f);

        isMoving = false;

        // Check if any pieces were pocketed this turn
        bool continueTurn = NetworkBoardScript.piecesPocketedThisTurn > 0 &&
                           !NetworkBoardScript.strikerPocketedThisTurn;

        // Notify NetworkGameManager about shot completion
        if (networkGameManager != null)
        {
            networkGameManager.OnShotCompleteServerRpc(
                continueTurn,
                NetworkBoardScript.piecesPocketedThisTurn,
                NetworkBoardScript.strikerPocketedThisTurn
            );
        }

        // Reset turn tracking variables
        NetworkBoardScript.piecesPocketedThisTurn = 0;
        NetworkBoardScript.strikerPocketedThisTurn = false;

        gameObject.SetActive(false);
    }

    private void OnMouseDrag()
    {
        // Only allow input if it's this player's turn and they own this striker
        if (!IsOwner || !IsMyTurn()) return;

        // If charging is not enabled, return
        if (!isCharging || strikerForceField == null)
        {
            return;
        }

        // Update the direction and scale of the striker force field
        Vector3 direction = transform.position - Camera.main.ScreenToWorldPoint(Input.mousePosition);
        direction.z = 0f;
        strikerForceField.LookAt(transform.position + direction);

        float scaleValue = Vector3.Distance(transform.position, transform.position + direction / 4f);
        scaleValue = Mathf.Min(scaleValue, maxScale);

        strikerForceField.localScale = new Vector3(scaleValue, scaleValue, scaleValue);
    }

    public void SetSliderX()
    {
        // Only the owner can adjust slider position
        if (!IsOwner || !IsMyTurn()) return;

        // Set the position of the striker based on the slider value
        if (rb.velocity.magnitude < 0.1f && strikerSlider != null)
        {
            Vector3 newPosition = GetCorrectPosition();
            transform.position = newPosition;
            UpdatePositionServerRpc(newPosition);
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        // Play the collision sound if the striker collides with the board
        if (other.gameObject.CompareTag("Board"))
        {
            PlayCollisionSoundServerRpc();
        }
    }

    // Network RPCs
    [ServerRpc(RequireOwnership = false)]
    private void UpdatePositionServerRpc(Vector3 position)
    {
        networkPosition.Value = position;
        UpdatePositionClientRpc(position);
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdateVelocityServerRpc(Vector2 velocity)
    {
        networkVelocity.Value = velocity;
        UpdateVelocityClientRpc(velocity);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlayCollisionSoundServerRpc()
    {
        PlayCollisionSoundClientRpc();
    }

    [ClientRpc]
    private void UpdatePositionClientRpc(Vector3 position)
    {
        if (!IsOwner)
        {
            transform.position = position;
        }
    }

    [ClientRpc]
    private void UpdateVelocityClientRpc(Vector2 velocity)
    {
        if (!IsOwner)
        {
            rb.velocity = velocity;
        }
    }

    [ClientRpc]
    private void PlayCollisionSoundClientRpc()
    {
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.Play();
        }
    }

    // Network variable change handlers
    private void OnPositionChanged(Vector3 oldValue, Vector3 newValue)
    {
        if (!IsOwner)
        {
            transform.position = newValue;
        }
    }

    private void OnVelocityChanged(Vector2 oldValue, Vector2 newValue)
    {
        if (!IsOwner)
        {
            rb.velocity = newValue;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (networkPosition != null)
            networkPosition.OnValueChanged -= OnPositionChanged;
        if (networkVelocity != null)
            networkVelocity.OnValueChanged -= OnVelocityChanged;
    }

    public void SetPlayerIndex(int index)
    {
        playerIndex = index;
        SetStrikerColor();
    }
}