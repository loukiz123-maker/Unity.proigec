using UnityEngine;

public class HackingTrigger : MonoBehaviour
{
    public GameObject hackingCanvas; // Сюда перетащи свой Canvas терминала

    void Start()
    {
        hackingCanvas.SetActive(false); // В начале игры терминал скрыт
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        { // Убедись, что на твоем VR-игроке тег Player
            hackingCanvas.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            hackingCanvas.SetActive(false);
        }
    }
}