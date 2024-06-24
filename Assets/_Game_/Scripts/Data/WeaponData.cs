using System;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(menuName = "DataSO/WeaponSO")]
public class WeaponSO : ScriptableObject
{
    public Weapon[] weapons;
}
[Serializable]
public class Weapon
{
    public int id;
    public GameObject weaponPrefab;
    public float3 offset;
    public float damage;
    public float speed;
    public float cooldown;
    public int bulletPerShot;
    public float spaceAnglePerBullet;
    public bool parallelOrbit;
}