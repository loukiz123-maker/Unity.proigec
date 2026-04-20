using UnityEngine;

[CreateAssetMenu(fileName = "New Avatar", menuName = "Avatar System/Avatar Data")]
public class AvatarData : ScriptableObject
{
    public string avatarName;
    public string prefabResourceName; // Имя префаба в папке Resources
    public Sprite avatarIcon;        // Иконка для UI
}