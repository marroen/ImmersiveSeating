using UnityEngine;
using UnityEngine.UI;

public class Selection : MonoBehaviour
{
    [SerializeField] private Button hideButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (hideButton != null)
        {
            hideButton.onClick.AddListener(HideSeatMap);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void HideSeatMap()
    {
        gameObject.SetActive(false);
    }
}
