using UnityEngine;
using Normal.Realtime;

// using Normal.Realtime; ← ЗАКОММЕНТИРУЙТЕ

public class AvatarSwitcher : MonoBehaviour
{
    // public RealtimeAvatarManager avatarManager; ← ЗАКОММЕНТИРУЙТЕ
    public GameObject[] availableAvatars;

    public void SelectAvatar(int index)
    {
        // if (avatarManager == null) return; ← ЗАКОММЕНТИРУЙТЕ

        if (index < 0 || index >= availableAvatars.Length) return;

        Debug.Log("Аватар выбран: " + availableAvatars[index].name);
    }
}
