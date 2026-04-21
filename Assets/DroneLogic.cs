using UnityEngine;

public class DroneLogic : MonoBehaviour
{
    [Header("Настройки движения")]
    public Transform pointA;
    public Transform pointB;
    public float speed = 2f;
    private bool movingToB = true;

    [Header("Настройки зрения")]
    public Light droneLight;
    public float viewDistance = 10f;
    public Color normalColor = Color.white;
    public Color alertColor = Color.red;

    void Update()
    {
        // 1. Движение между точками
        Transform target = movingToB ? pointB : pointA;
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        // Поворот в сторону цели
        if (target.position - transform.position != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(target.position - transform.position), 0.1f);

        if (Vector3.Distance(transform.position, target.position) < 0.2f)
            movingToB = !movingToB;

        // 2. Логика зрения (Raycast)
        RaycastHit hit;
        // Пускаем луч из прожектора вперед
        if (Physics.Raycast(droneLight.transform.position, droneLight.transform.forward, out hit, viewDistance))
        {
            if (hit.collider.CompareTag("Player"))
            {
                droneLight.color = alertColor;
                Debug.Log("ИИ ЗАМЕТИЛ ТЕБЯ!");
                // Здесь можно добавить вызов монстра или замедление игрока
            }
            else
            {
                droneLight.color = normalColor;
            }
        }
    }
}