
using UnityEngine;

public class SetPosition : MonoBehaviour
{
    [SerializeField] private Transform _resetTransform;
    [SerializeField] private GameObject _player;
    [SerializeField] private Camera _playerHead;

    public void ResetPosition()
    {
        float rotationAnglesY = _resetTransform.rotation.eulerAngles.y - _playerHead.transform.rotation.eulerAngles.y;
        _player.transform.Rotate(0, rotationAnglesY, 0);
        Vector3 distanceDiff = _resetTransform.position - _playerHead.transform.position;
        _player.transform.position += distanceDiff;
    }

}
