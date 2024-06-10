using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

public class MovermentManagerJOB : Singleton<MovermentManagerJOB>
{
    [SerializeField,Range(1,10000)] private int _totalAgent = 1000;
    [SerializeField] private float _speed = 1f;

    public Vector3 Direct = Vector3.forward;
    
    public Transform[] _agentTransform;
    private TransformAccessArray _transformAccessArray;
    private JobHandle jobHandle;
    private TransformMoveJOB _transformMoveJob;
    private int _indexMax = -1;
    private List<Transform> _agentAddList;
    private List<Transform> _agentRemoveList;
    
    
    private void Awake()
    {
        HandleList();
        UpdateTransformAccess();
        this.DontDestroyOnLoad();
    }

    private void HandleList()
    {
        _agentTransform = new Transform[_totalAgent];
        _agentAddList = new List<Transform>();
        _agentRemoveList = new List<Transform>();
    }

    private void Update()
    {
        jobHandle.Complete();

        if (_agentAddList.Count > 0 || _agentRemoveList.Count > 0)
        {
            Transform transSet;
            for (int i = 0; i < _agentTransform.Length; i++)
            {
                if (_agentAddList.Count == 0 && _agentRemoveList.Count == 0) return;
                transSet = _agentTransform[i];
                if (!transSet)
                {
                    if (_agentAddList.Count == 0) continue;
                    _agentTransform[i] = _agentAddList[0];
                    _agentAddList.RemoveAt(0);
                    if (i > _indexMax)
                    {
                        _indexMax = i;
                    }
                }
                else if (_agentRemoveList.Contains(transSet))
                {
                    _agentTransform[i] = null;
                    _agentRemoveList.Remove(transSet);
                    if (i >= _indexMax)
                    {
                        _indexMax = i - 1;
                    }
                }
            }

            UpdateTransformAccess();
        }
        
        _transformMoveJob.Direct = this.Direct;
        _transformMoveJob.DeltaTime = Time.deltaTime;
        _transformMoveJob.Speed = _speed;
        _transformMoveJob.IndexMax = _indexMax;
        jobHandle = _transformMoveJob.Schedule(_transformAccessArray);
    }

    private void UpdateTransformAccess()
    {
        try
        {
            _transformAccessArray.Dispose();
        }
        catch
        {
            //ignored
        }
        _transformAccessArray = new TransformAccessArray(_agentTransform);
        _indexMax = _agentTransform.Length - 1;
    }

    private void OnDisable()
    {
        try
        {
            _transformAccessArray.Dispose();
        }
        catch
        {
            //ignored
        }
    }

    #region PUBLIC METHOD

    public void AddAgent(Transform trans)
    {
        if (_agentRemoveList.Contains(trans))
        {
            _agentRemoveList.Remove(trans);
            return;
        }
        _agentAddList.Add(trans);
    }

    public void RemoveAgent(Transform trans)
    {
        if (_agentAddList.Contains(trans))
        {
            _agentAddList.Remove(trans);
            return;
        }
        _agentRemoveList.Add(trans);
    }

    #endregion
    

    [BurstCompile]
    public struct TransformMoveJOB : IJobParallelForTransform
    {
        [ReadOnly] public float Speed;
        [ReadOnly] public float DeltaTime;
        [ReadOnly] public Vector3 Direct;
        [ReadOnly] public int IndexMax;
        public void Execute(int index, TransformAccess transform)
        {
            if(index > IndexMax) return;
            transform.position += Direct * (Speed * DeltaTime);
        }
    }
    
}       
