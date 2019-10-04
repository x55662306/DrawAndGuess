using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PressKey : MonoBehaviour
{

    public string inputName;
    Button buttonMe;
    // Use this for initialization
    void Start()
    {
        buttonMe = GetComponent<Button>();
    }

    void Update()
    {
        if (Input.GetKeyDown(inputName))
        {
            buttonMe.onClick.Invoke();
        }


    }
}