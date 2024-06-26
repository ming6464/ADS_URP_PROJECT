using Unity.Entities;
using UnityEngine;

public class ItemAuthoring : MonoBehaviour
{
    public int count;
    public ItemType type;
    public int id;
    public TextMesh textObj;
    public Transform[] spawnPoints;
    
    private void OnValidate()
    {
        if (textObj)
        {
            string str;
            switch (type)
            {
                case ItemType.Character:
                    str = "+";
                    if (count < 0) str = "-";
                    textObj.text = str + " " + count;
                    break;
                default:
                    textObj.text = $"ID : {id}";
                    break;
            }
        }
    }

    private class ItemAuthoringBaker : Baker<ItemAuthoring>
    {
        public override void Bake(ItemAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity,new ItemInfo()
            {
                id = authoring.id,
                type = authoring.type,
                count = authoring.count,
            });
            if (authoring.spawnPoints.Length > 0)
            {
                var buffer = AddBuffer<BufferSpawnPoint>(entity);

                foreach (var pointTf in authoring.spawnPoints)
                {
                    buffer.Add(new BufferSpawnPoint()
                    {
                        value = pointTf.position,
                    });
                }
            }
        }
    }
}