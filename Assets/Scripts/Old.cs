using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine.UI; // For UI elements

public class Old : MonoBehaviour
{
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
    [SerializeField] private bool showDebug = true;
    [SerializeField] private bool verboseDebug = true; // NEW: Toggle for more detailed debugging
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

#if UNITY_WEBGL && !UNITY_EDITOR
    // WebGL plugin function imports
    [DllImport("__Internal")]
    private static extern int InitDeviceOrientation();
    
    [DllImport("__Internal")]
    private static extern int RequestDeviceOrientationPermission();
    
    [DllImport("__Internal")]
    private static extern int IsOrientationAvailable();
    
    [DllImport("__Internal")]
    private static extern float GetOrientationAlpha();
    
    [DllImport("__Internal")]
    private static extern float GetOrientationBeta();
    
    [DllImport("__Internal")]
    private static extern float GetOrientationGamma();
    
    [DllImport("__Internal")]
    private static extern int IsOrientationPermissionGranted();
    
    [DllImport("__Internal")]
    private static extern int GetOrientationEventCount();
    
    [DllImport("__Internal")]
    private static extern void EvalJS(string jsCode);
#endif

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

#if UNITY_WEBGL && !UNITY_EDITOR
        // Initialize the WebGL orientation system
        Debug.Log("Initializing WebGL device orientation");
        int result = InitDeviceOrientation();
        Debug.Log($"WebGL device orientation initialization result: {result}");
        
        if (statusText != null)
        {
            statusText.text = "Tap 'Enable Motion' button to start";
        }
#else
        // Perform initial calibration if needed
        if (calibrateOnStart)
        {
            Calibrate();
        }
#endif
    }

    private void InitializeSensors()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // For WebGL, we'll wait for explicit permission before initializing
        debugText = "Waiting for permission...";
        Debug.Log("WebGL build: Waiting for device orientation permission");
        // We'll initialize after permission is granted via the button
