using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class HoldableButton : MonoBehaviour, IUpdateSelectedHandler, IPointerDownHandler, IPointerUpHandler
{
    private bool _isPressed;

    public UnityEvent onHold;
    public void OnUpdateSelected(BaseEventData data)
    {
        if (_isPressed)
        {
            onHold.Invoke();
        }
    }
    public void OnPointerDown(PointerEventData data)
    {
        _isPressed = true;
    }
    public void OnPointerUp(PointerEventData data)
    {
        _isPressed = false;
    }
}