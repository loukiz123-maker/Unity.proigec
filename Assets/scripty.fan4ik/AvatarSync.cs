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

        // ѕопытка найти камеру, если мы владелец
        if (_realtimeView != null && _realtimeView.isOwnedLocallyInHierarchy)
        {
            if (Camera.main != null)
                _mainCamera = Camera.main.transform;
        }
    }

    void Update()
    {
        // 1. ѕровер€ем, существует ли RealtimeView и инициализирован ли он (есть ли модель)
        if (_realtimeView == null || _realtimeView.isOwnedLocallyInHierarchy == false) return;

        // 2. “олько если мы владельцы этого аватара, обновл€ем поворот головы
        if (_realtimeView.isOwnedLocallyInHierarchy && _mainCamera != null && headTransform != null)
        {
            headTransform.rotation = _mainCamera.rotation;
        }
    }
}