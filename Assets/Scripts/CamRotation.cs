using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Runtime.InteropServices;

public class CamRotationGyro : MonoBehaviour
{
    [Header("External Control")]
    [SerializeField] private bool allowExternalRotationControl = false; // NEW: Allow external scripts to control rotation
    public bool AllowExternalRotationControl
    {
        get { return allowExternalRotationControl; }
        set { allowExternalRotationControl = value; }
    }

    [Header("Rotation Settings")]
    [SerializeField] private bool enableDeviceRotation = true;
    [SerializeField] private float smoothing = 0.1f;

    [Header("Rotation Limits")]
    [SerializeField] private bool enableRotationLimits = false;
    [SerializeField] private float maxVerticalAngle = 80f;

    [Header("Calibration")]
    [SerializeField] private bool calibrateOnStart = true;
    [SerializeField] private bool applyCalibration = true; // NEW: Toggle calibration application
    [SerializeField] private Button calibrateButton; // NEW: Add a calibration button reference

    [Header("UI References")]
    [SerializeField] private Button permissionButton; // Assign this in the inspector
    [SerializeField] private Text statusText; // For displaying debug info
    [SerializeField] private Canvas mainCanvas; // Reference to your main canvas
    [SerializeField] private GameObject targetObject; // Reference to the small object to focus on

    [Header("Focus Transition")]
    [SerializeField] private float transitionDuration = 1.5f; // Duration of focus transition
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // Animation curve for smooth transition
    [SerializeField] private float focusDistance = 2f; // Distance to position the camera from target
    [SerializeField] private Vector3 targetOffset = Vector3.zero; // Offset to add to target position

    [Header("Debug Options")]
    [SerializeField] private bool showDebug = false;
    [SerializeField] private bool verboseDebug = false; // NEW: Toggle for more detailed debugging
    [SerializeField] private bool useRawOrientation = false; // NEW: Skip calibration for testing

    private bool deviceMotionAvailable = false;
    private bool isUsingGyro = false;
    private bool isUsingAccelerometer = false;
    private Quaternion initialRotation;
    private Quaternion calibration = Quaternion.identity;
    private Quaternion baseRotation = Quaternion.identity;
    private Quaternion targetRotation;
    private bool permissionRequested = false;
    private bool isTransitioning = false;
    private Vector3 initialPosition;
    private Quaternion transitionStartRotation;
    private float touchDurationCounter = 0f; // NEW: Track touch duration for alternative gesture

    // Debug variables
    private string debugText = "";
    private float lastOrientationAlpha = 0;
    private float lastOrientationBeta = 0;
    private float lastOrientationGamma = 0;
    private int updateCount = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Store initial rotation and position
        initialRotation = transform.rotation;
        initialPosition = transform.position;

        Debug.Log("Starting CamRotation script");

        RequestPermissions();
        // Set up permission button if available
        if (permissionButton != null)
        {
            permissionButton.onClick.AddListener(RequestPermissions);
            Debug.Log("Permission button set up");
        }
        else
        {
            Debug.LogWarning("Permission button not assigned in inspector!");
        }

        Calibrate();
        // Set up calibration button if available
        if (calibrateButton != null)
        {
            calibrateButton.onClick.AddListener(() => {
                Debug.Log("Calibration button clicked");
                Calibrate();
            });
            Debug.Log("Calibration button set up");
        }
        else
        {
            Debug.LogWarning("Calibration button not assigned in inspector!");
        }

        // Try to initialize sensors
        InitializeSensors();

