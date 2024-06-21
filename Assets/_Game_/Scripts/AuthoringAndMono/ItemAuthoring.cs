using Unity.Entities;
using UnityEngine;

public class ItemAuthoring : MonoBehaviour
{
    public int count;
    public ItemType type;
    private class ItemAuthoringBaker : Baker<ItemAuthoring>
    {
        public override void Bake(ItemAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity,new ItemInfo()
            {
                type = authoring.type,
                count = authoring.count,
            });
        }
    }
}