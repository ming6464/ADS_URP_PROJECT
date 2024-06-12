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
    public Transform Y;
    public Transform X;
}


class ZombieBaker : Baker<ZombieAuthoring>
{
    public override void Bake(ZombieAuthoring authoring)
    {

        Entity entity = GetEntity(TransformUsageFlags.Dynamic);

        Vector3 posX = authoring.X.position;
        Vector3 posY = authoring.Y.position;
        float3 posMin = new float3(posY.x, posX.y, posX.z);
        float3 posMax = new float3(posX.x, posY.y, posY.z);
        
        AddComponent(entity,new GenericZombieProperties
        {
            entity = GetEntity(authoring.zombiePrefab,TransformUsageFlags.Dynamic),
            speed = authoring.speed,
            targetPosition = authoring.target.position,
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