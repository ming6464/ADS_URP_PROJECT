using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class WeaponAuthoring : MonoBehaviour
{
    public GameObject weaponPrefab;
    public GameObject bulletPrefab;
    public float3 offset;
    public float damage;
    public float speed;
    public float cooldown;
    public float lengthRay;
    public int bulletPerShot;
    public float spaceAnglePerBullet;
    public float expired;
}

class WeaponBaker : Baker<WeaponAuthoring>
{
    public override void Bake(WeaponAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity,new WeaponProperties()
        {
            bulletDamage = authoring.damage,
            entityWeapon = GetEntity(authoring.weaponPrefab,TransformUsageFlags.Dynamic),
            entityBullet = GetEntity(authoring.bulletPrefab,TransformUsageFlags.Dynamic),
            bulletSpeed = authoring.speed,
            offset = authoring.offset,
            length = authoring.lengthRay,
            bulletPerShot = authoring.bulletPerShot,
            spaceAngleAnyBullet = authoring.spaceAnglePerBullet,
            expired = authoring.expired,
        });
        
        AddComponent(entity,new WeaponRunTime()
        {
            cooldown = authoring.cooldown,
        });
    }
}

