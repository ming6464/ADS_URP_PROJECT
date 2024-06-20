using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Game_.Scripts.AuthoringAndMono
{
    public class SupportAuthoring : MonoBehaviour
    {
        public float3 offsetFirstPerson; 
        public float3 offsetRotationFirstPerson; 
        public float3 offsetThirstPerson; 
        public float3 offsetRotationThirstPerson; 
        private class SupportAuthoringBaker : Baker<SupportAuthoring>
        {
            public override void Bake(SupportAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity,new CameraProperty()
                {
                    offsetCamFirst = authoring.offsetFirstPerson,
                    offsetRotationCamFirst = MathExt.Float3ToQuaternion(authoring.offsetRotationFirstPerson),
                    offsetCamThirst = authoring.offsetThirstPerson,
                    offsetRotationCamThirst = MathExt.Float3ToQuaternion(authoring.offsetRotationThirstPerson)
                });
            }
        }
    }
}



