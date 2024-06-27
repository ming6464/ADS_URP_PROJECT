using _Game_.Scripts.Data;
using Unity.Entities;
using UnityEngine;

namespace _Game_.Scripts.AuthoringAndMono
{
    public class ObstacleAuthoring : MonoBehaviour
    {
        public ObstacleSO obstacleSo;
        private class ObstacleAuthoringBaker : Baker<ObstacleAuthoring>
        {
            public override void Bake(ObstacleAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var buffer = AddBuffer<BufferTurretObstacle>(entity);
                var arr = authoring.obstacleSo.obstacles;
                foreach (var obs in arr)
                {
                    switch (obs.obstacle.type)
                    {
                        case ObstacleType.Turret:
                            var turret = (TurrentSO) obs.obstacle;
                            buffer.Add(new BufferTurretObstacle()
                            {
                                id = obs.id,
                                entity = GetEntity(turret.prefabs,TransformUsageFlags.None),
                                bulletPerShot = turret.bulletPerShot,
                                cooldown = turret.cooldown,
                                damage = turret.damage,
                                distanceSetChangeRota = turret.distanceSetChangeRota,
                                moveToWardMax = turret.moveToWardMax,
                                moveToWardMin = turret.moveToWardMin,
                                parallelOrbit = turret.parallelOrbit,
                                pivotFireOffset = turret.pivotFireOffset,
                                speed = turret.speed,
                                spaceAnglePerBullet = turret.spaceAnglePerBullet
                            });
                            break;
                    }
                    
                }

            }
        }
    }
}