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

    //public Dictionary<string, string> parameters = new Dictionary<string, string>();

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
        //parameters.Clear();  // Clear any previously stored parameters

        if (url.Contains(linkName))  // Check if the URL contains the expected link name
        {
            // Code here if it needs to do something different from just opening
            // TODO

        }
        else
        {
            Debug.LogWarning($"URL does not contain the expected link name: {linkName}");  // Log a warning if the URL does not contain the expected link name
        }
    }
}