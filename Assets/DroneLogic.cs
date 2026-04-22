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

    [Header("Связь с эффектами")]
    public GlitchOverlay glitchScript;

    private bool iSeePlayer = false; // Состояние: видит ли ЭТОТ дрон игрока прямо сейчас

    void Update()
    {
        // 1. Движение между точками
        Transform target = movingToB ? pointB : pointA;
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        if (target.position - transform.position != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(target.position - transform.position), 0.1f);

        if (Vector3.Distance(transform.position, target.position) < 0.2f)
            movingToB = !movingToB;

        // 2. Логика зрения (Raycast)
        RaycastHit hit;
        if (Physics.Raycast(droneLight.transform.position, droneLight.transform.forward, out hit, viewDistance))
        {
            if (hit.collider.CompareTag("Player"))
            {
                // Если мы только что заметили игрока (раньше не видели)
                if (!iSeePlayer)
                {
                    iSeePlayer = true;
                    droneLight.color = alertColor;
                    if (glitchScript != null) glitchScript.SetGlitch(true);
                    Debug.Log("Дрон " + gameObject.name + " заметил игрока!");
                }
            }
            else
            {
                // Если луч попал во что-то другое, но мы до этого видели игрока
                if (iSeePlayer) LostPlayer();
            }
        }
        else
        {
            // Если луч улетел в пустоту, но мы до этого видели игрока
            if (iSeePlayer) LostPlayer();
        }
    }

    // Метод для сброса состояния
    void LostPlayer()
    {
        iSeePlayer = false;
        droneLight.color = normalColor;
        if (glitchScript != null) glitchScript.SetGlitch(false);
        Debug.Log("Дрон " + gameObject.name + " потерял игрока.");
    }
}