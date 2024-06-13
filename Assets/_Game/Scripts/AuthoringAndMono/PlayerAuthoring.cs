using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    public GameObject playerPrefab;
    public float speed;

    [Header("Bullet")] 
    public GameObject bulletPrefab;
    public float speedBullet;
    public float damage;
    public float cooldown;
}

class AuthoringBaker : Baker<PlayerAuthoring>
{
    public override void Bake(PlayerAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.None);

        AddComponent(entity, new PlayerProperty
        {
            entity = GetEntity(authoring.playerPrefab, TransformUsageFlags.Dynamic),
            speed = authoring.speed,
        });
        
        AddComponent(entity, new PlayerMoveInput
        {
            directMove = new float2(0, 0),
            shot = false,
            mousePos = new float2(0, 0),
        });
        
        AddComponent(entity,new BulletProperty()
        {
            entity = GetEntity(authoring.bulletPrefab,TransformUsageFlags.Dynamic),
            speed = authoring.speedBullet,
            damage = authoring.damage,
        });
        
        AddComponent(entity,new BulletRunTime()
        {
            cooldown = authoring.cooldown,
            timeLatest = -authoring.cooldown,
        });
        
    }
}