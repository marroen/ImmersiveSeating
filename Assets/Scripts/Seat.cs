using UnityEngine;
using System.Collections;

public class Seat : MonoBehaviour
{
    [SerializeField] private float seatX;
    [SerializeField] private float seatY;
    [SerializeField] private float seatZ;
    [SerializeField] private float seatRotationX;
    [SerializeField] private float seatRotationY;

    [SerializeField] private GameObject camera;
    [SerializeField] private float fadeDuration = 0.5f;

    [SerializeField] private GameObject sectionPanel;
    [SerializeField] private GameObject seatPanel;

    [SerializeField] private int seatPrice;
    [SerializeField] private int seatIndex; // Idk if we will use this


    public void TaskOnClick()
    {
        Debug.Log("Seat clicked!");

        camera.transform.position = new Vector3(seatX, seatY, seatZ);
        camera.transform.rotation = Quaternion.Euler(seatRotationX, seatRotationY, 0f);

        seatPanel.SetActive(false);
    }

    public void BackToSections()
    {
        Debug.Log("Back to sections clicked!");
        seatPanel.SetActive(false);
        sectionPanel.SetActive(true);
    }


}


