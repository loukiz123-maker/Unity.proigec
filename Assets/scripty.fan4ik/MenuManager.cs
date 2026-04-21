using UnityEngine;

public class MenuManager : MonoBehaviour
{
    public GameObject avatarPanel; // Перетащите сюда вашу панель в инспекторе

    // Метод для закрытия/открытия панели
    public void TogglePanel()
    {
        avatarPanel.SetActive(!avatarPanel.activeSelf);
    }
}