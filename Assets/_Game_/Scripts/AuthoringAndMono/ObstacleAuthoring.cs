using _Game_.Scripts.Data;
using Unity.Entities;
using UnityEngine;

namespace _Game_.Scripts.AuthoringAndMono
{
    public class ObstacleAuthoring : MonoBehaviour
    {
        public ObstacleSO obstacleSo;
        public bool check;
        
        private class ObstacleAuthoringBaker : Baker<ObstacleAuthoring>
        {
            public override void Bake(ObstacleAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var buffer = AddBuffer<BufferObstacle>(entity);
                var arr = authoring.obstacleSo.obstacles;
                foreach (var obs in arr)
                {
                    buffer.Add(new BufferObstacle()
                    {
                        id = obs.id,
                        entity = GetEntity(obs.prefabs,TransformUsageFlags.None),
                    });
                }

            }
        }
    }
}