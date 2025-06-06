using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class InteractSwitcher : MonoBehaviour
{
    
    [Header("Camera Scripts")]
    [SerializeField] private CamRotationGyro gyroScript;
    [SerializeField] private CamRotationSwipe swipeScript;

    [Header("UI Buttons")]
    [SerializeField] private Button gyroButton;
    [SerializeField] private Button swipeButton;

    [Header("Button Styling")]
    [SerializeField] private Color activeButtonColor = Color.green;
    [SerializeField] private Color inactiveButtonColor = Color.white;

    [Header("Status Display")]
    [SerializeField] private Text statusText;
    [SerializeField] private float statusDisplayTime = 2f;

    [Header("Default Mode")]
    [SerializeField] private RotationMode defaultMode = RotationMode.Gyro;

    public enum RotationMode
    {
        Gyro,
        Swipe
    }

    private RotationMode currentMode;
    private ColorBlock gyroButtonColors;
    private ColorBlock swipeButtonColors;

    void Start()
    {
        // Store original button colors
        if (gyroButton != null)
        {
            gyroButtonColors = gyroButton.colors;
        }

        if (swipeButton != null)
        {
            swipeButtonColors = swipeButton.colors;
        }

        // Set up button listeners
        SetupButtons();

        // Set initial mode
        SetRotationMode(defaultMode);

        Debug.Log("CameraRotationSwitcher initialized");
    }

    private void SetupButtons()
    {
        if (gyroButton != null)
        {
            gyroButton.onClick.AddListener(() => SwitchToGyro());
            Debug.Log("Gyro button set up");
        }
        else
        {
            Debug.LogWarning("Gyro button not assigned in inspector!");
        }

        if (swipeButton != null)
        {
            swipeButton.onClick.AddListener(() => SwitchToSwipe());
            Debug.Log("Swipe button set up");
        }
        else
        {
            Debug.LogWarning("Swipe button not assigned in inspector!");
        }
    }

    public void SwitchToGyro()
    {
        Debug.Log("Switching to Gyro rotation mode");
        SetRotationMode(RotationMode.Gyro);

        if (statusText != null)
        {
            statusText.text = "Switched to Gyro Control";
            StartCoroutine(FadeOutStatusText());
        }
    }

    public void SwitchToSwipe()
    {
        Debug.Log("Switching to Swipe rotation mode");
        SetRotationMode(RotationMode.Swipe);

        if (statusText != null)
        {
            statusText.text = "Switched to Touch Control";
            StartCoroutine(FadeOutStatusText());
        }
    }

    private void SetRotationMode(RotationMode mode)
    {
        currentMode = mode;

        Debug.Log($"Attempting to set rotation mode to: {mode}");

        // Disable both scripts first
        if (gyroScript != null)
        {
            gyroScript.enabled = false;
            Debug.Log("Gyro script disabled");
        }

        if (swipeScript != null)
        {
            swipeScript.enabled = false;
            Debug.Log("Swipe script disabled");
        }

        // Wait a frame before enabling the new script
        StartCoroutine(EnableScriptAfterFrame(mode));
    }

    private IEnumerator EnableScriptAfterFrame(RotationMode mode)
    {
        yield return null; // Wait one frame

        // Enable the selected script
        switch (mode)
        {
            case RotationMode.Gyro:
                if (gyroScript != null)
                {
                    gyroScript.enabled = true;
                    Debug.Log("Gyro script enabled");

                    // Wait another frame then calibrate
                    yield return null;
                    try
                    {
                        gyroScript.Calibrate();
                        Debug.Log("Gyro script calibrated");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Gyro calibration failed: {e.Message}");
                    }
                }
                break;

            case RotationMode.Swipe:
                if (swipeScript != null)
                {
                    swipeScript.enabled = true;
                    Debug.Log("Swipe script enabled");

                    // Wait another frame then reset
                    yield return null;
                    try
                    {
                        swipeScript.ResetRotation();
                        Debug.Log("Swipe script reset");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Swipe reset failed: {e.Message}");
                    }
                }
                break;
        }

        // Update button appearance
        UpdateButtonAppearance();

        Debug.Log($"Rotation mode successfully set to: {mode}");
    }

    private void UpdateButtonAppearance()
    {
        // Update gyro button
        if (gyroButton != null)
        {
            ColorBlock colors = gyroButtonColors;
            colors.normalColor = (currentMode == RotationMode.Gyro) ? activeButtonColor : inactiveButtonColor;
            colors.selectedColor = colors.normalColor;
            gyroButton.colors = colors;
        }

        // Update swipe button
        if (swipeButton != null)
        {
            ColorBlock colors = swipeButtonColors;
            colors.normalColor = (currentMode == RotationMode.Swipe) ? activeButtonColor : inactiveButtonColor;
            colors.selectedColor = colors.normalColor;
            swipeButton.colors = colors;
        }
    }

    public RotationMode GetCurrentMode()
    {
        return currentMode;
    }

    public void ToggleMode()
    {
        RotationMode newMode = (currentMode == RotationMode.Gyro) ? RotationMode.Swipe : RotationMode.Gyro;
        SetRotationMode(newMode);
    }

    private IEnumerator FadeOutStatusText()
    {
        if (statusText == null) yield break;

        // Ensure text is visible
        Color originalColor = statusText.color;
        statusText.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);

        // Wait for display time
        yield return new WaitForSeconds(statusDisplayTime);

        // Fade out
        float fadeDuration = 1.0f;
        float elapsedTime = 0;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(1.0f - (elapsedTime / fadeDuration));
            statusText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        statusText.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0);
    }

    // Public methods for external control
    public void EnableGyroMode()
    {
        SwitchToGyro();
    }

    public void EnableSwipeMode()
    {
        SwitchToSwipe();
    }

    // Method to check if gyroscope is available (useful for automatic fallback)
    public bool IsGyroAvailable()
    {
        return SystemInfo.supportsGyroscope;
    }

    // Override the auto-fallback for desktop testing
    void Update()
    {
        // Commented out automatic fallback for desktop testing
        /*
        // Optional: Automatically switch to swipe if gyro becomes unavailable
        if (currentMode == RotationMode.Gyro && !IsGyroAvailable())
        {
            Debug.LogWarning("Gyroscope not available, switching to swipe mode");
            SwitchToSwipe();
        }
        */
    }
}