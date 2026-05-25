using UnityEngine;

public abstract class WeaponBase : MonoBehaviour
{
    [Header("Weapon Stat")]
    [SerializeField] protected WeaponStat dmgStat = new WeaponStat(10f);
    [SerializeField] protected WeaponStat spdStat = new WeaponStat(1f);
    [SerializeField] protected float knockback = 5f;

    public int evoLevel = 0;
    private float _attackTimer;

    public float CurrentDamage => dmgStat.FinalValue;
    public float CurrentAttackSpeed => spdStat.FinalValue;

    protected virtual void Update()
    {
        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0f)
        {
            Attack();
            _attackTimer = 1f / CurrentAttackSpeed;
        }
    }

    protected abstract void Attack();

    public virtual void OnLevelUp()
    {
        dmgStat.addValue += 3f;
        spdStat.multiValue += 0.1f;
        Debug.Log($"Level Up! 데미지: {CurrentDamage}, 공속: {CurrentAttackSpeed}");
    }

    public void Evolve()
    {
        if (evoLevel >= 2) return;
        evoLevel++;
        OnEvolve();
    }

    protected virtual void OnEvolve() { }
}