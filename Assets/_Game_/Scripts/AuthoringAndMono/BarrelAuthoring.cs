using Unity.Entities;
using UnityEngine;

namespace _Game_.Scripts.ComponentsAndTags.Obstacle
{
    public class BarrelAuthoring : MonoBehaviour
    {
        public int id;
        private class TurretAuthoringBaker : Baker<BarrelAuthoring>
        {
            public override void Bake(BarrelAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new BarrelCanSetup()
                {
                    id = authoring.id,
                });
            }
        }
    }
}