using UnityEngine;
using UnityEngine.UI;

public class TopBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image logoImage;
    [SerializeField] private RectTransform barPanel;

    [Header("Settings")]
    [SerializeField] private float barHeight = 60f;
    [SerializeField] private Color barColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    [SerializeField] private Vector2 logoSize = new Vector2(50f, 50f);
    [SerializeField] private float logoPadding = 5f;

    private void Awake()
    {
        // Make sure this canvas persists between scene loads if needed
        // DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SetupBarAppearance();
    }

    private void SetupBarAppearance()
    {
        // Set the bar panel color
        Image barImage = barPanel.GetComponent<Image>();
        if (barImage != null)
        {
            barImage.color = barColor;
        }

        // Adjust logo size if needed
        if (logoImage != null)
        {
            RectTransform logoRect = logoImage.rectTransform;
            logoRect.sizeDelta = logoSize;

            // Position the logo on the left with some padding
            logoRect.anchoredPosition = new Vector2(logoSize.x / 2 + logoPadding, 0);
        }
    }

    // If you need the bar to dynamically adjust to screen size changes
    private void OnRectTransformDimensionsChange()
    {
        UpdateBarSize();
    }

    private void UpdateBarSize()
    {
        if (barPanel != null)
        {
            // Get the canvas width
            Canvas canvas = GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            float width = canvasRect.rect.width;

            // Set the bar width to match screen width
            barPanel.sizeDelta = new Vector2(width, barHeight);

            // Position at top of screen
            barPanel.anchoredPosition = new Vector2(0, -barHeight / 2);
        }
    }
}