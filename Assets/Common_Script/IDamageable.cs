using UnityEngine;
/// 데미지를 받을 수 있는 오브젝트에 구현하는 인터페이스
/// 적, 플레이어 등 피격 가능한 모든 오브젝트에 적용
/// 사용법: hit.GetComponent<IDamageable>()?.OnDamaged(damage, attackerPos);
public interface IDamageable
{
    /// <param name="damage">입힐 데미지량</param>
    /// <param name="attackerPos">공격자 위치 (넉백 방향 계산에 사용)</param>
    void OnDamaged(float damage, Vector2 attackerPos);
}