using UnityEngine;

public class VRBodyPushback : MonoBehaviour
{
    [Header("Настройки")]
    public Transform cameraTransform;      // Главная камера (VR)
    public CharacterController body;      // Тело персонажа
    public float minHeadDistance = 0.3f;  // Минимальное расстояние до стены
    public float pushbackSpeed = 5f;      // Скорость отталкивания

    void Update()
    {
        // Проверяем, не в стене ли камера (голова)
        if (Physics.CheckSphere(cameraTransform.position, minHeadDistance))
        {
            // Направление от камеры к телу
            Vector3 pushDir = (body.transform.position - cameraTransform.position).normalized;
            pushDir.y = 0; // Только горизонтально

            // Толкаем ТЕЛО, не камеру!
            body.Move(pushDir * pushbackSpeed * Time.deltaTime);
        }
    }
}