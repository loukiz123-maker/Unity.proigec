using UnityEngine;
using Normal.Realtime;

public class AvatarSync : MonoBehaviour
{
    public Transform headTransform;
    private Transform _mainCamera;
    private RealtimeView _realtimeView;

    void Start()
    {
        _realtimeView = GetComponent<RealtimeView>();

        if (_realtimeView != null && _realtimeView.isOwnedLocallyInHierarchy)
        {
            if (Camera.main != null)
                _mainCamera = Camera.main.transform;
        }
    }

    void Update()
    {
        if (_realtimeView == null || _realtimeView.isOwnedLocallyInHierarchy == false) return;

        if (_realtimeView.isOwnedLocallyInHierarchy && _mainCamera != null && headTransform != null)
        {
            headTransform.rotation = _mainCamera.rotation;
        }
    }
}