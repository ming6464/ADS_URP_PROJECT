
using System;
using UnityEngine;


[CreateAssetMenu(menuName = "DataSO/ZombieSO")]
public class ZombieSO : ScriptableObject
{
    public Zombie[] zombies;
}

[Serializable]
public struct Zombie
{
    public int id;
    public GameObject prefab;
    public float hp;
    public float speed;
    public float damage;
    public float attackRange;
    public float delayAttack;
}