        // Perform initial calibration if needed
        if (calibrateOnStart)
        {
            Calibrate();
        }
    }

    // Update is called once per frame
    void Update()
    {
        updateCount++;
        // If we're transitioning, skip normal rotation updates
        if (isTransitioning || allowExternalRotationControl)
            return;

        if (!deviceMotionAvailable || !enableDeviceRotation)
            return;

        // Keep the original three-finger touch for native apps
        if (Input.touchCount == 3)
        {
            Debug.Log("Three-finger touch detected: recalibrating");
            Calibrate();
            if (statusText != null)
            {
                statusText.text = "Calibrated!";
                StartCoroutine(FadeOutText(statusText, 1.5f));
            }
        }

        UpdateCameraRotation();
    }

    public void Calibrate()
    {
        Debug.Log("Calibrate function called!");

        if (statusText != null)
        {
            statusText.text = "Calibrating...";
        }

        // Native app calibration
        if (isUsingGyro)
        {
            calibration = Quaternion.Inverse(ConvertGyroToUnity(Input.gyro.attitude));
        }
        else if (isUsingAccelerometer)
        {
            // Recalculate accelerometer orientation for calibration
            Vector3 acceleration = Input.acceleration.normalized;
            float pitch = Mathf.Atan2(-acceleration.y, -acceleration.z) * Mathf.Rad2Deg;
            float yaw = Mathf.Atan2(-acceleration.x, -acceleration.z) * Mathf.Rad2Deg;
            Quaternion current = Quaternion.Euler(pitch, yaw, 0);
            calibration = Quaternion.Inverse(current);
        }

        if (statusText != null)
        {
            statusText.text = "Calibrated!";
            //StartCoroutine(FadeOutText(statusText, 1.5f));
        }

        Debug.Log("Device orientation calibrated");
    }

    public void SetNewInitialRotation(Quaternion newInitialRotation)
    {
        initialRotation = newInitialRotation;
        // Force immediate calibration with new initial rotation
        Calibrate();
        Debug.Log($"Set new initial rotation to: {newInitialRotation.eulerAngles}");
    }

    private void UpdateCameraRotation()
    {
        Quaternion rotation = initialRotation;

        
        if (isUsingGyro)
        {
            Quaternion gyroRotation = ConvertGyroToUnity(Input.gyro.attitude);
            rotation = initialRotation * baseRotation * calibration * gyroRotation;
        }
        else if (isUsingAccelerometer)
        {
            rotation = GetRotationFromAccelerometer();
        }

        // Apply rotation limits if enabled
        if (enableRotationLimits)
        {
            Vector3 euler = rotation.eulerAngles;

            // Convert angles to -180 to 180 range for easier limiting
            float xAngle = euler.x;
            if (xAngle > 180)
                xAngle -= 360;

            // Clamp vertical rotation
            xAngle = Mathf.Clamp(xAngle, -maxVerticalAngle, maxVerticalAngle);

            // Convert back to 0-360 range
            if (xAngle < 0)
                xAngle += 360;

            euler.x = xAngle;
            rotation = Quaternion.Euler(euler);
        }

        // Apply smoothing
        targetRotation = rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothing);

        // Debug output to show rotation is updating
        if (verboseDebug && updateCount % 180 == 0) // Log more frequently for rotation
        {
            Vector3 currentEuler = transform.rotation.eulerAngles;
            // Displaying targetRotation euler angles might be more informative here before Slerp
            Vector3 targetEuler = targetRotation.eulerAngles;
            debugText = $"TargetRot X:{targetEuler.x:F0}° Y:{targetEuler.y:F0}° Z:{targetEuler.z:F0}°";
        }
    }

    // Convert the gyroscope quaternion to Unity's coordinate system
    private Quaternion ConvertGyroToUnity(Quaternion q)
    {
        // Standard conversion for gyroscope data to Unity's coordinate system
        return new Quaternion(q.x, q.y, -q.z, -q.w);
    }

    // Get rotation from accelerometer (fallback)
    private Quaternion GetRotationFromAccelerometer()
    {
        Vector3 acceleration = Input.acceleration;
        // Basic accelerometer-based orientation - can be jittery and limited
        float pitch = Mathf.Atan2(-acceleration.y, -acceleration.z) * Mathf.Rad2Deg;
        float yaw = Mathf.Atan2(-acceleration.x, -acceleration.z) * Mathf.Rad2Deg; // This yaw is often unreliable

        Quaternion orientationRotation = Quaternion.Euler(pitch, yaw, 0);
        return initialRotation * baseRotation * calibration * orientationRotation;
    }

    private void InitializeSensors()
    {
        // For native apps, try gyroscope first
        if (SystemInfo.supportsGyroscope)
        {
            try
            {
                Input.gyro.enabled = true;
                isUsingGyro = true;
                deviceMotionAvailable = true;
                debugText = "Using Gyroscope";
                Debug.Log("Using gyroscope for device orientation");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Failed to initialize gyroscope: " + e.Message);
                isUsingGyro = false;
            }
        }

        // Fall back to accelerometer if gyro not available
        if (!isUsingGyro && SystemInfo.supportsAccelerometer)
        {
            isUsingAccelerometer = true;
            deviceMotionAvailable = true;
            debugText = "Using Accelerometer";
            Debug.Log("Using accelerometer for device orientation");
        }
    }

    public void RequestPermissions()
    {
        if (!permissionRequested)
        {
            Debug.Log("Requesting device orientation permission");

            permissionRequested = true;

            // Hide the permission button after requesting
            if (permissionButton != null)
            {
                permissionButton.gameObject.SetActive(false);
            }

            // Start checking for permission to be granted
            StartCoroutine(CheckForPermissionGrant());
        }
    }

    private IEnumerator CheckForPermissionGrant()
    {
        float timeoutCounter = 0;
        while (timeoutCounter < 15.0f) // Check for 15 seconds
        {
            // On non-WebGL platforms, permission is typically implicitly granted or not needed in this way
            deviceMotionAvailable = true; // Assume available for native
            yield break;

            timeoutCounter += 0.2f;
            yield return new WaitForSeconds(0.2f);
        }

        // If we get here, permission wasn't granted within timeout
        debugText = "Permission not granted or timed out.";
        Debug.LogWarning("Device orientation permission not granted or timed out");

        if (statusText != null)
        {
            statusText.text = "Motion access denied. Please try again.";
            // Make the permission button visible again
            if (permissionButton != null)
            {
                permissionButton.gameObject.SetActive(true);
                permissionRequested = false; // Allow requesting again
            }
        }
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
    
    /*
    void OnGUI()
    {
        if (showDebug)
        {
            // Basic debug text display
            GUI.Label(new Rect(10, 10, 400, 30), debugText); // Increased width for longer messages

            if (verboseDebug)
            {
                // More detailed debug info
                GUI.Label(new Rect(10, 40, 300, 30), $"Device Motion Available: {deviceMotionAvailable}");
                GUI.Label(new Rect(10, 70, 300, 30), $"Apply Calibration: {applyCalibration}");
                GUI.Label(new Rect(10, 100, 300, 30), $"Use Raw Orientation: {useRawOrientation}");

#if UNITY_WEBGL && !UNITY_EDITOR
                // WebGL specific debug info
                GUI.Label(new Rect(10, 130, 300, 30), $"Orientation Events: {GetOrientationEventCount()}");
                GUI.Label(new Rect(10, 160, 300, 30), $"Permission Granted: {(IsOrientationPermissionGranted() == 1)}");
#endif
            }
        }
    } */
}
