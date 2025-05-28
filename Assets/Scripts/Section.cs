using UnityEngine;

public class Section : MonoBehaviour
{
    
    [SerializeField] private int sectionIndex;
    [SerializeField] private GameObject seatPanel;
    [SerializeField] private GameObject sectionPanel;
    public void TaskOnClick()
    {
        Debug.Log("Section clicked: " + sectionIndex);

        seatPanel.SetActive(true);
        sectionPanel.SetActive(false);
    }
}
