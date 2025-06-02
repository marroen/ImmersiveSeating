using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SectionZoomController : MonoBehaviour
{
    [Header("Camera Settings")]
    public Camera mainCamera;
    public float zoomDuration = 1.5f;
    public AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Gyro Control")]
    public CamRotation camRotationScript; // Reference to CamRotation script

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

    private Vector3 originalCameraPosition;
    private float originalCameraSize;
    private Vector3 originalCameraRotation; // Store full rotation as Vector3
    private float currentCameraZRotation; // Track only Z rotation separately
    private bool isZooming = false;

    // Track current zoom state
    private string currentZoomedSection = null;
    private bool isZoomed = false;

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

        if (camRotationScript == null)
            camRotationScript = FindObjectOfType<CamRotation>();

        // Store original camera settings
        originalCameraPosition = mainCamera.transform.position;
        originalCameraSize = mainCamera.orthographicSize;
        originalCameraRotation = mainCamera.transform.eulerAngles; // Store full rotation
        currentCameraZRotation = originalCameraRotation.z; // Initialize current Z rotation

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
            Debug.Log("Something was touched!");
            GameObject touchedObject = hit.collider.gameObject;
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
                // If touching a different section or non-section object, zoom out
                else
                {
                    Debug.Log($"Touching different section ({touchedSection}), zooming out");
                    ZoomToOriginal();
                    return;
                }
            }

            // If we're not zoomed in, zoom into the touched section
            if (touchedSection != null)
            {
                Debug.Log($"Zooming to section ({touchedSection})");
                ZoomToSection(touchedSection);
            }
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

    IEnumerator ZoomToPosition(Vector3 targetPosition, float targetSize, float targetRotation, bool zoomingIn = false)
    {
        isZooming = true;

        if (camRotationScript != null)
        {
            camRotationScript.AllowExternalRotationControl = true;
        }

        Vector3 startPosition = mainCamera.transform.position;
        float startSize = mainCamera.orthographicSize;
        /*float startZRotation = currentCameraZRotation; // Use tracked Z rotation

        // Get current X and Y rotations to preserve them
        Vector3 currentEulerAngles = mainCamera.transform.eulerAngles;
        float xRotation = currentEulerAngles.x;
        float yRotation = currentEulerAngles.y;

        // Handle rotation wrapping for smooth animation
        float rotationDifference = Mathf.DeltaAngle(startZRotation, targetRotation);
        float endZRotation = startZRotation + rotationDifference; */

        Quaternion qRotation;

        /*
        if (targetRotation > 0)
            qRotation = Quaternion.Euler(90, 90, targetRotation);
        else
            qRotation = mainCamera.transform.rotation; */

        /*
        if (zoomingIn && targetRotation == 0)
            qRotation = Quaternion.Euler(90, 90, 180);
        else
            qRotation = Quaternion.Euler(90, 90, targetRotation); */

        qRotation = Quaternion.Euler(90, 90, targetRotation);

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

        //mainCamera.transform.rotation = Quaternion.RotateTowards(mainCamera.transform.rotation, myRotation, 1.0f);

        // Ensure final values are set exactly
        
        mainCamera.transform.position = targetPosition;
        mainCamera.orthographicSize = targetSize;
        //currentCameraZRotation = targetRotation; // Update tracked Z rotation
        mainCamera.transform.rotation = qRotation;

        // Update zoom state
        isZoomed = zoomingIn;
        if (!isZoomed)
        {
            currentZoomedSection = null;

            if (camRotationScript != null)
            {
                camRotationScript.AllowExternalRotationControl = false;
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
            StartCoroutine(ZoomToPosition(originalCameraPosition, originalCameraSize, originalCameraRotation.z, false));
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