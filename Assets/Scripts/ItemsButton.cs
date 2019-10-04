using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ItemsButton : MonoBehaviour {

    public GameObject shadingItem;

    // Use this for initialization
    void Start ()
    {
        //shadingItem = GameObject.Find("ShadingItem").GetComponent<GameObject>();
    }
	
	public void OpenItemsBag()
    {
        shadingItem.gameObject.SetActive(!shadingItem.gameObject.active);
    }
}
