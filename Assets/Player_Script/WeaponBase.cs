using UnityEngine;

/// 모든 무기의 베이스 클래스
/// 새 무기 만들 때 이 클래스를 상속받아 Attack()을 구현하면 됨
/// 
/// 외부에서 호출할 함수:
/// - Evolve() : 진화 (evo0→1→2), 조건 충족 시 호출
/// - OnLevelUp() : 레벨업 시 호출
/// - UpgradeSpeed/Damage/Range() 등 : 레벨업 옵션 선택 시 호출

public abstract class WeaponBase : MonoBehaviour
{
    [Header("Weapon Stat")]
    [SerializeField] protected WeaponStat dmgStat = new WeaponStat(10f);
    [SerializeField] protected WeaponStat spdStat = new WeaponStat(1f);

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

    /// 레벨업 시 외부에서 호출
    /// 기본: 데미지+3, 공속+10%
    /// 각 무기에서 오버라이드하여 추가 효과 구현 가능
    public virtual void OnLevelUp()
    {
        dmgStat.addValue += 3f;
        spdStat.multiValue += 0.1f;
    }
    
    /// 진화 시 외부에서 호출 (evo0→1→2 순서)
    /// evo2 이상이면 더 이상 진화 안 함
    public void Evolve()
    {
        if (evoLevel >= 2) return;
        evoLevel++;
        OnEvolve();
    }

    protected virtual void OnEvolve() { }
}