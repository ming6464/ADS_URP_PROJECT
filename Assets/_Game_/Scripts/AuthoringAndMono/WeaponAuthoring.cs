using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class WeaponAuthoring : MonoBehaviour
{
    public bool shootAuto;
    //
    public GameObject bulletPrefab;
    public float lengthRay;
    public float timeLife;

    public float2 damageRangeRatio;
    public float2 timeRange;

}

class WeaponBaker : Baker<WeaponAuthoring>
{
    public override void Bake(WeaponAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity,new WeaponProperty()
        {
            shootAuto = authoring.shootAuto,
            entityBullet = GetEntity(authoring.bulletPrefab,TransformUsageFlags.Dynamic),
            length = authoring.lengthRay,
            timeLife = authoring.timeLife,
            damageRangeRatio = authoring.damageRangeRatio,
            timeRange = authoring.timeRange,
        });

        AddBuffer<BufferBulletDisable>(entity);
        AddBuffer<BufferBulletSpawner>(entity);
    }
}

