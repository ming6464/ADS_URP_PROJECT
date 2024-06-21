using System;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class ParticleEffect : MonoBehaviour
{
    public ParticleSystem hitFlashEff;
    private bool isAddEvent;
    private Transform hitFlashTf;

    private void Awake()
    {
        hitFlashTf = hitFlashEff.GetComponent<Transform>();
    }

    private void Update()
    {
        // hitFlashEff.Emit(1);
        if (!isAddEvent)
        {
            return;
            UpdateHybrid playerSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<UpdateHybrid>();
            if(playerSystem == null) return;
            isAddEvent = true;
            playerSystem.UpdateHitFlashEff += UpdateHitFlashEff;
        }
    }

    private void UpdateHitFlashEff(LocalToWorld ltw)
    {
        Debug.Log("Play Eff1");
        if(!hitFlashTf) return;
        Debug.Log("Play Eff2");
        hitFlashTf.position = ltw.Position;
        hitFlashTf.rotation = ltw.Rotation;
        hitFlashEff.Emit(1);
    }
}