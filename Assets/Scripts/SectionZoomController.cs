using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class SectionZoomController : MonoBehaviour
{
    [Header("Camera Settings")]
    public Camera mainCamera;
    public float zoomDuration = 1.5f;
    public AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Center Stage")]
    public GameObject center;

    [Header("Gyro Control")]
    public CamRotationGyro camRotationGyroScript; // Reference to CamRotation script

    [Header("Swipe Control")]
    public CamRotationSwipe camRotationSwipeScript; // Reference to CamRotation script

    [Header("Top View")]
    public Button topViewButton;

    [Header("Section Zoom Positions")]
    public Vector3 mainSectionZoomPosition = new Vector3(0, 5, -10);
    public Vector3 leftSectionZoomPosition = new Vector3(-5, 5, -10);
    public Vector3 rightSectionZoomPosition = new Vector3(5, 5, -10);
    public Vector3 backSectionZoomPosition = new Vector3(0, 5, 10);

    [Header("Section Settings")]
    public float mainSectionZoomSize = 3f;
    public float leftSectionZoomSize = 3f;
    public float rightSectionZoomSize = 3f;
    public float backSectionZoomSize = 3f;

    [Header("Section Rotations (in degrees)")]
    public float mainSectionRotation = 0f;
    public float leftSectionRotation = 90f;   // 90 degrees clockwise
    public float rightSectionRotation = -90f; // 90 degrees counter-clockwise
    public float backSectionRotation = 180f;  // 180 degrees

    [Header("Price Display")]
    public Button buyButton;
    public TextMeshProUGUI priceText; // Reference to the price text UI element
    public float price1 = 50f;  // Price for first seat type
    public float price2 = 75f;  // Price for second seat type
    public float price3 = 100f; // Price for third seat type

    private Vector3 originalCameraPosition;
    private float originalCameraSize;
    private Vector3 originalCameraRotation; // Store full rotation as Vector3
    private Quaternion originalCameraRotationQuaternion; // Store original rotation as Quaternion for gyro reset
    private bool isZooming = false;

    // Track current zoom state
    public string currentZoomedSection = null;
    // We could maybe make a getter/setter here 

    public bool isZoomed = false;
    private bool isInSeatView = false; // Track if we're in seat POV

    // Lists to store child objects by section
    private List<GameObject> mainSectionObjects = new List<GameObject>();
    private List<GameObject> leftSectionObjects = new List<GameObject>();
    private List<GameObject> rightSectionObjects = new List<GameObject>();
    private List<GameObject> backSectionObjects = new List<GameObject>();

    void Start()
    {
        // If no camera is assigned, use the main camera
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (camRotationGyroScript == null)
        {
            camRotationGyroScript = FindObjectOfType<CamRotationGyro>();
            camRotationGyroScript.AllowExternalRotationControl = true;
        }
            
        if (camRotationSwipeScript == null)
        {
            camRotationSwipeScript = FindObjectOfType<CamRotationSwipe>();
            camRotationSwipeScript.AllowExternalRotationControl = true;
        }

        // Distinguish if launched from Deeplink or normally
        if (topViewButton != null)
        {
            
            // Deeplink
            if (isInSeatView) 
            {
                topViewButton.onClick.AddListener(ZoomToOriginal);
                topViewButton.gameObject.SetActive(true);
                
                // Set original camera values by hardcoding
                originalCameraPosition = new Vector3(45f, 120f, -0.22f);
                originalCameraSize = 5f;
                originalCameraRotation = new Vector3(70f, 270f, 0f);
                originalCameraRotationQuaternion = new Quaternion(0.40558f, -0.57923f, 0.40558f, 0.57923f);

                // Make sure price text is visible
                if (priceText != null && buyButton != null)
                {
                    priceText.gameObject.SetActive(true);
                    buyButton.gameObject.SetActive(true);
                }
            }
            // Normally
            else
            {
                topViewButton.gameObject.SetActive(false);

                // Set original camera values by reference
                originalCameraPosition = mainCamera.transform.position;
                originalCameraSize = mainCamera.orthographicSize;
                originalCameraRotation = mainCamera.transform.eulerAngles; // Store full rotation
                originalCameraRotationQuaternion = mainCamera.transform.rotation; // Store original rotation as Quaternion

                // Make sure price text is initially hidden
                if (priceText != null && buyButton != null)
                {
                    priceText.gameObject.SetActive(false);
                    buyButton.gameObject.SetActive(false);
                }
            }
            
            Debug.Log("topViewButton set up");
        }

        // Categorize child objects by their tags
        CategorizeChildObjects();

        
    }

    void CategorizeChildObjects()
    {
        // Clear existing lists
        mainSectionObjects.Clear();
        leftSectionObjects.Clear();
        rightSectionObjects.Clear();
        backSectionObjects.Clear();

        // Loop through all child objects
        foreach (Transform child in transform)
        {
            GameObject childObj = child.gameObject;

            // Categorize based on tag
            switch (childObj.tag)
            {
                case "MainSection":
                    mainSectionObjects.Add(childObj);
                    break;
                case "LeftSection":
                    leftSectionObjects.Add(childObj);
                    break;
                case "RightSection":
                    rightSectionObjects.Add(childObj);
                    break;
                case "BackSection":
                    backSectionObjects.Add(childObj);
                    break;
            }
        }

        Debug.Log($"Categorized objects - Main: {mainSectionObjects.Count}, Left: {leftSectionObjects.Count}, Right: {rightSectionObjects.Count}, Back: {backSectionObjects.Count}");
    }

    void Update()
    {
        // Handle touch input for Android
        if (Input.touchCount > 0 && !isZooming)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                HandleTouchInput(touch.position);
            }
        }

        // Handle mouse input for testing in editor
