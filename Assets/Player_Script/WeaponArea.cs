using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WeaponArea : WeaponBase
{
    [Header("Area Setting")]
    [SerializeField] float baseRange = 3f;

    [Header("Evo0 - Shield")]
    [SerializeField] GameObject evo0SlashPrefab;
    [SerializeField] float evo0Duration = 0.5f;
    [SerializeField] float evo0MoveSpeed = 8f;
    [SerializeField] float evo0RotateSpeed = 360f;
    [SerializeField] float evo0StartOffset = 0.8f;

    [Header("Evo1 - Glitch")]
    [SerializeField] GameObject evo1SlashPrefab;
    [SerializeField] GameObject evo1AfterImagePrefab;
    [SerializeField] float evo1Duration = 0.5f;

    [Header("Evo2 - Stellar")]
    [SerializeField] GameObject evo2SlashPrefab;
    [SerializeField] float evo2Duration = 0.6f;

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
            case 0: StartCoroutine(ShieldThrow()); break;
            case 1:
                SpawnVfx(evo1SlashPrefab, baseRange * 1.2f, evo1Duration);
                GlitchSlash();
                break;
            case 2:
                SpawnVfx(evo2SlashPrefab, baseRange * 2f, evo2Duration);
                StartCoroutine(StellarSlash());
                break;
        }
    }

    // ─── evo0 방패 던지기 ─────────────────────────────

    IEnumerator ShieldThrow()
    {
        if (evo0SlashPrefab == null) yield break;

        Vector2 dir = _player != null ? _player.lastMoveDir : Vector2.down;
        Vector2 startPos = (Vector2)transform.position + dir * evo0StartOffset;

        var obj = Instantiate(evo0SlashPrefab, startPos, Quaternion.identity);
        obj.transform.SetParent(null);

        float halfAngleRad = 15f * Mathf.Deg2Rad;
        float boxWidth = 2f * baseRange * Mathf.Sin(halfAngleRad);
        obj.transform.localScale = Vector3.one * boxWidth;

        Destroy(obj, evo0Duration);

        float elapsed = 0f;
        HashSet<Collider2D> alreadyHit = new();

        while (elapsed < evo0Duration && obj != null)
        {
            obj.transform.position += (Vector3)(dir * evo0MoveSpeed * Time.deltaTime);
            obj.transform.Rotate(0, 0, evo0RotateSpeed * Time.deltaTime);

            var hits = Physics2D.OverlapBoxAll(
                obj.transform.position,
                new Vector2(boxWidth, boxWidth),
                0f
            );

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy") || alreadyHit.Contains(hit)) continue;
                hit.GetComponent<IDamageable>()?.OnDamaged(CurrentDamage, obj.transform.position);
                alreadyHit.Add(hit);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // ─── evo1/2 공통 VFX 스폰 ────────────────────────

    void SpawnVfx(GameObject prefab, float range, float duration)
    {
        if (prefab == null) return;

        Vector2 dir = _player != null ? _player.lastMoveDir : Vector2.down;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0, 0, angle - 90f);
        Vector2 spawnPos = (Vector2)transform.position + dir * (range * 0.4f);

        var obj = Instantiate(prefab, spawnPos, rotation);
        obj.transform.SetParent(null);
        obj.transform.localScale = Vector3.one * (range / 2.7f);
        Destroy(obj, duration);
    }

    // ─── evo1 글리치 ──────────────────────────────────

    void SlashArc(float angle, float range, float damage, Vector2? customOrigin = null, Vector2? customDir = null)
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

            if (evo1AfterImagePrefab != null)
            {
                Vector2 spawnPos = origin + dir * (range * 0.5f);
                float angleRot = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                Quaternion rotation = Quaternion.Euler(0, 0, angleRot - 90f);
                var img = Instantiate(evo1AfterImagePrefab, spawnPos, rotation);
                img.transform.SetParent(null);
                img.transform.localScale = Vector3.one * (range / 3f);
                Destroy(img, evo1Duration);
            }

            SlashArc(angle, range, CurrentDamage * damageMultipliers[i], origin, dir);
        }
    }

    // ─── evo2 항성 ────────────────────────────────────

    IEnumerator StellarSlash()
    {
        Vector2 origin = transform.position;
        Vector2 dir = _player != null ? _player.lastMoveDir : Vector2.down;
        float range = baseRange * 2f;
        float angle = 90f;

        // 1타
        SlashArc(angle, range, CurrentDamage, origin, dir);

        yield return new WaitForSeconds(0.15f);

        // 2타 + 슬로우
        var hits = Physics2D.OverlapCircleAll(origin, range);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;
            Vector2 toEnemy = ((Vector2)hit.transform.position - origin).normalized;
            if (Vector2.Angle(dir, toEnemy) > angle / 2f) continue;
            hit.GetComponent<IDamageable>()?.OnDamaged(CurrentDamage, origin);
            hit.GetComponent<ISlowable>()?.ApplySlow(0.3f, 2f);
        }
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