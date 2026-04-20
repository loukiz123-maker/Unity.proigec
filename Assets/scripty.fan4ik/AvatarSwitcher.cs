using UnityEngine;
using Normal.Realtime;

public class AvatarSwitcher : MonoBehaviour
{
    public RealtimeAvatarManager avatarManager;
    public GameObject[] availableAvatars;

    public void SelectAvatar(int index)
    {
        if (index < 0 || index >= availableAvatars.Length) return;

        // Просто меняем префаб. 
        // НЕ удаляем текущий, НЕ перезаходим в комнату, НЕ вызываем спавн.
        avatarManager.localAvatarPrefab = availableAvatars[index];

        Debug.Log("Префаб для следующего спавна установлен: " + availableAvatars[index].name);
    }
}