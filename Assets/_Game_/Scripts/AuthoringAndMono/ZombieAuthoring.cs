using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ZombieAuthoring : MonoBehaviour
{
    public ZombieSO data;
    public int[] spawnDataArr;
    public float timeDelaySpawn;
    public bool spawnInfinity;
    public bool allowRespawn;
    public int numberSpawn;
    public float2 spawnAmountRange;
    public float2 timeRange;
    public Transform pointRange1;
    public Transform pointRange2;
    public Transform pointDir1;
    public Transform pointDir2;
}


class ZombieBaker : Baker<ZombieAuthoring>
{
    public override void Bake(ZombieAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        float3 posMin = authoring.pointRange1.position;
        float3 posMax = authoring.pointRange2.position;
        float3 dirNormal = math.normalize(authoring.pointDir2.position - authoring.pointDir1.position);
        dirNormal.y = 0;
        AddComponent(entity,new ZombieProperty
        {
            directNormal = dirNormal,
            spawner = new ZombieSpawner
            {
                numberSpawn = authoring.numberSpawn,
                spawnAmountRange = authoring.spawnAmountRange ,
                timeRange = authoring.timeRange,
                spawnInfinity = authoring.spawnInfinity,
                allowRespawn = authoring.allowRespawn,
                timeDelay = authoring.timeDelaySpawn,
                posMax = posMax,
                posMin = posMin,
            }
        });
        AddBuffer<BufferZombieDie>(entity);
        var buffer = AddBuffer<BufferZombieStore>(entity);

        foreach (var spawn in authoring.spawnDataArr)
        {
            var zombie = GetZombieData_L(spawn);
            
            buffer.Add(new BufferZombieStore()
            {
                id = spawn,
                entity = GetEntity(zombie.prefab,TransformUsageFlags.Dynamic),
                hp = zombie.hp,
                speed = zombie.speed,
                damage = zombie.damage,
                attackRange = zombie.attackRange,
                delayAttack = zombie.delayAttack,
                chasingRange = zombie.chasingRange,
            });
        }

        Zombie GetZombieData_L(int id)
        {
            return Array.Find(authoring.data.zombies, x => x.id == id);
        }
        
    }
}
