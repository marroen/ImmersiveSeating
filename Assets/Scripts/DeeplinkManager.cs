using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;

public class DeepLinkManager : MonoBehaviour
{
    // https://github.com/Alan-III/General/blob/main/Game%20Development/Tutorials/Unity%20DeepLink/DeepLinkManager.cs
    // Reworked from above
    public static DeepLinkManager Instance { get; private set; }


    [Tooltip("Custom link name to match the deep link URL prefix.")]
    public string linkName = "seatselection";  // Example: "mylink" in unitydl://mylink

    public Dictionary<string, string> parameters = new Dictionary<string, string>();

    public Camera mainCamera = null;

    public string DeeplinkURL { get; private set; } = null;

    public GameObject standardSeat;
    public GameObject backSeat;
    public GameObject premiumSeat;

    public GameObject center;

    public SectionZoomController sectionZoomController;
    public CamRotation camRotationScript;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Application.deepLinkActivated += OnDeepLinkActivated;
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                OnDeepLinkActivated(Application.absoluteURL);
            }
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDeepLinkActivated(string url)
    {
        DeeplinkURL = url;  // Store the deep link URL
        parameters.Clear();  // Clear any previously stored parameters
        ExtractParametersFromUrl(url);

        if (!url.Contains(linkName))  // Check if the URL contains the expected link name
        {
            Debug.LogWarning($"URL does not contain the expected link name: {linkName}");  // Log a warning if the URL does not contain the expected link name
            return;
        }

        bool directSeat = false;
        if (parameters.TryGetValue("seat", out string directSeatStr))
        {

            GameObject touchedObject;
            switch (directSeatStr)
            {
                case "premium":
                    touchedObject = premiumSeat;
                    break;
                case "back":
                    touchedObject = backSeat;
                    break;
                case "standard":
                    touchedObject = standardSeat;
                    break;
                default:
                    Debug.Log("No valid direct seat given: should be premium, back or standard");
                    return;
            }
            directSeat = true;
            StartCoroutine(GoToSeat(touchedObject));  // Start the coroutine to go to the selected seat

        }


        string mode = "gyro";  // Default mode if not specified in the URL
        if (parameters.TryGetValue("mode", out string modeParam))
        {
            mode = modeParam.ToLower();
        }

        var interactSwitcher = mainCamera?.GetComponent<InteractSwitcher>();
        if (interactSwitcher != null)
        {
            if (mode == "gyro")
            {
                interactSwitcher.SwitchToGyro();
            }
            else if (mode == "swipe")
            {
                interactSwitcher.SwitchToSwipe();

                // I think I am fighting SwitchToSwipe, which resets rotations - this is a workaround
                if (directSeat)
                {

                    // TODO this workaround does not seem to work (maybe a wait is required?)
                    Vector3 directionToCenter = (center.transform.position - mainCamera.transform.position);
                    directionToCenter.y = 0;
                    Quaternion centerLookingRotation = Quaternion.LookRotation(directionToCenter);

                    mainCamera.transform.rotation = centerLookingRotation;
                }
            }
        }



    }

    // public void tester()
    // {
    //     StartCoroutine(GoToSeat(premiumSeat));  // Start the coroutine to go to the selected seat
    // }

    public IEnumerator GoToSeat(GameObject touchedObject)
    {

        if (camRotationScript != null)
        {
            camRotationScript.AllowExternalRotationControl = true;
        }

        // Position and rotate camera
        mainCamera.transform.position = touchedObject.transform.position + Vector3.up;
        mainCamera.orthographicSize = 1;

        Vector3 directionToCenter = (center.transform.position - mainCamera.transform.position);
        directionToCenter.y = 0;
        Quaternion centerLookingRotation = Quaternion.LookRotation(directionToCenter);

        mainCamera.transform.rotation = centerLookingRotation;

        // Small delay to ensure transform is applied
        yield return new WaitForSeconds(0.5f);
        Debug.Log("Camera positioned and waiting for gyro calibration");

        // Calibrate and re-enable gyro
        if (camRotationScript != null)
        {
            camRotationScript.SetNewInitialRotation(centerLookingRotation);
            camRotationScript.AllowExternalRotationControl = false;
            Debug.Log("Camera positioned and gyro calibrated");
        }
    }

    private void ExtractParametersFromUrl(string url)
    {
        var uri = new System.Uri(url);  // Create a new Uri object from the URL
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);  // Parse the query string part of the URL
        foreach (var key in query.AllKeys)  // Iterate over all keys in the query string
        {

            parameters[key] = query.Get(key);  // Add the parameter to the dictionary

        }
    }
}