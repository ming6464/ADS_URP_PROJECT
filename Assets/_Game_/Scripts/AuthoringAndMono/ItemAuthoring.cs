using System.Globalization;
using Unity.Entities;
using UnityEngine;

public class ItemAuthoring : MonoBehaviour
{
    public int count;
    public ItemType type;
    public Operation operation;
    public TypeUsing typeUsing;
    public int hp;
    public int id;
    public TextMesh textObj;
    public int idTextHp;
    public Transform[] spawnPoints;
    
    private void OnValidate()
    {
        if (textObj)
        {
            string str = "";
            switch (operation)
            {
                case Operation.Addition:
                    str = "+";
                    break;
                case Operation.Subtraction:
                    str = "-";
                    break;
                case Operation.Multiplication:
                    str = "x";
                    break;
                case Operation.Division:
                    str = ":";
                    break;
            }
            switch (type)
            {
                case ItemType.Character:
                    textObj.text = str  + count;
                    break;
                case ItemType.ObstacleTurret:
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

            var info = new ItemInfo()
            {
                id = authoring.id,
                type = authoring.type,
                count = authoring.count,
                hp = authoring.hp,
                operation = authoring.operation,
            };
            
            if (authoring.typeUsing.Equals(TypeUsing.canShooting))
            {
                AddComponent(entity, new ItemCanShoot()
                {
                    currentHp = authoring.hp,
                });
                info.idTextHp = authoring.idTextHp;
                AddComponent(entity,new ChangeTextNumberMesh()
                {
                    id = authoring.idTextHp,
                    value = authoring.hp,
                });
            }
            
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
            
            AddComponent(entity,info);
        }
    }
}

public enum TypeUsing
{
    none,
    canShooting
}

public enum Operation
{
    Addition, Subtraction, Multiplication, Division
}