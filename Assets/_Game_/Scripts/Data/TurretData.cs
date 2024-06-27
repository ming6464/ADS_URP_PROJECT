﻿using Unity.Mathematics;
using UnityEngine;

namespace _Game_.Scripts.Data
{
    [CreateAssetMenu(menuName = "DataSO/TurretSO")]
    public class TurrentSO : Obstacle
    {
        public GameObject prefabs;
        public float3 pivotFireOffset;
        public int bulletPerShot;
        public float spaceAnglePerBullet;
        public bool parallelOrbit;
        public float speed;
        public float damage;
        public float cooldown;
        public float distanceSetChangeRota;
        public float moveToWardMax;
        public float moveToWardMin;
    }
}