using UnityEngine;
using System.Collections;

public class WeaponArea : WeaponBase
{
    [Header("Area Setting")]
    [SerializeField] float baseRange = 3f;
    [SerializeField] float pullForce = 3f;
    [SerializeField] float stellarStunDuration = 1f;

    PlayerController _player;

    void Start()
    {
        _player = GetComponentInParent<PlayerController>();
        if (_player == null)
            _player = GetComponent<PlayerController>();
    }

    protected override void Attack()
    {
        switch (evoLevel)
        {
            case 0:
                SpawnVfx(30f, baseRange, false);
                SlashArc(30f, baseRange, CurrentDamage);
                break;
            case 1:
                SpawnVfx(60f, baseRange * 1.2f, true);
                GlitchSlash();
                break;
            case 2:
                SpawnVfx(90f, baseRange * 2f, false);
                StartCoroutine(StellarSlash());
                break;
        }
    }

    void SpawnVfx(float angle, float range, bool glitch)
    {
        var obj = new GameObject("SlashVfx");
        obj.transform.position = transform.position;
        var vfx = obj.AddComponent<SlashVfx>();

        Color color = evoLevel == 0 ? Color.white :
                      evoLevel == 1 ? new Color(0.6f, 0f, 1f) :
                      new Color(0f, 0.8f, 1f);

        Vector2 dir = _player != null ? _player.lastMoveDir : Vector2.down;
        vfx.Init(angle, range, dir, color, glitch: glitch);
    }

    void SlashArc(float angle, float range, float damage, Vector2? customOrigin = null, Vector2? customDir = null, bool applyBlackholeStun = false)
    {
        Vector2 origin = customOrigin ?? (Vector2)transform.position;
        Vector2 dir = customDir ?? (_player != null ? _player.lastMoveDir : Vector2.down);

        var hits = Physics2D.OverlapCircleAll(origin, range);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;

            Vector2 toEnemy = ((Vector2)hit.transform.position - origin).normalized;
            if (Vector2.Angle(dir, toEnemy) > angle / 2f) continue;

            hit.GetComponent<IDamageable>()?.OnDamaged(damage, origin);

            if (applyBlackholeStun)
            {
                float stunTime = Mathf.Min(stellarStunDuration, 1f / CurrentAttackSpeed - 0.1f);
                hit.GetComponent<IStunnable>()?.ApplyStun(stunTime);

                var rb = hit.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Vector2 pullDir = (origin - (Vector2)hit.transform.position).normalized;
                    rb.AddForce(pullDir * pullForce, ForceMode2D.Impulse);
                }
            }
        }
    }

    void GlitchSlash()
    {
        Vector2 origin = transform.position;
        Vector2 dir = _player != null ? _player.lastMoveDir : Vector2.down;
        float angle = 60f;
        float range = baseRange * 1.2f;

        SlashArc(angle, range, CurrentDamage, origin, dir);
        StartCoroutine(GlitchArcAfterImage(origin, dir, angle, range));
    }

    IEnumerator GlitchArcAfterImage(Vector2 origin, Vector2 dir, float angle, float range)
    {
        float[] damageMultipliers = { 0.5f, 0.3f, 0.1f };

        for (int i = 0; i < damageMultipliers.Length; i++)
        {
            yield return new WaitForSeconds(0.34f);

            float movedDist = Vector2.Distance(transform.position, origin);
            if (movedDist < 0.3f) continue;

            var obj = new GameObject("GlitchAfterImage");
            obj.transform.position = origin;
            var vfx = obj.AddComponent<SlashVfx>();
            vfx.Init(angle, range, dir, new Color(0.6f, 0f, 1f, 0.4f), glitch: true);

            float reducedDamage = CurrentDamage * damageMultipliers[i];
            SlashArc(angle, range, reducedDamage, origin, dir);
        }
    }

    IEnumerator StellarSlash()
    {
        Vector2 origin = transform.position;
        Vector2 dir = _player != null ? _player.lastMoveDir : Vector2.down;

        SlashArc(90f, baseRange * 2f, CurrentDamage, origin, dir, applyBlackholeStun: true);

        yield return new WaitForSeconds(0.15f);
        SlashArc(90f, baseRange * 2f, CurrentDamage, origin, dir);
    }

    protected override void OnEvolve()
    {
        switch (evoLevel)
        {
            case 1: dmgStat.addValue += 5f; break;
            case 2: dmgStat.addValue += 12f; break;
        }
    }
}