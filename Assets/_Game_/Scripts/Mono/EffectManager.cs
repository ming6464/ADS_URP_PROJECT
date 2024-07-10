using System;
using Unity.Entities;
using UnityEngine;

public class EffectManager : MonoBehaviour
{
    [Serializable]
    public class EffectInfo
    {
        public ParticleSystem eff;
        public EffectID effectID;
        public Transform _tf;

        public void Play(Vector3 position, Quaternion rotation)
        {
            if(!eff) return;
            if (!_tf)
            {
                _tf = eff.GetComponent<Transform>();
            }

            _tf.position = position;
            _tf.rotation = rotation;
            eff.Emit(1);
        }
        
    }
    
    public EffectInfo[] effectInfos;
    private bool _isAddEvent;
    
    private void Update()
    {
        if (!_isAddEvent)
        {
            UpdateHybrid playerSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<UpdateHybrid>();
            if(playerSystem == null) return;
            _isAddEvent = true;
            playerSystem.UpdateHitFlashEff += UpdateHitFlashEff;
        }
    }

    private void UpdateHitFlashEff(Vector3 position,Quaternion rotation,EffectID effectID)
    {
        foreach (var i in effectInfos)
        {
            if (i.effectID == effectID)
            {
                if(!i.eff) return;
                if (!i._tf)
                {
                    i._tf = i.eff.GetComponent<Transform>();
                }

                i._tf.position = position;
                i._tf.rotation = rotation;
                i.eff.Emit(1);
            }
        }
    }
    
    
    
}