using UnityEngine;

[System.Serializable]
public struct WeaponStat
{
    public float baseValue;
    public float addValue;    // 덧셈 (+5 공격력같은거)
    public float multiValue;  // 곱셈 (10% 증가같은거)

    public float FinalValue => (baseValue + addValue) * (1 + multiValue);

    public WeaponStat(float @base)
    {
        baseValue = @base;
        addValue = 0;
        multiValue = 0;
    }
}