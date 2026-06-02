using UnityEngine;

public interface IDamageable
{
    void OnDamaged(float damage, Vector2 attackerPos);
}