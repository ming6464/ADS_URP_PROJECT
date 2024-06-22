using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Game_.Scripts.AuthoringAndMono
{
    public class SupportAuthoring : MonoBehaviour
    {
        [Header("Layer")] 
        public LayerMask playerLayer;
        public LayerMask characterLayer;
        public LayerMask zombieLayer;
        public LayerMask bulletLayer;
        public LayerMask itemLayer;

        [Header("Camera")] 
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
                
                AddComponent(entity,new LayerStoreComponent()
                {
                    playerLayer = (uint)authoring.playerLayer.value,
                    characterLayer = (uint)authoring.characterLayer.value,
                    zombieLayer = (uint)authoring.zombieLayer.value,
                    bulletLayer = (uint)authoring.bulletLayer.value,
                    itemLayer = (uint)authoring.itemLayer.value,
                });
            }
        }
    }
}



