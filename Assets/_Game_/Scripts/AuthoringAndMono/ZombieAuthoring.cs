using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ZombieAuthoring : MonoBehaviour
{
    [Header("Spawn Zombie Normal")]
    public int[] zombieNormalIds;
    public SpawnRange[] spawnRanges;
    [Header("Spawn Zombie Boss")] 
    public SpawnBoss[] spawnBosses;
    [Header("Spawn Info")]
    public int totalNumber;
    public float cooldown;
    public float2 spawnAmountRange;
    public float2 timeRange;
    public bool spawnInfinity;
    public bool allowRespawn;
    public bool allowSpawnZombie;
    public bool allowSpawnBoss;
    [Header("General Properties")] 
    public float speedAvoid;
    public bool comparePriorities;
    public float minDistanceAvoid;
}


class ZombieBaker : Baker<ZombieAuthoring>
{
    public override void Bake(ZombieAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.None);
        AddBuffer<BufferZombieDie>(entity);
        AddComponent<ZombieSpawnRuntime>(entity);
        AddComponent(entity, new ZombieProperty
        {
            entity = entity,
            speedAvoid = authoring.speedAvoid,
            comparePriorities = authoring.comparePriorities,
            minDistanceAvoid = authoring.minDistanceAvoid,
        });
        AddComponent(entity, new ZombieSpawner
        {
            spawnInfinity = authoring.spawnInfinity,
            allowRespawn = authoring.allowRespawn,
            cooldown = authoring.cooldown,
            totalNumber = authoring.totalNumber,
            spawnAmountRange = authoring.spawnAmountRange,
            timeRange = authoring.timeRange,
            allowSpawnZombie = authoring.allowSpawnZombie,
            allowSpawnBoss = authoring.allowSpawnBoss,
        });
        
        // Add buffer zombie normal id spawn
        var bufferZombieNormalId = AddBuffer<BufferZombieNormalSpawnID>(entity);
        foreach (int id in authoring.zombieNormalIds)
        {
            bufferZombieNormalId.Add(new BufferZombieNormalSpawnID()
            {
                id = id,
            });
        }
        //
        
        // Add buffer zombie Spawn
        var bufferZombieSpawn = AddBuffer<BufferZombieSpawnRange>(entity);
        foreach (var spawnRange in authoring.spawnRanges)
        {
            float3 posMin = spawnRange.pointRange1.position;
            float3 posMax = spawnRange.pointRange2.position;
            float3 dirNormal = math.normalize(spawnRange.pointDir2.position - spawnRange.pointDir1.position);
            bufferZombieSpawn.Add(new BufferZombieSpawnRange()
            {
                posMax = posMax,
                posMin = posMin,
                directNormal = dirNormal,
            });
        }
        //
        
        // Add buffer zombie boss spawn
        var bufferZombieBossSpawn = AddBuffer<BufferZombieBossSpawn>(entity);
        foreach (var boss in authoring.spawnBosses)
        {
            float3 dirNormal = math.normalize(boss.pointDir2.position - boss.pointDir1.position);
            bufferZombieBossSpawn.Add(new BufferZombieBossSpawn()
            {
                id = boss.id,
                timeDelay = boss.timeDelay,
                directNormal = dirNormal,
                position = boss.spawnPos.position,
            });
        }
        //
    }
}

[Serializable]
public struct SpawnRange
{
    public Transform pointRange1;
    public Transform pointRange2;
    public Transform pointDir1;
    public Transform pointDir2;
}

[Serializable]
public struct SpawnBoss
{
    public int id;
    public float timeDelay;
    public Transform spawnPos;
    public Transform pointDir1;
    public Transform pointDir2;
}

