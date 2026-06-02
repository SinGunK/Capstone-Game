using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WeaponRotate : WeaponBase
{
    [Header("Rotate Setting")]
    [SerializeField] float orbitRadius = 2.5f;
    [SerializeField] float rotateSpeed = 120f;
    [SerializeField] int maxOrbCount = 4;
    [SerializeField] float orbSize = 0.6f;

    [Header("--- 외부 VFX 에셋 프리팹 (독립성 추가) ---")]
    [Tooltip("기본적으로 회전할 오브(구체) 프리팹")]
    [SerializeField] GameObject orbPrefab;
    [Tooltip("1단계 글리치 텔레포트 폭발 시 생성될 이펙트")]
    [SerializeField] GameObject glitchBlastPrefab;

    [Header("Evo1 - Glitch")]
    [SerializeField] float glitchInterval = 1.2f;
    [SerializeField] float glitchBlastRadius = 1.0f;
    [SerializeField] float glitchDamageRatio = 0.6f;

    [Header("Evo2 - Stellar")]
    [SerializeField] float cometSpeedMult = 1.4f;
    [SerializeField] int tailDotCount = 6;
    [SerializeField] float tailAngleSpan = 60f;
    [SerializeField] float tailDamageInterval = 0.3f;

    readonly List<GameObject> _orbs = new();
    readonly Dictionary<Collider2D, float> _hitCooldowns = new();

    // evo1
    readonly List<float> _glitchTimers = new();

    // evo2
    readonly List<List<GameObject>> _tailDots = new(); // SpriteRenderer에서 GameObject(프리팹 대응)로 변경
    readonly Dictionary<Collider2D, float> _tailHitCooldowns = new();

    int _orbCount = 1;
    float _currentAngle;

    protected override void Attack()
    {
        if (_orbs.Count < _orbCount && _orbs.Count < maxOrbCount)
            SpawnOrb();
    }

    protected override void Update()
    {
        base.Update();
        if (_orbs.Count == 0) return;

        switch (evoLevel)
        {
            case 0:
                RotateOrbs();
                CheckOrbHit();
                break;
            case 1:
                RotateOrbs();
                CheckOrbHit();
                UpdateGlitch();
                break;
            case 2:
                RotateOrbs();
                CheckOrbHit();
                UpdateTailDots();
                CheckTailHit();
                break;
        }

        CleanupCooldowns(_hitCooldowns);
        CleanupCooldowns(_tailHitCooldowns);
    }

    protected override void OnEvolve()
    {
        switch (evoLevel)
        {
            case 1:
                dmgStat.addValue += 8f;
                spdStat.multiValue += 0.2f;
                RebuildOrbs();
                break;
            case 2:
                dmgStat.addValue += 15f;
                spdStat.multiValue += 0.1f;
                RebuildOrbs();
                break;
        }
    }

    public override void OnLevelUp()
    {
        base.OnLevelUp();
        _orbCount = Mathf.Min(_orbCount + 1, maxOrbCount);
    }

    // ─── 오브 생성 (코드로 그리던 OrbVfx 완전 제거) ──────────────────────

    void SpawnOrb()
    {
        // 1. 기본 물리용 오행 오브 생성
        var orb = new GameObject("Orb");
        orb.transform.SetParent(transform);

        var col = orb.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = orbSize;

        // 2. 코드로 그리던 OrbVfx 대신 외부 프리팹을 자식으로 생성
        if (orbPrefab != null)
        {
            var vfx = Instantiate(orbPrefab, orb.transform);
            vfx.transform.localPosition = Vector3.zero;
            vfx.transform.localScale = Vector3.one * orbSize * 2f;
        }

        _orbs.Add(orb);

        if (evoLevel == 1)
            _glitchTimers.Add(Random.Range(0f, glitchInterval));

        if (evoLevel == 2)
            _tailDots.Add(CreateTailDots());
    }

    void RebuildOrbs()
    {
        foreach (var o in _orbs) Destroy(o);
        _orbs.Clear();
        _glitchTimers.Clear();
        _hitCooldowns.Clear();
        _tailHitCooldowns.Clear();

        foreach (var dots in _tailDots)
            foreach (var d in dots)
                if (d != null) Destroy(d);
        _tailDots.Clear();

        for (int i = 0; i < _orbCount; i++)
            SpawnOrb();
    }

    void RotateOrbs()
    {
        float speed = evoLevel == 2 ? rotateSpeed * cometSpeedMult : rotateSpeed;
        _currentAngle += speed * Time.deltaTime;

        float step = 360f / _orbs.Count;
        for (int i = 0; i < _orbs.Count; i++)
        {
            float angle = _currentAngle + step * i;
            _orbs[i].transform.position = (Vector2)transform.position + AngleToDir(angle) * orbitRadius;
        }
    }

    void CheckOrbHit()
    {
        foreach (var orb in _orbs)
        {
            var hits = Physics2D.OverlapCircleAll(orb.transform.position, orbSize);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy") || IsOnCooldown(hit, _hitCooldowns)) continue;
                hit.GetComponent<IDamageable>()?.OnDamaged(CurrentDamage, orb.transform.position);
                if (evoLevel == 0)
                    hit.GetComponent<ISlowable>()?.ApplySlow(0.4f, 1.5f);
                SetCooldown(hit, _hitCooldowns);
            }
        }
    }

    // ─── evo1 글리치 (자체 페이드아웃 코루틴 삭제 후 프리팹 생성으로 단순화) ──────

    void UpdateGlitch()
    {
        while (_glitchTimers.Count < _orbs.Count)
            _glitchTimers.Add(Random.Range(0f, glitchInterval));

        for (int i = 0; i < _orbs.Count; i++)
        {
            _glitchTimers[i] -= Time.deltaTime;

            // 단순 깜빡임 연출은 오브 자체를 껐다 켜는 방식으로 호환성 확보
            float flickerSpeed = _glitchTimers[i] < glitchInterval * 0.4f ? 40f : 8f;
            bool visible = Mathf.Sin(Time.time * flickerSpeed) > 0f;
            if (_orbs[i].transform.childCount > 0)
                _orbs[i].transform.GetChild(0).gameObject.SetActive(visible);

            if (_glitchTimers[i] > 0f) continue;

            Vector2 prevPos = _orbs[i].transform.position;
            Collider2D nearestEnemy = FindNearestEnemy(prevPos);

            Vector2 teleportPos;
            if (nearestEnemy != null)
                teleportPos = (Vector2)nearestEnemy.transform.position;
            else
                teleportPos = (Vector2)transform.position + AngleToDir(Random.Range(0f, 360f)) * orbitRadius;

            // 잔상 자리에 오브 프리팹 복사본 남기기
            if (orbPrefab != null)
            {
                GameObject img = Instantiate(orbPrefab, prevPos, Quaternion.identity);
                Destroy(img, 0.15f); // 0.15초 뒤 자동 파괴
            }

            _orbs[i].transform.position = teleportPos;
            GlitchBlast(teleportPos);

            StartCoroutine(ReturnToOrbit(i, teleportPos));
            _glitchTimers[i] = glitchInterval + Random.Range(-0.2f, 0.2f);
        }
    }

    Collider2D FindNearestEnemy(Vector2 from)
    {
        Vector2 playerPos = GameContext.PlayerTransform != null ? (Vector2)GameContext.PlayerTransform.position : (Vector2)transform.position;
        var hits = Physics2D.OverlapCircleAll(playerPos, orbitRadius * 4f);
        Collider2D nearest = null;
        float minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;
            float dist = Vector2.Distance(from, hit.transform.position);
            if (dist < minDist) { minDist = dist; nearest = hit; }
        }
        return nearest;
    }

    IEnumerator ReturnToOrbit(int orbIndex, Vector2 fromPos)
    {
        yield return new WaitForSeconds(0.15f);
        if (orbIndex >= _orbs.Count || _orbs[orbIndex] == null) yield break;

        float step = 360f / _orbs.Count;
        float angle = _currentAngle + step * orbIndex;
        Vector2 orbitPos = (Vector2)transform.position + AngleToDir(angle) * orbitRadius;

        if (orbPrefab != null)
        {
            GameObject img = Instantiate(orbPrefab, fromPos, Quaternion.identity);
            Destroy(img, 0.15f);
        }
        _orbs[orbIndex].transform.position = orbitPos;
    }

    void GlitchBlast(Vector2 pos)
    {
        if (glitchBlastPrefab) Instantiate(glitchBlastPrefab, pos, Quaternion.identity);

        var hits = Physics2D.OverlapCircleAll(pos, glitchBlastRadius);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy") || IsOnCooldown(hit, _hitCooldowns)) continue;
            hit.GetComponent<IDamageable>()?.OnDamaged(CurrentDamage * glitchDamageRatio, pos);
            SetCooldown(hit, _hitCooldowns);
        }
    }

    // ─── evo2 꼬리 (코드로 그리던 원형 꼬리 대신 오브 프리팹 복사생성으로 전환) ───

    List<GameObject> CreateTailDots()
    {
        var dots = new List<GameObject>();
        for (int i = 0; i < tailDotCount; i++)
        {
            if (orbPrefab == null) break;
            
            // 꼬리도 오브 프리팹을 복사해서 생성
            var dotObj = Instantiate(orbPrefab, transform, false);
            dots.Add(dotObj);
        }
        return dots;
    }

    void UpdateTailDots()
    {
        float step = 360f / _orbs.Count;

        for (int i = 0; i < _orbs.Count && i < _tailDots.Count; i++)
        {
            var dots = _tailDots[i];
            float orbAngle = _currentAngle + step * i;

            for (int j = 0; j < dots.Count; j++)
            {
                if (dots[j] == null) continue;

                float t = (float)(j + 1) / (dots.Count + 1);
                float dotAngle = orbAngle - t * tailAngleSpan;

                Vector2 worldPos = (Vector2)transform.position + AngleToDir(dotAngle) * orbitRadius;
                dots[j].transform.position = worldPos;

                float size = Mathf.Lerp(orbSize * 1.1f, orbSize * 0.15f, t);
                dots[j].transform.localScale = Vector3.one * size;
            }
        }
    }

    void CheckTailHit()
    {
        for (int i = 0; i < _orbs.Count && i < _tailDots.Count; i++)
        {
            var dots = _tailDots[i];
            for (int j = 0; j < dots.Count; j++)
            {
                if (dots[j] == null) continue;

                float t = (float)(j + 1) / (dots.Count + 1);
                float checkRadius = Mathf.Lerp(orbSize * 0.5f, orbSize * 0.1f, t);

                var hits = Physics2D.OverlapCircleAll(dots[j].transform.position, checkRadius);
                foreach (var hit in hits)
                {
                    if (!hit.CompareTag("Enemy") || IsOnCooldown(hit, _tailHitCooldowns)) continue;
                    hit.GetComponent<IDamageable>()?.OnDamaged(CurrentDamage * 0.5f, dots[j].transform.position);
                    SetCooldown(hit, _tailHitCooldowns, tailDamageInterval);
                }
            }
        }
    }

    // ─── 유틸 ─────────────────────────────────────────

    bool IsOnCooldown(Collider2D col, Dictionary<Collider2D, float> dict) =>
        dict.TryGetValue(col, out float t) && Time.time < t;

    void SetCooldown(Collider2D col, Dictionary<Collider2D, float> dict, float duration = -1f)
    {
        float cd = duration < 0 ? 1f / CurrentAttackSpeed : duration;
        dict[col] = Time.time + cd;
    }

    void CleanupCooldowns(Dictionary<Collider2D, float> dict)
    {
        var toRemove = new List<Collider2D>();
        foreach (var kv in dict)
            if (Time.time >= kv.Value) toRemove.Add(kv.Key);
        foreach (var k in toRemove) dict.Remove(k);
    }

    Vector2 AngleToDir(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }
}