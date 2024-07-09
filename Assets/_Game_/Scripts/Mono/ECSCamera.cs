using Unity.Entities;
using UnityEngine;

public class ECSCamera : MonoBehaviour
{
    
    public Camera mainCamera;
    public float speedChangeCamera;
    public LayerMask characterLayer;
    
    private Transform _mainCameraTf;
    private Inputs _inputs;
    private bool _isAddEvent;
    private float _progressChangeCamera;
    private CameraType _curCameraType;

    private Vector3 _positionFirstPersonCamera = Vector3.zero;
    private Quaternion _rotationFirstPersonCamera = Quaternion.identity; 
    private Vector3 _positionThirstPersonCamera =  Vector3.zero;
    private Quaternion _rotationThirstPersonCamera = Quaternion.identity;

    private Vector3 _nextPosition;
    private Quaternion _nextRotation;

    private void Awake()
    {
        _mainCameraTf = mainCamera.GetComponent<Transform>();
        _curCameraType = CameraType.FirstPersonCamera;
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
            switch (_curCameraType)
            {
                case CameraType.FirstPersonCamera:
                    _curCameraType = CameraType.ThirstPersonCamera;
                    break;
                case CameraType.ThirstPersonCamera:
                    _curCameraType = CameraType.FirstPersonCamera;
                    break;
            }
            _progressChangeCamera = 0;
            SetUpCamera();
        };
        SetUpCamera();
        _progressChangeCamera = 1;
    }


    private void Update()
    {
        if (!_isAddEvent)
        {
            UpdateHybrid playerSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<UpdateHybrid>();
            if(playerSystem == null) return;
            playerSystem.UpdateCamera += UpdateCamera;
            _isAddEvent = true;
        }

        GetPosition();
        _mainCameraTf.position = _nextPosition;
        _mainCameraTf.rotation = _nextRotation;

    }

    private void UpdateCamera(Vector3 position,Quaternion rotation,CameraType type)
    {
        switch (type)
        {
            case CameraType.FirstPersonCamera:
                _positionFirstPersonCamera = position;
                _rotationFirstPersonCamera = rotation;
                break;
            case CameraType.ThirstPersonCamera:
                _positionThirstPersonCamera = position;
                _rotationThirstPersonCamera = rotation;
                break;
        }
    }

    private void SetUpCamera()
    {
        int layer = -1;
        if (_curCameraType.Equals(CameraType.FirstPersonCamera))
        {
            layer = ~ characterLayer;
        }

        mainCamera.cullingMask = layer;
    }

    private void GetPosition()
    {
        _progressChangeCamera = Mathf.Clamp(_progressChangeCamera + speedChangeCamera * Time.deltaTime,0,1);
        switch (_curCameraType)
        {
            case CameraType.ThirstPersonCamera:
                _nextPosition = Vector3.Lerp(_positionFirstPersonCamera, _positionThirstPersonCamera,
                    _progressChangeCamera);
                _nextRotation = Quaternion.Lerp(_rotationFirstPersonCamera,_rotationThirstPersonCamera,_progressChangeCamera);
                break;
            case CameraType.FirstPersonCamera:
                _nextPosition = Vector3.Lerp(_positionThirstPersonCamera, _positionFirstPersonCamera,
                    _progressChangeCamera);
                _nextRotation = Quaternion.Lerp(_rotationThirstPersonCamera,_rotationFirstPersonCamera,_progressChangeCamera);
                break;
        }
    }
}