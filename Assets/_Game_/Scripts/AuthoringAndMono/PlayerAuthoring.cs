using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    public Transform spawnPosition;
    public GameObject playerPrefab;
    public float speed;
    public float radius;
    public float rotaAngleMax;
    public AimType aimType;
    //
    public float speedMoveToNextPoint;
    //
    public bool aimNearestEnemy;
    public float distanceAim;
    public float moveToWardMax;
    public float moveToWardMin;
    public int numberSpawnDefault;
    public float2 spaceGrid;
    //
    public int idWeaponDefault;
    
    
    
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
                numberSpawnDefault = authoring.numberSpawnDefault,
                aimType = authoring.aimType,
                characterRadius = authoring.radius,
                idWeaponDefault = authoring.idWeaponDefault,
                distanceAim = authoring.distanceAim,
                moveToWardMin = authoring.moveToWardMin,
                moveToWardMax = authoring.moveToWardMax,
                speedMoveToNextPoint = authoring.speedMoveToNextPoint,
                rotaAngleMax = authoring.rotaAngleMax,
            });
        
            AddComponent(entity, new PlayerInput
            {
                directMove = new float2(0, 0),
                pullTrigger = false,
                mousePos = new float2(0, 0),
            });

            
        }
    }

}

