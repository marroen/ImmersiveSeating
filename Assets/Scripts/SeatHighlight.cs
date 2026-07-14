using UnityEngine;

public class SeatHighlight : MonoBehaviour
{
    [SerializeField] private float minAlpha = 0.2f;
    [SerializeField] private float maxAlpha = 0.6f;
    [SerializeField] private float pulseSpeed = 1.5f;

    private Material mat;

    void Start()
    {
        mat = GetComponent<Renderer>().material;
    }

    void Update()
    {
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(Time.time * pulseSpeed) + 1) / 2);
        Color c = mat.color;
        c.a = alpha;
        mat.color = c;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        Debug.Log($"Hiding Highlight of {gameObject.transform.GetInstanceID()}...");
    }

    public void Show()
    {
        gameObject.SetActive(true);
        Debug.Log($"Showing Highlight of {gameObject.name}...");
    }
}