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
                var buffer = AddBuffer<BufferObstacle>(entity);
                for (int i = 0; i < authoring.obstacleSo.obstacles.Length; i++)
                {
                    var obs = authoring.obstacleSo.obstacles[i];
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