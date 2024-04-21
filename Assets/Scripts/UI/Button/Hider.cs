using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Hider : MonoBehaviour
{
    public void UpdatePosition(GameObject goHere)
    {
        this.GetComponent<RectTransform>().position = goHere.transform.position;
    }

}
