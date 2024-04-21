using TMPro;
using UnityEngine;

public class VisionFollower : MonoBehaviour
{
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private float _distance = 1.0f;
    [SerializeField] private bool _isFollower = true;
    // Update is called once per frame
    private void Update()
    {
        Vector3 targetPosition = TargetPosition();
        if (_isFollower && !ReachPosition(targetPosition))
            MoveTowards(targetPosition);

        transform.LookAt(_cameraTransform.position);
    }

    private Vector3 TargetPosition()
    {
        return _cameraTransform.position + (_cameraTransform.forward * _distance);
    }

    private void MoveTowards(Vector3 targetPosition)
    {
        transform.position += (targetPosition - transform.position) * Time.deltaTime;
    }

    private bool ReachPosition(Vector3 targetPosition)
    {
        return Vector3.Distance(transform.position, targetPosition) < 0.1f;
    }
}
