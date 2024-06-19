using Unity.Entities;
using UnityEngine;

public class EffectAuthoring : MonoBehaviour
{
    public GameObject effHitFlashPrefab;
}


partial class EffectAuthoringBaker : Baker<EffectAuthoring>
{
    public override void Bake(EffectAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity,new EffectProperty()
        {
            effHitFlash = GetEntity(authoring.effHitFlashPrefab,TransformUsageFlags.None),
        });
    }
}