using Unity.Entities;
using UnityEngine;

public class WeaponAuthoring : MonoBehaviour
{
    public bool shootAuto;
    [Space(10)] 
    public WeaponSO data;

    public GameObject bulletPrefab;
    public float lengthRay;
    public float expired;
}

class WeaponBaker : Baker<WeaponAuthoring>
{
    public override void Bake(WeaponAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity,new WeaponProperties()
        {
            shootAuto = authoring.shootAuto,
            entityBullet = GetEntity(authoring.bulletPrefab,TransformUsageFlags.Dynamic),
            length = authoring.lengthRay,
            expired = authoring.expired,
        });

        AddBuffer<BufferBulletDisable>(entity);
        DynamicBuffer<BufferWeaponStore> weaponStoresBuffer = AddBuffer<BufferWeaponStore>(entity);
        foreach (var weapon in authoring.data.weapons)
        {
            weaponStoresBuffer.Add(new BufferWeaponStore()
            {
                id = weapon.id,
                entity = GetEntity(weapon.weaponPrefab,TransformUsageFlags.Dynamic),
                offset = weapon.offset,
                damage = weapon.damage,
                speed = weapon.speed,
                cooldown = weapon.cooldown,
                bulletPerShot = weapon.bulletPerShot,
                spaceAnglePerBullet = weapon.spaceAnglePerBullet,
                parallelOrbit = weapon.parallelOrbit,
            });
        }
    }
}

