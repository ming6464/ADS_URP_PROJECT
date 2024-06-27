using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ZombieAuthoring : MonoBehaviour
{
    public ZombieSO data;
    public SpawnData[] spawnDataArr;
    public float timeDelaySpawn;
    public bool spawnInfinity;
    public bool allowRespawn;
    public bool applyTotalCount;
    public int numberSpawn;
    public float2 numberSpawnPerFrameRange;
    public float timeSpawn;
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
            applyTotalCount = authoring.applyTotalCount,
            spawner = new ZombieSpawner
            {
                numberSpawn = authoring.numberSpawn,
                numberSpawnPerFrameRange = authoring.numberSpawnPerFrameRange,
                timeSpawn = authoring.timeSpawn,
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
            var zombie = GetZombieData_L(spawn.id);
            
            if(!authoring.applyTotalCount && spawn.count <= 0) continue;
            
            buffer.Add(new BufferZombieStore()
            {
                id = spawn.id,
                entity = GetEntity(zombie.prefab,TransformUsageFlags.Dynamic),
                hp = zombie.hp,
                speed = zombie.speed,
                damage = zombie.damage,
                attackRange = zombie.attackRange,
                delayAttack = zombie.delayAttack,
                numberSpawn = spawn.count,
                chasingRange = zombie.chasingRange,
            });
        }

        Zombie GetZombieData_L(int id)
        {
            return Array.Find(authoring.data.zombies, x => x.id == id);
        }
        
    }
}

[Serializable]
public struct SpawnData
{
    public int id;
    public int count;
}