
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