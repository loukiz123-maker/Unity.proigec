using UnityEngine;
using UnityEngine.AI;

public class MechAI : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform player;

    [Header("Distance Mechanics")]
    public float maxDistance = 1f; // Если игрок отошел дальше, мех прыгает вперед
    public float spawnInFrontDistance = 4f; // На каком расстоянии ПЕРЕД лицом он появится

    private NavMeshAgent agent;
    private Animator animator;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (player == null || agent == null) return;

        // Постоянная погоня
        agent.SetDestination(player.position);

        if (animator != null)
            animator.SetFloat("Speed", agent.velocity.magnitude);

        // Проверка дистанции
        float currentDistance = Vector3.Distance(transform.position, player.position);

        if (currentDistance > maxDistance)
        {
            TeleportInFront();
        }
    }

    void TeleportInFront()
    {
        // Рассчитываем точку ПЕРЕД игроком (используем player.forward)
        Vector3 targetPos = player.position + (player.forward * spawnInFrontDistance);

        NavMeshHit hit;
        // Проверяем наличие пола (NavMesh) в этой точке
        if (NavMesh.SamplePosition(targetPos, out hit, 5f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);

            // Поворачиваем меха лицом к игроку сразу после прыжка
            transform.LookAt(new Vector3(player.position.x, transform.position.y, player.position.z));

            Debug.Log("<color=red>Сингулярность:</color> Куда это ты собрался?");
        }
    }
}