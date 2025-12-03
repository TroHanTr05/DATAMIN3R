using UnityEngine;

[CreateAssetMenu(menuName = "Items/Item", fileName = "NewItem")]
public class Item : ScriptableObject
{
    public string id;
    public string displayName;
    public Sprite icon;
}