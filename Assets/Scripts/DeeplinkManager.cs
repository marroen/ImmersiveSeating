using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class DeepLinkManager : MonoBehaviour
{
    // https://github.com/Alan-III/General/blob/main/Game%20Development/Tutorials/Unity%20DeepLink/DeepLinkManager.cs
    // Reworked from above
    public static DeepLinkManager Instance { get; private set; }


    [Tooltip("Custom link name to match the deep link URL prefix.")]
    public string linkName = "seatselection";  // Example: "mylink" in unitydl://mylink

    public Dictionary<string, string> parameters = new Dictionary<string, string>();

    public GameObject camera = null;

    public string DeeplinkURL { get; private set; } = null;

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


        string mode = "gyro";  // Default mode if not specified in the URL
        if (parameters.TryGetValue("mode", out string modeParam) && modeParam == "swipe")
        {
            mode = "swipe";
        }

        var interactSwitcher = camera?.GetComponent<InteractSwitcher>();
        if (interactSwitcher != null)
        {
            if (mode == "gyro")
            {
                interactSwitcher.SwitchToGyro();
            }
            else if (mode == "swipe")
            {
                interactSwitcher.SwitchToSwipe();
            }
        }

        //TODO edit according to selection code
        if (parameters.TryGetValue("seat", out string directSeatStr))
        {
            /* Marti: commented this out for now due to compiler error
            int directSeat = ParseInt(directSeatStr);  // Parse the direct seat parameter from the URL

            var selectionManager = camera?.GetComponent<SelectionManager>();
            selectionManager.FlyToSeat(directSeat);
            
            
            Debug.Log($"Direct seat selection: {directSeat}");  // Log the direct seat selection
            */
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