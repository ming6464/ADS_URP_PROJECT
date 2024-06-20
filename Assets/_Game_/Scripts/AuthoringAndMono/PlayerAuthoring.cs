using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    public Transform spawnPosition;
    public GameObject playerPrefab;
    public float speed;
    //
    public int numberSpawn;
    public float2 spaceGrid;
    public int countOfCol;
}

class AuthoringBaker : Baker<PlayerAuthoring>
{
    public override void Bake(PlayerAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.None);

        AddComponent(entity, new PlayerProperty
        {
            entity = GetEntity(authoring.playerPrefab, TransformUsageFlags.Dynamic),
            speed = authoring.speed,
            spawnPosition = authoring.spawnPosition.position,
            spaceGrid = authoring.spaceGrid,
            countOfCol = authoring.countOfCol,
            numberSpawn = authoring.numberSpawn,
        });
        
        AddComponent(entity, new PlayerMoveInput
        {
            directMove = new float2(0, 0),
            shot = false,
            mousePos = new float2(0, 0),
        });
    }
}