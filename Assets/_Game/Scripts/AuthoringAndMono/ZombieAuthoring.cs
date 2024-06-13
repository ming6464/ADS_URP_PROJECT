using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ZombieAuthoring : MonoBehaviour
{
    public GameObject zombiePrefab;
    public float speed;
    public Transform target;
    public float timeDelaySpawn;
    public byte spawnInfinity;
    public int numberSpawn;
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
        AddComponent(entity,new GenericZombieProperties
        {
            entity = GetEntity(authoring.zombiePrefab,TransformUsageFlags.Dynamic),
            speed = authoring.speed,
            targetPosition = authoring.target.position,
            directNormal = dirNormal,
            spawner = new ZombieSpawner
            {
                numberSpawn = authoring.numberSpawn,
                spawnInfinity = authoring.spawnInfinity,
                timeDelay = authoring.timeDelaySpawn,
                posMax = posMax,
                posMin = posMin,
            }
        });
    }
}