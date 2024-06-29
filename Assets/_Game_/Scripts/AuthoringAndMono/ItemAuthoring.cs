using Unity.Entities;
using UnityEngine;

public class ItemAuthoring : MonoBehaviour
{
    public int count;
    public ItemType type;
    public TypeUsing typeUsing;
    public float hp;
    public int id;
    public TextMesh textObj;
    public TextMesh textHp;
    public Transform[] spawnPoints;
    
    private void OnValidate()
    {
        if (textObj)
        {
            string str;
            switch (type)
            {
                case ItemType.Character:
                    str = "";
                    if (count > 0) str = "+";
                    textObj.text = str + " " + count;
                    break;
                default:
                    textObj.text = $"ID : {id}";
                    break;
            }
        }

        if (textHp)
        {
            textHp.text = hp.ToString();
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
                hp = authoring.hp,
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

            if (authoring.typeUsing.Equals(TypeUsing.canShooting))
            {
                AddComponent<ItemCanShoot>(entity);
                
            }
        }
    }
}

public enum TypeUsing
{
    none,
    canShooting
}