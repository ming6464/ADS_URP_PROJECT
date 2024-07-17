using System;
using _Game_.Scripts.Data;
using Unity.Entities;
using UnityEngine;

namespace _Game_.Scripts.AuthoringAndMono
{
    public class DataAuthoring : MonoBehaviour
    {
        public WeaponSO weaponSo;
        public ZombieSO zombieSo;
        public ObstacleSO obstacleSo;
        private class DataAuthoringBaker : Baker<DataAuthoring>
        {
            public override void Bake(DataAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent<DataProperty>(entity);
                var weaponBuffer = AddBuffer<BufferWeaponStore>(entity);
                var zombieBuffer = AddBuffer<BufferZombieStore>(entity);
                var obstacleBuffer = AddBuffer<BufferTurretObstacle>(entity);
                
                // add Buffer weapon
                foreach (var weapon in authoring.weaponSo.weapons)
                {
                    weaponBuffer.Add(new BufferWeaponStore()
                    {
                        id = weapon.id,
                        entity = GetEntity(weapon.weaponPrefab,TransformUsageFlags.None),
                        offset = weapon.offset,
                        damage = weapon.damage,
                        speed = weapon.speed,
                        cooldown = weapon.cooldown,
                        bulletPerShot = weapon.bulletPerShot,
                        spaceAnglePerBullet = weapon.spaceAnglePerBullet,
                        parallelOrbit = weapon.parallelOrbit,
                        spacePerBullet = weapon.spacePerBullet,
                    });
                }
                //

                // Add buffer zombie
                foreach (var zombie in authoring.zombieSo.zombies)
                {
                    zombieBuffer.Add(new BufferZombieStore()
                    {
                        priorityKey = zombie.priorityKey,
                        id = zombie.id,
                        entity = GetEntity(zombie.prefab,TransformUsageFlags.Dynamic),
                        hp = zombie.hp,
                        radius = zombie.radius,
                        speed = zombie.speed,
                        damage = zombie.damage,
                        attackRange = zombie.attackRange,
                        delayAttack = zombie.delayAttack,
                        chasingRange = zombie.chasingRange,
                        radiusDamage = zombie.radiusDamage,
                        offsetAttackPosition = zombie.offsetAttackPosition,
                    });
                }
                //
                
                // Add buffer obstacle
                foreach (var obs in authoring.obstacleSo.obstacles)
                {
                    switch (obs.obstacle.type)
                    {
                        case ObstacleType.Turret:
                            var turret = (TurrentSO) obs.obstacle;
                            obstacleBuffer.Add(new BufferTurretObstacle()
                            {
                                id = obs.id,
                                entity = GetEntity(turret.prefabs,TransformUsageFlags.None),
                                bulletPerShot = turret.bulletPerShot,
                                cooldown = turret.cooldown,
                                damage = turret.damage,
                                distanceAim = turret.distanceAim,
                                moveToWardMax = turret.moveToWardMax,
                                moveToWardMin = turret.moveToWardMin,
                                parallelOrbit = turret.parallelOrbit,
                                pivotFireOffset = turret.pivotFireOffset,
                                speed = turret.speed,
                                spaceAnglePerBullet = turret.spaceAnglePerBullet,
                                spacePerBullet = turret.spacePerBullet,
                                timeLife = turret.timeLife,
                            });
                            break;
                    }
                }
                //
            }
        }
    }
}
