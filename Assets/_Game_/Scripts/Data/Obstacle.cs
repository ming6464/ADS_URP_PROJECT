using System;
using UnityEngine;

namespace _Game_.Scripts.Data
{
    [CreateAssetMenu(menuName = "DataSO/ObstacleSO")]
    public class ObstacleSO : ScriptableObject
    {
        public Obstacle[] obstacles;
    }
    
    
    [Serializable]
    public struct Obstacle 
    {
        public int id;
        public ObstacleType type;
        public GameObject prefabs;
    }


    public enum ObstacleType
    {
        Turret
    }
}