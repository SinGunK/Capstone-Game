using UnityEngine;

public class EnemyController : MonoBehaviour, IDamageable, IStunnable, ISlowable
{
    [Header("Spec")]
    [SerializeField] float moveSpeed = 3.5f;
    [SerializeField] float maxHealth = 30f;

    [Header("Knockback")]
    [SerializeField] float airResistance = 5f;
    [SerializeField] float knockbackForce = 7f;

    Rigidbody2D _rb;
    float _currentHp;
    float _hitStopTimer;
    float _stunTimer;
    float _slowTimer;
    float _slowAmount = 1f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _currentHp = maxHealth;
    }

    void FixedUpdate()
    {
        if (_stunTimer > 0)
        {
            _stunTimer -= Time.fixedDeltaTime;
            ApplyFriction();
            return;
        }
        if (_hitStopTimer > 0)
        {
            _hitStopTimer -= Time.fixedDeltaTime;
            ApplyFriction();
            return;
        }
        if (_slowTimer > 0)
        {
            _slowTimer -= Time.fixedDeltaTime;
            if (_slowTimer <= 0) _slowAmount = 1f;
        }
        MoveToPlayer();
    }

    void MoveToPlayer()
    {
        if (GameContext.PlayerTransform == null) return;
        Vector2 dir = (GameContext.PlayerTransform.position - transform.position).normalized;
        _rb.linearVelocity = dir * moveSpeed * _slowAmount;
    }

    public void OnDamaged(float damage, Vector2 attackerPos)
    {
        _currentHp -= damage;
        _hitStopTimer = 0.15f;
        Vector2 pushDir = ((Vector2)transform.position - attackerPos).normalized;
        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(pushDir * knockbackForce, ForceMode2D.Impulse);
        if (_currentHp <= 0) Die();
    }

    public void ApplyStun(float duration)
    {
        if (duration > _stunTimer) _stunTimer = duration;
    }

    public void ApplySlow(float amount, float duration)
    {
        _slowAmount = amount;
        _slowTimer = duration;
        Debug.Log($"슬로우, 속도 {amount * 100}%");
    }

    void ApplyFriction()
    {
        _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, Vector2.zero,
            Time.fixedDeltaTime * airResistance);
    }

    void Die()
    {
        Destroy(gameObject);
    }
}