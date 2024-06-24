using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    
    public Transform spawnPosition;
    public GameObject playerPrefab;
    public float speed;
    public float radius;
    //
    public bool aimNearestEnemy;
    public float moveToWard;
    public int numberSpawnDefault;
    public float2 spaceGrid;
    public int countOfCol;
    //
    public int idWeaponDefault;
}

class AuthoringBaker : Baker<PlayerAuthoring>
{
    public override void Bake(PlayerAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.None);

        AddComponent(entity, new PlayerProperty
        {
            characterEntity = GetEntity(authoring.playerPrefab, TransformUsageFlags.Dynamic),
            speed = authoring.speed,
            spawnPosition = authoring.spawnPosition.position,
            spaceGrid = authoring.spaceGrid,
            countOfCol = authoring.countOfCol,
            numberSpawnDefault = authoring.numberSpawnDefault,
            aimNearestEnemy = authoring.aimNearestEnemy,
            moveToWard = authoring.moveToWard,
            characterRadius = authoring.radius,
            idWeaponDefault = authoring.idWeaponDefault,
        });
        
        AddComponent(entity, new PlayerInput
        {
            directMove = new float2(0, 0),
            pullTrigger = false,
            mousePos = new float2(0, 0),
        });
    }
}