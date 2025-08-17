
using UnityEngine;
using UnityEngine.UI;

public class StrikerController : MonoBehaviour
{
    [HideInInspector]
    public GameManager gameManager;

    [Header("UI & Visuals")]
    public Slider strikerSlider;                 // Assign in Inspector
    public Transform strikerForceField;          // Assign in Inspector
    public float maxForceMagnitude = 30f;
    public float maxScale = 1f;

    private Camera gameCamera;
    private bool isCharging = false;
    private Vector3 dragStartPos, dragEndPos;
    private const float STRIKER_MIN_X = -3.4f;
    private const float STRIKER_MAX_X = 3.4f;
    private const float STRIKER_Y = -4.57f;

    void Start()
    {
        gameCamera = Camera.main;
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        // Set striker start position
        float x = strikerSlider ? strikerSlider.value : 0f;
        transform.position = new Vector3(Mathf.Clamp(x, STRIKER_MIN_X, STRIKER_MAX_X), STRIKER_Y, 0f);

        // Hide force field initially
        if (strikerForceField)
            strikerForceField.localScale = Vector3.zero;
    }

    void Update()
    {
        if (!gameManager.IsMyTurn()) return;

        // Allow slider to move striker left/right in bounds if not charging
        if (strikerSlider && !isCharging)
        {
            float clampedX = Mathf.Clamp(strikerSlider.value, STRIKER_MIN_X, STRIKER_MAX_X);
            transform.position = new Vector3(clampedX, STRIKER_Y, 0f);
        }

        // Mouse down: start charging (only if clicked on striker)
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorld = gameCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0;
            if (Vector2.Distance(mouseWorld, transform.position) < 0.5f)
            {
                isCharging = true;
                dragStartPos = transform.position;
                if (strikerForceField)
                    strikerForceField.gameObject.SetActive(true);
            }
        }

        // Mouse drag: update arrow/force field
        if (Input.GetMouseButton(0) && isCharging)
        {
            Vector3 mouseWorld = gameCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0;
            Vector3 direction = dragStartPos - mouseWorld;
            dragEndPos = dragStartPos + direction;

            if (strikerForceField)
            {
                strikerForceField.LookAt(dragStartPos + direction);
                float scaleValue = Mathf.Clamp(direction.magnitude / 4f, 0f, maxScale);
                strikerForceField.localScale = new Vector3(scaleValue, scaleValue, scaleValue);
            }
        }

        // Mouse up: shoot striker
        if (Input.GetMouseButtonUp(0) && isCharging)
        {
            isCharging = false;
            if (strikerForceField)
                strikerForceField.gameObject.SetActive(false);

            Vector3 shootDirection = dragStartPos - dragEndPos;
            float power = Mathf.Clamp(shootDirection.magnitude, 0f, maxForceMagnitude);

            // Send to backend for authoritative shoot
            gameManager.ShootStriker(dragStartPos, dragEndPos, power);
        }
    }

    // Call this from your slider's OnValueChanged event for instant response
    public void SetSliderX(float value)
    {
        if (!isCharging && gameManager.IsMyTurn())
        {
            float clampedX = Mathf.Clamp(value, STRIKER_MIN_X, STRIKER_MAX_X);
            transform.position = new Vector3(clampedX, STRIKER_Y, 0f);
            // Send move to backend!
            gameManager.MoveStriker(clampedX);
        }
    }
}