#else
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
#endif
    }

    public void RequestPermissions()
    {
        if (!permissionRequested)
        {
            Debug.Log("Requesting device orientation permission");

#if UNITY_WEBGL && !UNITY_EDITOR
            RequestDeviceOrientationPermission();
#endif

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
#if UNITY_WEBGL && !UNITY_EDITOR
            if (IsOrientationPermissionGranted() == 1)
            {
                deviceMotionAvailable = true;
                debugText = "Permission granted! Using device orientation.";
                Debug.Log("Device orientation permission granted");
                
                if (statusText != null)
                {
                    statusText.text = "Motion enabled!";
                    // Make the status text fade out after 3 seconds
                    StartCoroutine(FadeOutText(statusText, 3.0f));
                }
                
                // Now that we have permission, we can calibrate
                if (calibrateOnStart)
                {
                    Debug.Log("Calibrating after permission granted");
                    Calibrate();
                }
                
                yield break; // Exit coroutine
            }
#else
            // On non-WebGL platforms, permission is typically implicitly granted or not needed in this way
            deviceMotionAvailable = true; // Assume available for native
            yield break;
#endif

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

    void Update()
    {
        updateCount++;

        // Every 100 frames, check if we're getting orientation events
#if UNITY_WEBGL && !UNITY_EDITOR
        if (deviceMotionAvailable && verboseDebug && updateCount % 100 == 0)
        {
            int eventCount = GetOrientationEventCount();
            Debug.Log($"Orientation event count: {eventCount}");
            
            // Check if values are changing
            float alpha = GetOrientationAlpha();
            float beta = GetOrientationBeta();
            float gamma = GetOrientationGamma();
            
            if (alpha != lastOrientationAlpha || beta != lastOrientationBeta || gamma != lastOrientationGamma)
            {
                Debug.Log($"Orientation changed: α:{alpha:F1}° β:{beta:F1}° γ:{gamma:F1}°");
                lastOrientationAlpha = alpha;
                lastOrientationBeta = beta;
                lastOrientationGamma = gamma;
            }
        }
#endif

        // If we're transitioning, skip normal rotation updates
        if (isTransitioning)
            return;

        if (!deviceMotionAvailable || !enableDeviceRotation)
            return;

        // Double tap detection for WebGL calibration
#if UNITY_WEBGL && !UNITY_EDITOR
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            
            // Check for double tap (single finger, faster response)
            if (touch.tapCount >= 2 && touch.phase == TouchPhase.Began)
            {
                Debug.Log("Double-tap detected: recalibrating");
                Calibrate();
                if (statusText != null)
                {
                    statusText.text = "Calibrated!";
                    StartCoroutine(FadeOutText(statusText, 1.5f));
                }
            }
            
            // Alternative: long press with two fingers
            if (Input.touchCount == 2 && 
                Input.GetTouch(0).phase == TouchPhase.Stationary && 
                Input.GetTouch(1).phase == TouchPhase.Stationary)
            {
                touchDurationCounter += Time.deltaTime;
                
                // Visual feedback during hold
                if (touchDurationCounter > 0.5f && statusText != null)
                {
                    statusText.text = "Hold for calibration... " + 
                        Mathf.RoundToInt((1.0f - touchDurationCounter) * 10) / 10f + "s";
                }
                
                // After holding for 1 second, calibrate
                if (touchDurationCounter >= 1.0f)
                {
                    Debug.Log("Two-finger hold gesture detected: recalibrating");
                    Calibrate();
                    touchDurationCounter = 0f; // Reset the counter
                }
            }
            else
            {
                touchDurationCounter = 0f; // Reset if touch conditions not met
            }
        }
#else
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
#endif

        // Update camera rotation based on available sensors
        UpdateCameraRotation();

        // Add keyboard controls for testing on desktop WebGL builds
#if UNITY_WEBGL && !UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.C))
        {
            Debug.Log("C key pressed: recalibrating");
            Calibrate();
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("R key pressed: reset orientation");
            ResetOrientation();
        }
#endif
    }

    private void UpdateCameraRotation()
    {
        Quaternion rotation = initialRotation;

#if UNITY_WEBGL && !UNITY_EDITOR
        // Check if orientation is actually available
        if (IsOrientationAvailable() == 1)
        {
            // Get orientation from WebGL
            rotation = GetRotationFromWebGL();
            
            // Log rotation details if verbose debug is enabled (infrequently to not spam)
            if (verboseDebug && updateCount % 300 == 0)
            {
                Vector3 euler = rotation.eulerAngles;
                Debug.Log($"Camera rotation - X:{euler.x:F1}° Y:{euler.y:F1}° Z:{euler.z:F1}°");
            }
        }
        else
        {
            // If orientation data is not available, just return
            if (verboseDebug && updateCount % 300 == 0)
            {
                Debug.LogWarning("Orientation data not available");
            }
            return;
        }
#else
        if (isUsingGyro)
        {
            Quaternion gyroRotation = ConvertGyroToUnity(Input.gyro.attitude);
            rotation = initialRotation * baseRotation * calibration * gyroRotation;
        }
        else if (isUsingAccelerometer)
        {
            rotation = GetRotationFromAccelerometer();
        }
#endif

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

#if UNITY_WEBGL && !UNITY_EDITOR
    // Get rotation from WebGL device orientation
    private Quaternion GetRotationFromWebGL()
    {
        float alpha = GetOrientationAlpha(); // Z-axis rotation (yaw)
        float beta = GetOrientationBeta();   // X-axis rotation (pitch)
        float gamma = GetOrientationGamma(); // Y-axis rotation (roll)

        // Determine if we're in portrait or landscape
        bool isPortrait = Screen.height > Screen.width;

        // Update debug info with raw values
        debugText = $"Raw: α:{alpha:F0}° β:{beta:F0}° γ:{gamma:F0}°";
    
        // Check if we're getting non-zero data
        if (Mathf.Approximately(alpha, 0f) && Mathf.Approximately(beta, 0f) && Mathf.Approximately(gamma, 0f))
        {
            if (verboseDebug && updateCount % 300 == 0)
            {
                Debug.LogWarning("All orientation values are zero! Check device orientation permissions or sensor activity.");
            }
        }
    
        Quaternion orientationRotation;
        
        // Use consistent orientation mapping between portrait and landscape
        if (isPortrait) {
            // Portrait mapping
            orientationRotation = Quaternion.Euler(-beta, -alpha, -gamma);
        } else {
            // Landscape mapping - you might need to adjust this based on testing
            orientationRotation = Quaternion.Euler(gamma, -alpha, beta);
        }
    
        // Option to use raw orientation data for testing
        if (useRawOrientation)
        {
            return initialRotation * orientationRotation;
        }
    
        // Apply calibration if enabled
        if (applyCalibration)
        {
            Quaternion calibratedRotation = initialRotation * baseRotation * calibration * orientationRotation;
            Vector3 euler = calibratedRotation.eulerAngles;
            debugText += $"\nCal: X:{euler.x:F0}° Y:{euler.y:F0}° Z:{euler.z:F0}°";
            return calibratedRotation;
        }
        else
        {
            debugText += $"\nCalibration OFF";
            return initialRotation * baseRotation * orientationRotation;
        }
    }
#else
    // Dummy implementation for non-WebGL platforms to allow script compilation
    private Quaternion GetRotationFromWebGL()
    {
        return initialRotation;
    }
#endif

    // Calibrate based on current device orientation
    public void Calibrate()
    {
        Debug.Log("Calibrate function called!");

        if (statusText != null)
        {
            statusText.text = "Calibrating...";
        }

#if UNITY_WEBGL && !UNITY_EDITOR
    // Check if orientation data is available before attempting calibration
    if (IsOrientationAvailable() != 1)
    {
        Debug.LogError("Cannot calibrate - orientation data not available!");
        if (statusText != null)
        {
            statusText.text = "Calibration failed - no orientation data!";
            StartCoroutine(FadeOutText(statusText, 2.0f));
        }
        return;
    }
    
    // WebGL calibration
    float alpha = GetOrientationAlpha();
    float beta = GetOrientationBeta();
    float gamma = GetOrientationGamma();
    
    Debug.Log($"Calibrating with α:{alpha:F1}° β:{beta:F1}° γ:{gamma:F1}°");
    
    // Make sure we have valid orientation data
    if (Mathf.Approximately(alpha, 0f) && Mathf.Approximately(beta, 0f) && Mathf.Approximately(gamma, 0f) && GetOrientationEventCount() > 0)
    {
        Debug.LogWarning("Attempting to calibrate with zero orientation values.");
    }
    
    try
    {
        // Determine if we're in portrait or landscape
        bool isPortrait = Screen.height > Screen.width;
        Quaternion currentOrientationRaw;
        
        // Use the SAME orientation mapping as in GetRotationFromWebGL for consistency
        if (isPortrait) {
            currentOrientationRaw = Quaternion.Euler(-beta, -alpha, -gamma);
        } else {
            currentOrientationRaw = Quaternion.Euler(gamma, alpha, beta);
        }
        
        // Calculate inverse quaternion to use as calibration offset
        calibration = Quaternion.Inverse(currentOrientationRaw);
        
        // Apply additional correction to account for different initial device position in WebGL
        // These values may need adjustment based on testing
        //Vector3 correctionAngles = new Vector3(0, 0, 0);
        
        // If you find the camera is consistently off by a specific angle, adjust these values
        // Example: If it's always 90 degrees to the right, you might use:
        // correctionAngles = new Vector3(0, -90, 0);
        
        //Quaternion initialCorrection = Quaternion.Euler(correctionAngles);
        //calibration = calibration * initialCorrection;
        
        // Log calibration values for debugging
        Debug.Log($"Calibration quaternion: {calibration.eulerAngles.x:F1}, {calibration.eulerAngles.y:F1}, {calibration.eulerAngles.z:F1}");
        
        if (statusText != null)
        {
            statusText.text = "Calibrated!";
            StartCoroutine(FadeOutText(statusText, 1.5f));
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Calibration error: {e.Message}");
        if (statusText != null)
        {
            statusText.text = "Calibration error!";
            StartCoroutine(FadeOutText(statusText, 1.5f));
        }
    }
#else
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
            StartCoroutine(FadeOutText(statusText, 1.5f));
        }
#endif

        Debug.Log("Device orientation calibrated");
    }

    // Reset orientation to initial values
    public void ResetOrientation()
    {
        Debug.Log("Resetting orientation...");
        // baseRotation = Quaternion.identity; // Base rotation is usually for world alignment, not typically reset this way.
        calibration = Quaternion.identity; // Reset calibration offset completely

        // For a full reset, you might want to re-calibrate to the current "forward"
        // Or simply set the camera to its absolute initial rotation if device motion is then disabled.
        // If device motion remains active, recalibrating makes sense.
        if (calibrateOnStart && deviceMotionAvailable) // Or simply always calibrate if motion is active
        {
            Calibrate(); // Re-calibrate to the current orientation as the new zero
        }
        else
        {
            transform.rotation = initialRotation; // Fallback to absolute initial if no calibration desired/possible
        }


        Debug.Log("Orientation reset complete");

        if (statusText != null)
        {
            statusText.text = "Orientation Reset";
            StartCoroutine(FadeOutText(statusText, 1.5f));
        }
    }

    // Call this when button is pressed to focus on target object
    public void FocusOnTarget()
    {
        if (targetObject == null)
        {
            Debug.LogError("Target object is not assigned!");
            return;
        }

        StartCoroutine(TransitionToTarget());

        // Optionally disable the canvas
        if (mainCanvas != null)
        {
            mainCanvas.enabled = false;
        }
    }

    // Transition camera to focus on target
    private IEnumerator TransitionToTarget()
    {
        /*
        isTransitioning = true;
        Debug.Log("Starting transition to target");

        // Store starting position and rotation for smooth lerp
        Vector3 startPosition = transform.position;
        transitionStartRotation = transform.rotation; // Use current rotation as start

        // Calculate target position (offset from target object, looking at it)
        Vector3 directionToTarget = (targetObject.transform.position - startPosition).normalized;
        if (directionToTarget == Vector3.zero) directionToTarget = -transform.forward; // Fallback if at same position

        Vector3 targetPosition = targetObject.transform.position - (directionToTarget * focusDistance) + targetOffset;

        // Calculate target rotation to look at the center of the target object
        Quaternion targetLookRotation = Quaternion.LookRotation(
            (targetObject.transform.position + targetOffset) - targetPosition
        );

        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            float t = transitionCurve.Evaluate(elapsedTime / transitionDuration);

            // Move and rotate camera
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            transform.rotation = Quaternion.Slerp(transitionStartRotation, targetLookRotation, t);

            elapsedTime += Time.deltaTime;
            yield return null; // Wait for the next frame
        }

        // Ensure we end at the exact target position and rotation
        transform.position = targetPosition;
        transform.rotation = targetLookRotation; */

        // Store this target rotation as a reference for future calibrations
        Quaternion targetRotationReference = Quaternion.identity;

        // Wait a moment before calibrating to ensure device readings are stable
        yield return new WaitForSeconds(0.5f);

        // Reset orientation and force recalibrate
        baseRotation = Quaternion.identity;

        // Force a more aggressive calibration for WebGL
#if UNITY_WEBGL && !UNITY_EDITOR
        // Get current orientation values
        float alpha = GetOrientationAlpha();
        float beta = GetOrientationBeta();
        float gamma = GetOrientationGamma();
    
        bool isPortrait = Screen.height > Screen.width;
        Quaternion deviceOrientation;
    
        if (isPortrait) {
            deviceOrientation = Quaternion.Euler(-beta, -alpha, -gamma);
        } else {
            deviceOrientation = Quaternion.Euler(gamma, -alpha, beta);
        }
    
        // Calculate the calibration quaternion that will make the current device orientation
        // result in the target look rotation
        //calibration = Quaternion.Inverse(deviceOrientation);
        //calibration = deviceOrientation;
    
        // Log for debugging
        Debug.Log($"Focus calibration: α:{alpha:F1}° β:{beta:F1}° γ:{gamma:F1}°");
        Debug.Log($"Target rotation: {targetRotationReference.eulerAngles.x:F1}, {targetRotationReference.eulerAngles.y:F1}, {targetRotationReference.eulerAngles.z:F1}");
        Debug.Log($"Calibration quaternion: {calibration.eulerAngles.x:F1}, {calibration.eulerAngles.y:F1}, {calibration.eulerAngles.z:F1}");
        Calibrate();
#else
        // Standard calibration for native builds
        Calibrate();
#endif

        isTransitioning = false;
        Debug.Log("Camera transition to target complete");
    }

    // Return to initial position
    public void ReturnToInitialPosition()
    {
        StartCoroutine(TransitionToInitial());

        // Re-enable the canvas
        if (mainCanvas != null)
        {
            mainCanvas.enabled = true;
        }
    }

    // Transition back to initial position
    private IEnumerator TransitionToInitial()
    {
        isTransitioning = true;
        Debug.Log("Starting transition to initial position");

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;

        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            float t = transitionCurve.Evaluate(elapsedTime / transitionDuration);

            transform.position = Vector3.Lerp(startPosition, initialPosition, t);
            transform.rotation = Quaternion.Slerp(startRotation, initialRotation, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure final state is exact
        transform.position = initialPosition;
        transform.rotation = initialRotation;

        // Reset orientation and calibration to the initial state's perspective
        ResetOrientation(); // This will internally call Calibrate()

        isTransitioning = false;

        Debug.Log("Camera returned to initial position");
    }

    // Toggle raw orientation mode (for testing)
    public void ToggleRawOrientation()
    {
        useRawOrientation = !useRawOrientation;
        Debug.Log($"Raw orientation mode: {useRawOrientation}");

        if (statusText != null)
        {
            statusText.text = useRawOrientation ? "Raw Mode ON" : "Normal Mode"; // Assuming "Normal" means calibrated
            StartCoroutine(FadeOutText(statusText, 1.5f));
        }
    }

    // Toggle calibration application
    public void ToggleCalibration()
    {
        applyCalibration = !applyCalibration;
        Debug.Log($"Apply calibration: {applyCalibration}");

        if (statusText != null)
        {
            statusText.text = applyCalibration ? "Calibration ON" : "Calibration OFF";
            StartCoroutine(FadeOutText(statusText, 1.5f));
        }
    }

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
    }

    // Helper method to fade out UI text
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
}
