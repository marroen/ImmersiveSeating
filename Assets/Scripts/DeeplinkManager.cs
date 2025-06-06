using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;
using Unity.VisualScripting;

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
    public CamRotationGyro camRotationGyroScript;
    public CamRotationSwipe camRotationSwipeScript;

    private void Awake()
    {
        if (Instance == null)
        {
            Debug.Log("Awaking DeeplinkManager");
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
        Debug.Log("Deeplink activated!");
        DeeplinkURL = url;  // Store the deep link URL
        parameters.Clear();  // Clear any previously stored parameters
        ExtractParametersFromUrl(url);

        if (!url.Contains(linkName))  // Check if the URL contains the expected link name
        {
            Debug.LogWarning($"URL does not contain the expected link name: {linkName}");  // Log a warning if the URL does not contain the expected link name
            return;
        }

        string mode = "gyro";  // Default mode if not specified in the URL
        if (parameters.TryGetValue("mode", out string modeParam))
        {
            mode = modeParam.ToLower();
        }
        StartCoroutine(SetMode(mode));


        if (parameters.TryGetValue("seat", out string directSeatStr))
        {

            GameObject touchedObject;
            string section;
            switch (directSeatStr)
            {
                case "premium":
                    touchedObject = premiumSeat;
                    section = "LeftSection";
                    break;
                case "back":
                    touchedObject = backSeat;
                    section = "BackSection";
                    break;
                case "standard":
                    touchedObject = standardSeat;
                    section = "MainSection";
                    break;
                default:
                    Debug.Log("No valid direct seat given: should be premium, back or standard");
                    return;
            }


            StartCoroutine(sectionZoomController.GoToSeat(touchedObject));

            //  bit of a hack, but required 
            sectionZoomController.currentZoomedSection = section;
            sectionZoomController.isZoomed = true;
            sectionZoomController.topViewButton.gameObject.SetActive(true);
            sectionZoomController.topViewButton.onClick.AddListener(sectionZoomController.ZoomToOriginal);
        }
    }

    IEnumerator SetMode(string mode)
    {
        yield return new WaitForSeconds(.5f);
        var interactSwitcher = mainCamera?.GetComponent<InteractSwitcher>();
        if (interactSwitcher != null)
        {
            if (mode == "swipe")
            {
                interactSwitcher.SwitchToSwipe();
            }
            else if (mode == "gyro")
            {
                interactSwitcher.SwitchToGyro();
            }
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