using System;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class ECSCamera : MonoBehaviour
{
    public Transform cameraFirstPerson;
    public Transform cameraThirstPerson;

    private bool _isCamFirstPersonActive;
    private Inputs _inputs;
    private bool isAddEvent;


    private void Awake()
    {
        _isCamFirstPersonActive = true;
        if (cameraFirstPerson)
        {
            cameraFirstPerson.gameObject.SetActive(_isCamFirstPersonActive);
        }

        if (cameraThirstPerson)
        {
            cameraThirstPerson.gameObject.SetActive(!_isCamFirstPersonActive);
        }
        _inputs = new Inputs();
    }

    private void OnEnable()
    {
        _inputs.Enable();
    }

    private void OnDisable()
    {
        _inputs.Disable();
    }

    private void Start()
    {
        _inputs.Camera.ChangeCamera.performed += _ =>
        {
            _isCamFirstPersonActive = !_isCamFirstPersonActive;
            if (cameraFirstPerson)
            {
                cameraFirstPerson.gameObject.SetActive(_isCamFirstPersonActive);
            }

            if (cameraThirstPerson)
            {
                cameraThirstPerson.gameObject.SetActive(!_isCamFirstPersonActive);
            }

        };
    }


    private void Update()
    {
        if (!isAddEvent)
        {
            UpdateHybrid playerSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<UpdateHybrid>();
            if(playerSystem == null) return;
            Debug.Log("Hello0 ");
            playerSystem.UpdateCamera += UpdateCamera;
            isAddEvent = true;
        }
    }

    private void UpdateCamera(LocalToWorld ltw,bool isFirstPerson)
    {
        Debug.Log("Hello1 ");
        if(isFirstPerson != _isCamFirstPersonActive) return;
        Debug.Log("Hello2 ");
        if (isFirstPerson)
        {
            Debug.Log("Hello3 ");
            cameraFirstPerson.position = ltw.Position;
            cameraFirstPerson.rotation = ltw.Rotation;
        }
        else
        {
            Debug.Log("Hello4 ");
            cameraThirstPerson.position = ltw.Position;
        }
    }
}