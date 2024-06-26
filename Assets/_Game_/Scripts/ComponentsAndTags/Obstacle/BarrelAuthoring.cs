using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Game_.Scripts.ComponentsAndTags.Obstacle
{
    public class BarrelAuthoring : MonoBehaviour
    {
        public float3 pivotFireOffset;
        public int bulletPerShot;
        public float spaceAnglePerBullet;
        public bool parallelOrbit;
        public float speed;
        public float damage;
        public float cooldown;
        public float distanceSetChangeRota;
        public float moveToWardMax;
        public float moveToWardMin;
        private class TurretAuthoringBaker : Baker<BarrelAuthoring>
        {
            public override void Bake(BarrelAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity,new BarrelInfo()
                {
                    damage = authoring.damage,
                    distanceSetChangeRota = authoring.distanceSetChangeRota,
                    moveToWardMax = authoring.moveToWardMax,
                    moveToWardMin = authoring.moveToWardMin,
                    speed = authoring.speed,
                    cooldown = authoring.cooldown,
                    pivotFireOffset = authoring.pivotFireOffset,
                    bulletPerShot = authoring.bulletPerShot,
                    spaceAnglePerBullet = authoring.spaceAnglePerBullet,
                    parallelOrbit = authoring.parallelOrbit
                });
                AddComponent(entity,new BarrelRunTime()
                {
                    value = -authoring.cooldown,
                });
            }
        }
    }
}