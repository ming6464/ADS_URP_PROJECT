using Unity.Entities;
using UnityEngine;

public class ParticleEffect : MonoBehaviour
{
    public ParticleSystem hitFlashEff;
    private bool _isAddEvent;
    private Transform _hitFlashTf;

    private void Awake()
    {
        _hitFlashTf = hitFlashEff.GetComponent<Transform>();
    }

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

    private void UpdateHitFlashEff(Vector3 position,Quaternion rotation)
    {
        if(!_hitFlashTf) return;
        _hitFlashTf.position = position;
        _hitFlashTf.rotation = rotation;
        hitFlashEff.Emit(1);
    }
}