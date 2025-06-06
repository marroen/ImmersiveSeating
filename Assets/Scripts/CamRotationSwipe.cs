using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CamRotationSwipe : MonoBehaviour
{
    [Header("External Control")]
    [SerializeField] private bool allowExternalRotationControl = false; // NEW: Allow external scripts to control rotation
    public bool AllowExternalRotationControl
    {
        get { return allowExternalRotationControl; }
        set { allowExternalRotationControl = value; }
    }

    [Header("Touch Settings")]
    [SerializeField] private bool enableTouchRotation = true;
    [SerializeField] private float touchSensitivity = 2f;
    [SerializeField] private float smoothing = 0.1f;
    [SerializeField] private bool invertHorizontal = false;
    [SerializeField] private bool invertVertical = false;

    [Header("Rotation Limits")]
    [SerializeField] private bool enableRotationLimits = true;
    [SerializeField] private float maxVerticalAngle = 80f;
    [SerializeField] private float minVerticalAngle = -80f;
    [SerializeField] private bool enableHorizontalLimits = false;
    [SerializeField] private float maxHorizontalAngle = 360f;
    [SerializeField] private float minHorizontalAngle = -360f;

    [Header("Reset Settings")]
    [SerializeField] private bool enableReset = true;
    [SerializeField] private Button resetButton; // Button to reset camera rotation
    [SerializeField] private bool resetOnDoubleClick = true;
    [SerializeField] private float doubleClickTime = 0.3f;

    [Header("UI References")]
    [SerializeField] private Text statusText; // For displaying debug info
    [SerializeField] private Canvas mainCanvas; // Reference to your main canvas
    [SerializeField] private GameObject targetObject; // Reference to the small object to focus on

    [Header("Focus Transition")]
    [SerializeField] private float transitionDuration = 1.5f; // Duration of focus transition
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // Animation curve for smooth transition
    [SerializeField] private float focusDistance = 2f; // Distance to position the camera from target
    [SerializeField] private Vector3 targetOffset = Vector3.zero; // Offset to add to target position

    [Header("Debug Options")]
    [SerializeField] private bool showDebug = true;
    [SerializeField] private bool verboseDebug = true;

    // Touch tracking variables
    private Vector2 lastTouchPosition;
    private bool isTouching = false;
    private float currentVerticalAngle = 0f;
    private float currentHorizontalAngle = 0f;

    // Reset functionality
    private float lastTouchTime = 0f;
    private int touchCount = 0;

    // Rotation variables
    private Quaternion initialRotation;
    private Quaternion targetRotation;
    private bool isTransitioning = false;
    private Vector3 initialPosition;
    private Quaternion transitionStartRotation;

    // Debug variables
    private string debugText = "";
    private int updateCount = 0;
    private Vector2 currentDelta = Vector2.zero;

    void Start()
    {
        // Store initial rotation and position
        initialRotation = transform.rotation;
        initialPosition = transform.position;
        targetRotation = initialRotation;

        // Extract initial angles from the rotation
        Vector3 initialEuler = initialRotation.eulerAngles;
        currentHorizontalAngle = initialEuler.y;
        currentVerticalAngle = initialEuler.x;

        // Convert to -180 to 180 range for easier calculation
        if (currentVerticalAngle > 180)
            currentVerticalAngle -= 360;
        if (currentHorizontalAngle > 180)
            currentHorizontalAngle -= 360;

        Debug.Log("Starting CamRotationSwipe script");

        // Set up reset button if available
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(() =>
            {
                Debug.Log("Reset button clicked");
                ResetRotation();
            });
            Debug.Log("Reset button set up");
        }
        else
        {
            Debug.LogWarning("Reset button not assigned in inspector!");
        }

        debugText = "Touch to rotate camera";
        Debug.Log("Touch rotation system initialized");

        if (statusText != null)
        {
            statusText.text = "Touch and drag to rotate camera";
        }
    }

    void Update()
    {
        updateCount++;

        // If we're transitioning, skip normal rotation updates
        if (isTransitioning)
            return;

        if (!enableTouchRotation)
            return;

        HandleTouchInput();
        UpdateCameraRotation();
    }

    private void HandleTouchInput()
    {
        // Handle mouse input for editor testing
        if (Application.isEditor)
        {
            HandleMouseInput();
            return;
        }

        // Handle touch input for mobile
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    lastTouchPosition = touch.position;
                    isTouching = true;
                    HandleTouchBegin();
                    break;

                case TouchPhase.Moved:
                    if (isTouching)
                    {
                        Vector2 deltaPosition = touch.position - lastTouchPosition;
                        ProcessRotationInput(deltaPosition);
                        lastTouchPosition = touch.position;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    isTouching = false;
                    HandleTouchEnd();
                    break;
            }
        }
        else
        {
            isTouching = false;
        }
    }

    private void HandleMouseInput()
    {
        // Mouse input for editor testing
        if (Input.GetMouseButtonDown(0))
        {
            lastTouchPosition = Input.mousePosition;
            isTouching = true;
            HandleTouchBegin();
        }
        else if (Input.GetMouseButton(0) && isTouching)
        {
            Vector2 currentMousePosition = Input.mousePosition;
            Vector2 deltaPosition = currentMousePosition - lastTouchPosition;
            ProcessRotationInput(deltaPosition);
            lastTouchPosition = currentMousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isTouching = false;
            HandleTouchEnd();
        }
    }

    private void HandleTouchBegin()
    {
        // Handle double-click/tap for reset
        if (resetOnDoubleClick)
        {
            float currentTime = Time.time;
            if (currentTime - lastTouchTime < doubleClickTime)
            {
                touchCount++;
                if (touchCount >= 2)
                {
                    Debug.Log("Double-tap detected: resetting rotation");
                    ResetRotation();
                    touchCount = 0;
                }
            }
            else
            {
                touchCount = 1;
            }
            lastTouchTime = currentTime;
        }
    }

    private void HandleTouchEnd()
    {
        currentDelta = Vector2.zero;
    }

    private void ProcessRotationInput(Vector2 deltaPosition)
    {
        currentDelta = deltaPosition;

        // Convert screen delta to rotation
        float horizontalRotation = deltaPosition.x * touchSensitivity * 0.1f;
        float verticalRotation = deltaPosition.y * touchSensitivity * 0.1f;

        // Apply inversion if enabled
        if (invertHorizontal)
            horizontalRotation = -horizontalRotation;
        if (invertVertical)
            verticalRotation = -verticalRotation;

        // Update rotation angles
        currentHorizontalAngle += horizontalRotation;
        currentVerticalAngle -= verticalRotation; // Subtract because screen Y is inverted

        // Apply limits
        ApplyRotationLimits();
    }

    private void ApplyRotationLimits()
    {
        // Apply vertical limits
        if (enableRotationLimits)
        {
            currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, minVerticalAngle, maxVerticalAngle);
        }

        // Apply horizontal limits if enabled
        if (enableHorizontalLimits)
        {
            currentHorizontalAngle = Mathf.Clamp(currentHorizontalAngle, minHorizontalAngle, maxHorizontalAngle);
        }
        else
        {
            // Keep horizontal angle in reasonable range
            if (currentHorizontalAngle > 360f)
                currentHorizontalAngle -= 360f;
            else if (currentHorizontalAngle < -360f)
                currentHorizontalAngle += 360f;
        }
    }

    private void UpdateCameraRotation()
    {
        // Create target rotation from current angles
        Quaternion rotation = Quaternion.Euler(currentVerticalAngle, currentHorizontalAngle, 0);
        targetRotation = rotation;

        // Apply smoothing
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothing);

        // Debug output
        if (verboseDebug && updateCount % 60 == 0) // Log every 60 frames
        {
            debugText = $"Rotation X:{currentVerticalAngle:F1}� Y:{currentHorizontalAngle:F1}�";
            if (isTouching)
            {
                debugText += $" | Delta:{currentDelta.x:F1},{currentDelta.y:F1}";
            }
        }
    }

    public void ResetRotation()
    {
        Debug.Log("Resetting camera rotation");

        // Reset to initial rotation
        currentVerticalAngle = 0f;
        currentHorizontalAngle = 0f;
        targetRotation = initialRotation;

        if (statusText != null)
        {
            statusText.text = "Camera rotation reset";
            StartCoroutine(FadeOutText(statusText, 1.5f));
        }
    }

    public void SetTouchSensitivity(float sensitivity)
    {
        touchSensitivity = sensitivity;
        Debug.Log($"Touch sensitivity set to: {touchSensitivity}");
    }

    public void ToggleTouchRotation()
    {
        enableTouchRotation = !enableTouchRotation;
        Debug.Log($"Touch rotation enabled: {enableTouchRotation}");

        if (statusText != null)
        {
            statusText.text = enableTouchRotation ? "Touch rotation enabled" : "Touch rotation disabled";
            StartCoroutine(FadeOutText(statusText, 1.5f));
        }
    }

    // Focus on target object (keeping the same functionality from original script)
    public void FocusOnTarget()
    {
        if (targetObject != null && !isTransitioning)
        {
            StartCoroutine(TransitionToTarget());
        }
    }

    private IEnumerator TransitionToTarget()
    {
        isTransitioning = true;
        transitionStartRotation = transform.rotation;
        Vector3 startPosition = transform.position;

        // Calculate target position and rotation
        Vector3 targetPosition = targetObject.transform.position + targetOffset;
        Vector3 direction = (targetPosition - transform.position).normalized;
        Vector3 finalPosition = targetPosition - direction * focusDistance;
        Quaternion finalRotation = Quaternion.LookRotation(direction);

        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / transitionDuration;
            float curveValue = transitionCurve.Evaluate(t);

            // Interpolate position and rotation
            transform.position = Vector3.Lerp(startPosition, finalPosition, curveValue);
            transform.rotation = Quaternion.Slerp(transitionStartRotation, finalRotation, curveValue);

            yield return null;
        }

        // Ensure final values are set
        transform.position = finalPosition;
        transform.rotation = finalRotation;

        // Update current angles to match final rotation
        Vector3 finalEuler = finalRotation.eulerAngles;
        currentHorizontalAngle = finalEuler.y;
        currentVerticalAngle = finalEuler.x;

        // Convert to -180 to 180 range
        if (currentVerticalAngle > 180)
            currentVerticalAngle -= 360;
        if (currentHorizontalAngle > 180)
            currentHorizontalAngle -= 360;

        isTransitioning = false;
    }

    private IEnumerator FadeOutText(Text text, float delay)
    {
        // Ensure text is visible before starting fade
        Color originalColor = text.color;
        text.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);

        yield return new WaitForSeconds(delay);

        float fadeDuration = 1.0f; // Duration of the fade out
        float elapsedTime = 0;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(1.0f - (elapsedTime / fadeDuration));
            text.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        text.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0); // Ensure fully transparent
    }

    void OnGUI()
    {
        if (showDebug)
        {
            // Basic debug text display
            GUI.Label(new Rect(10, 10, 400, 30), debugText);

            if (verboseDebug)
            {
                // More detailed debug info
                GUI.Label(new Rect(10, 40, 300, 30), $"Touch Rotation Enabled: {enableTouchRotation}");
                GUI.Label(new Rect(10, 70, 300, 30), $"Is Touching: {isTouching}");
                GUI.Label(new Rect(10, 100, 300, 30), $"Touch Sensitivity: {touchSensitivity}");
                GUI.Label(new Rect(10, 130, 300, 30), $"Vertical Limits: {minVerticalAngle}� to {maxVerticalAngle}�");

                if (enableHorizontalLimits)
                {
                    GUI.Label(new Rect(10, 160, 300, 30), $"Horizontal Limits: {minHorizontalAngle}� to {maxHorizontalAngle}�");
                }
                else
                {
                    GUI.Label(new Rect(10, 160, 300, 30), "Horizontal Limits: Disabled");
                }
            }
        }
    }
}