#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0) && !isZooming)
        {
            HandleTouchInput(Input.mousePosition);
        }
#endif
    }

    void HandleTouchInput(Vector2 screenPosition)
    {
        // Convert screen position to world position
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        RaycastHit hit;

        // Perform raycast to detect which object was touched
        if (Physics.Raycast(ray, out hit))
        {
            GameObject touchedObject = hit.collider.gameObject;
            Debug.Log($"{touchedObject.name} was touched!");
            string touchedSection = GetObjectSection(touchedObject);

            // If we're currently zoomed in
            if (isZoomed && currentZoomedSection != null)
            {
                // If touching the same section, do nothing (stay zoomed)
                if (touchedSection == currentZoomedSection)
                {
                    Debug.Log($"Already zoomed into {currentZoomedSection} section");
                    return;
                }
                else if (touchedObject.tag == "Available")
                {
                    Debug.Log($"Touched available seat!");

                    

                    StartCoroutine(GoToSeat(touchedObject));
                }
            }
            else if (touchedSection != null)
            {
                Debug.Log($"Zooming to section ({touchedSection})");
                HidePriceText();
                topViewButton.gameObject.SetActive(true);
                topViewButton.onClick.AddListener(ZoomToOriginal);
                ZoomToSection(touchedSection);
            }
        
        }
    }

    // Method to determine seat price based on the seat object
    float GetSeatPrice(GameObject seatObject)
    {
        // You can implement different logic here to determine price
        // For example, based on seat name, position, or a custom component

        // Option 1: Based on seat name
        if (seatObject.name.Contains("Premium"))
            return price3;
        else if (seatObject.name.Contains("Standard"))
            return price2;
        else
            return price1;
    }

    // Method to show price text
    void ShowPriceText(float price)
    {
        if (priceText != null && buyButton != null)
        {
            priceText.text = $"€{price:F0}";
            priceText.gameObject.SetActive(true);
            buyButton.gameObject.SetActive(true);
            isInSeatView = true;
            Debug.Log($"Showing price: €{price}");
        }
    }

    // Method to hide price text
    void HidePriceText()
    {
        if (priceText != null && buyButton != null)
        {
            priceText.gameObject.SetActive(false);
            buyButton.gameObject.SetActive(false);
            isInSeatView = false;
            Debug.Log("Hiding price");
        }
    }

    public IEnumerator GoToSeat(GameObject touchedObject)
    {
        isInSeatView = true;

        // Get the price for this seat and show it
        float seatPrice = GetSeatPrice(touchedObject);
        ShowPriceText(seatPrice);

        if (camRotationGyroScript != null)
        {
            camRotationGyroScript.AllowExternalRotationControl = true;
        }

        if (camRotationSwipeScript != null)
        {
            camRotationSwipeScript.AllowExternalRotationControl = true;
        }

        // Position and rotate camera
        mainCamera.transform.position = touchedObject.transform.position + (Vector3.up / 2);
        mainCamera.orthographicSize = 1;

        Vector3 directionToCenter = (center.transform.position - mainCamera.transform.position);
        directionToCenter.y = 0;
        Quaternion centerLookingRotation = Quaternion.LookRotation(directionToCenter);

        mainCamera.transform.rotation = centerLookingRotation;

        // Small delay to ensure transform is applied
        yield return new WaitForSeconds(0.5f);

        // Calibrate and re-enable gyro
        if (camRotationGyroScript != null)
        {
            camRotationGyroScript.SetNewInitialRotation(centerLookingRotation);
            camRotationGyroScript.AllowExternalRotationControl = false;
            Debug.Log("Camera positioned and gyro calibrated");
        }

        // Re-enable swipe
        if (camRotationSwipeScript != null)
        {
            camRotationSwipeScript.SetNewInitialRotation(centerLookingRotation);
            camRotationSwipeScript.AllowExternalRotationControl = false;
            Debug.Log("Camera positioned and swipe activated");
        }
    }

    // Helper method to determine which section an object belongs to
    string GetObjectSection(GameObject obj)
    {
        if (mainSectionObjects.Contains(obj))
            return "MainSection";
        else if (leftSectionObjects.Contains(obj))
            return "LeftSection";
        else if (rightSectionObjects.Contains(obj))
            return "RightSection";
        else if (backSectionObjects.Contains(obj))
            return "BackSection";
        else
            return null; // Object doesn't belong to any section
    }

    void ZoomToSection(string sectionType)
    {
        Vector3 targetPosition;
        float targetSize;
        float targetRotation;

        // Determine target position, zoom size, and rotation based on section type
        switch (sectionType)
        {
            case "MainSection":
                targetPosition = mainSectionZoomPosition;
                targetSize = mainSectionZoomSize;
                targetRotation = mainSectionRotation;
                break;
            case "LeftSection":
                targetPosition = leftSectionZoomPosition;
                targetSize = leftSectionZoomSize;
                targetRotation = leftSectionRotation;
                break;
            case "RightSection":
                targetPosition = rightSectionZoomPosition;
                targetSize = rightSectionZoomSize;
                targetRotation = rightSectionRotation;
                break;
            case "BackSection":
                targetPosition = backSectionZoomPosition;
                targetSize = backSectionZoomSize;
                targetRotation = backSectionRotation;
                break;
            default:
                return;
        }

        // Update zoom state
        currentZoomedSection = sectionType;

        // Start zoom coroutine
        StartCoroutine(ZoomToPosition(targetPosition, targetSize, targetRotation, true));
    }

    public IEnumerator ZoomToPosition(Vector3 targetPosition, float targetSize, float targetRotation, bool zoomingIn = false, bool toOriginal = false)
    {
        isZooming = true;

        if (camRotationGyroScript != null)
        {
            camRotationGyroScript.AllowExternalRotationControl = true;
            Debug.Log($"AllowExternal: {camRotationGyroScript.AllowExternalRotationControl}");
        }

        if (camRotationSwipeScript != null)
        {
            camRotationSwipeScript.AllowExternalRotationControl = true;
            Debug.Log($"AllowExternal: {camRotationSwipeScript.AllowExternalRotationControl}");
        }

        Vector3 startPosition = mainCamera.transform.position;
        float startSize = mainCamera.orthographicSize;

        Quaternion qRotation;

        if (toOriginal)
        {
            qRotation = Quaternion.Euler(110, 90, targetRotation - 180);
        }
        else
        {
            qRotation = Quaternion.Euler(90, 90, targetRotation);
        }

        float elapsedTime = 0f;

        while (elapsedTime < zoomDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / zoomDuration;
            float curveValue = zoomCurve.Evaluate(progress);

            // Interpolate camera position, size, and rotation
            mainCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, curveValue);
            mainCamera.orthographicSize = Mathf.Lerp(startSize, targetSize, curveValue);

            // Smoothly rotate only the Z axis, preserving X and Y
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, qRotation, curveValue);

            yield return null;
        }

        // Ensure final values are set exactly
        mainCamera.transform.position = targetPosition;
        mainCamera.orthographicSize = targetSize;
        mainCamera.transform.rotation = qRotation;

        // Update zoom state
        isZoomed = zoomingIn;
        if (!zoomingIn)
        {
            currentZoomedSection = null;

            if (camRotationGyroScript != null)
            {
                if (!toOriginal)
                {
                    camRotationGyroScript.AllowExternalRotationControl = false;
                    Debug.Log($"AllowExternal: {camRotationGyroScript.AllowExternalRotationControl}");
                }
                else
                {
                    camRotationGyroScript.Calibrate();
                }

            }

            if (camRotationSwipeScript != null)
            {
                if (!toOriginal)
                {
                    camRotationSwipeScript.AllowExternalRotationControl = false;
                    Debug.Log($"AllowExternal: {camRotationSwipeScript.AllowExternalRotationControl}");
                }
                /* necessary for swipe ?
                else
                {
                    camRotationSwipeScript.Calibrate();
                }*/

            }

        }

        isZooming = false;
    }

    // Public method to zoom back to original position
    public void ZoomToOriginal()
    {
        if (!isZooming)
        {
            Debug.Log("Zooming back to original position");

            // Hide price text when leaving seat view
            if (isInSeatView)
            {
                HidePriceText();
            }

            topViewButton.gameObject.SetActive(false);

            StartCoroutine(ZoomToPosition(originalCameraPosition, originalCameraSize, originalCameraRotation.z, false, true));

            // Reset the gyro initial rotation back to the original camera rotation
            if (camRotationGyroScript != null)
            {
                camRotationGyroScript.AllowExternalRotationControl = true;
                Debug.Log($"AllowExternal: {camRotationGyroScript.AllowExternalRotationControl}");

                camRotationGyroScript.SetNewInitialRotation(originalCameraRotationQuaternion);
                Debug.Log($"Reset gyro initial rotation to original: {originalCameraRotationQuaternion.eulerAngles}");

                camRotationGyroScript.AllowExternalRotationControl = true;
                Debug.Log($"AllowExternal: {camRotationGyroScript.AllowExternalRotationControl}");
            }

            // Lock swipe
            if (camRotationSwipeScript != null)
            {
                camRotationSwipeScript.AllowExternalRotationControl = true;
                Debug.Log($"AllowExternal: {camRotationSwipeScript.AllowExternalRotationControl}");

                camRotationSwipeScript.SetNewInitialRotation(originalCameraRotationQuaternion);
                Debug.Log($"Reset swipe initial rotation to original: {originalCameraRotationQuaternion.eulerAngles}");

                camRotationSwipeScript.AllowExternalRotationControl = true;
                Debug.Log($"AllowExternal: {camRotationSwipeScript.AllowExternalRotationControl}");
            }
        }
    }

    // Public method to manually set section zoom positions (useful for runtime adjustments)
    public void SetSectionZoomPosition(string sectionType, Vector3 newPosition)
    {
        switch (sectionType)
        {
            case "MainSection":
                mainSectionZoomPosition = newPosition;
                break;
            case "LeftSection":
                leftSectionZoomPosition = newPosition;
                break;
            case "RightSection":
                rightSectionZoomPosition = newPosition;
                break;
            case "BackSection":
                backSectionZoomPosition = newPosition;
                break;
        }
    }

    // Public method to manually set section rotations (useful for runtime adjustments)
    public void SetSectionRotation(string sectionType, float newRotation)
    {
        switch (sectionType)
        {
            case "MainSection":
                mainSectionRotation = newRotation;
                break;
            case "LeftSection":
                leftSectionRotation = newRotation;
                break;
            case "RightSection":
                rightSectionRotation = newRotation;
                break;
            case "BackSection":
                backSectionRotation = newRotation;
                break;
        }
    }

    // Public method to recategorize objects (useful if objects are added/removed at runtime)
    public void RefreshCategories()
    {
        CategorizeChildObjects();
    }

    // Debug method to visualize zoom positions in Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(mainSectionZoomPosition, Vector3.one * 2f);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(leftSectionZoomPosition, Vector3.one * 2f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(rightSectionZoomPosition, Vector3.one * 2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(backSectionZoomPosition, Vector3.one * 2f);
    }
}