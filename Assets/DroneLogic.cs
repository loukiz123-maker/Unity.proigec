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
    public float beamWidth = 1.5f; // Ширина луча (как у прожектора)
    public Color normalColor = Color.white;
    public Color alertColor = Color.red;

    [Header("Связь с эффектами")]
    public GlitchOverlay glitchScript;

    private bool iSeePlayer = false;

    void Update()
    {
        // 1. Движение
        MoveDrone();

        // 2. Логика зрения (используем SphereCast для объема)
        ScanForPlayer();
    }

    void MoveDrone()
    {
        Transform target = movingToB ? pointB : pointA;
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        if (target.position - transform.position != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(target.position - transform.position), 0.1f);

        if (Vector3.Distance(transform.position, target.position) < 0.2f)
            movingToB = !movingToB;
    }

    void ScanForPlayer()
    {
        RaycastHit hit;
        // SphereCast пускает "шар" по направлению света, что имитирует широкий луч
        if (Physics.SphereCast(droneLight.transform.position, beamWidth, droneLight.transform.forward, out hit, viewDistance))
        {
            if (hit.collider.CompareTag("Player"))
            {
                if (!iSeePlayer)
                {
                    DetectedPlayer();
                }
            }
            else
            {
                if (iSeePlayer) LostPlayer();
            }
        }
        else
        {
            if (iSeePlayer) LostPlayer();
        }

        // РИСУЕМ ЛУЧ в окне Scene для отладки
        Debug.DrawRay(droneLight.transform.position, droneLight.transform.forward * viewDistance, iSeePlayer ? Color.red : Color.green);
    }

    void DetectedPlayer()
    {
        iSeePlayer = true;
        droneLight.color = alertColor;
        if (glitchScript != null) glitchScript.SetGlitch(true);
        Debug.Log("<color=red>ВНИМАНИЕ:</color> Игрок в зоне луча!");
    }

    void LostPlayer()
    {
        iSeePlayer = false;
        droneLight.color = normalColor;
        if (glitchScript != null) glitchScript.SetGlitch(false);
        Debug.Log("<color=green>БЕЗОПАСНО:</color> Игрок скрылся.");
    }
}