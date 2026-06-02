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

    [Header("VFX Prefabs")] // 각 진화단계별 오브 프리팹 인스펙터에서 연결
    [SerializeField] GameObject evo0OrbPrefab;
    [SerializeField] GameObject evo1OrbPrefab;
    [SerializeField] GameObject evo1BlastPrefab; // evo1 텔레포트 폭발 이펙트
    [SerializeField] GameObject evo2OrbPrefab;

    [Header("Evo1 - Glitch")]
    [SerializeField] float glitchInterval = 1.2f;  // 텔레포트 주기
    [SerializeField] float glitchBlastRadius = 1.0f; // 폭발 범위
    [SerializeField] float glitchDamageRatio = 0.6f; // 폭발 데미지 비율

    [Header("Evo2 - Stellar")]
    [SerializeField] float cometSpeedMult = 1.4f;   // 회전속도 배율
    [SerializeField] int tailSegments = 8;           // 꼬리 세그먼트 수
    [SerializeField] float tailAngleSpan = 45f;      // 꼬리 길이 (각도)
    [SerializeField] float tailDamageInterval = 0.3f;
    [SerializeField] Material tailMaterial;          // URP 머티리얼 연결 필요

    readonly List<GameObject> _orbs = new();
    readonly Dictionary<Collider2D, float> _hitCooldowns = new();
    readonly List<float> _glitchTimers = new();

    readonly List<LineRenderer> _tailLines = new();
    readonly Dictionary<Collider2D, float> _tailHitCooldowns = new();

    int _orbCount = 1;
    float _currentAngle;

    GameObject CurrentOrbPrefab() => evoLevel switch
    {
        0 => evo0OrbPrefab,
        1 => evo1OrbPrefab,
        2 => evo2OrbPrefab,
        _ => evo0OrbPrefab
    };

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
                UpdateTails();
                CheckTailHit();
                break;
        }

        CleanupCooldowns(_hitCooldowns);
        CleanupCooldowns(_tailHitCooldowns);
    }

    // ─── 진화 시 호출 (WeaponBase.Evolve()가 자동 호출) ──
    // 진화 조건 충족 시 외부에서 Evolve() 호출하면 됨
    protected override void OnEvolve()
    {
        switch (evoLevel)
        {
            case 1:
                dmgStat.addValue += 8f;
                RebuildOrbs();
                break;
            case 2:
                dmgStat.addValue += 15f;
                RebuildOrbs();
                break;
        }
    }

    // ─── 오브 생성 ────────────────────────────────────

    void SpawnOrb()
    {
        var orb = new GameObject("Orb");
        orb.transform.SetParent(transform);

        var col = orb.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = orbSize;

        var prefab = CurrentOrbPrefab();
        if (prefab != null)
        {
            var vfx = Instantiate(prefab, orb.transform);
            vfx.transform.localPosition = Vector3.zero;
            vfx.transform.localScale = Vector3.one * orbSize * 2f;
        }

        _orbs.Add(orb);

        if (evoLevel == 1)
            _glitchTimers.Add(Random.Range(0f, glitchInterval));

        if (evoLevel == 2)
            _tailLines.Add(CreateTailLine());
    }

    void RebuildOrbs()
    {
        foreach (var o in _orbs) Destroy(o);
        _orbs.Clear();
        _glitchTimers.Clear();
        _hitCooldowns.Clear();
        _tailHitCooldowns.Clear();

        foreach (var lr in _tailLines)
            if (lr != null) Destroy(lr.gameObject);
        _tailLines.Clear();

        for (int i = 0; i < _orbCount; i++)
            SpawnOrb();
    }

    // ─── 회전 ─────────────────────────────────────────

    void RotateOrbs()
    {
        float speed = evoLevel == 2 ? rotateSpeed * cometSpeedMult : rotateSpeed;
        _currentAngle += speed * Time.deltaTime;

        float step = 360f / _orbs.Count;
        for (int i = 0; i < _orbs.Count; i++)
        {
            float angle = _currentAngle + step * i;
            _orbs[i].transform.position = (Vector2)transform.position + AngleToDir(angle) * orbitRadius;

            if (_orbs[i].transform.childCount > 0)
            {
                if (evoLevel == 2)
                {
                    float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.15f;
                    _orbs[i].transform.GetChild(0).localScale = Vector3.one * orbSize * 2f * pulse;
                }
                else
                {
                    _orbs[i].transform.GetChild(0).Rotate(0, 0, rotateSpeed * Time.deltaTime);
                }
            }
        }
    }

    // ─── 공통 오브 판정 ───────────────────────────────

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

    // ─── evo1 글리치 ──────────────────────────────────
    // 오브가 주기적으로 가장 가까운 적에게 텔레포트 후 폭발
    // 텔레포트 후 0.15초 뒤 궤도로 복귀

    void UpdateGlitch()
    {
        while (_glitchTimers.Count < _orbs.Count)
            _glitchTimers.Add(Random.Range(0f, glitchInterval));

        for (int i = 0; i < _orbs.Count; i++)
        {
            _glitchTimers[i] -= Time.deltaTime;

            float flickerSpeed = _glitchTimers[i] < glitchInterval * 0.4f ? 40f : 8f;
            bool visible = Mathf.Sin(Time.time * flickerSpeed) > 0f;
            if (_orbs[i].transform.childCount > 0)
                _orbs[i].transform.GetChild(0).gameObject.SetActive(visible);

            if (_glitchTimers[i] > 0f) continue;

            Vector2 prevPos = _orbs[i].transform.position;
            Collider2D nearestEnemy = FindNearestEnemy(prevPos);

            Vector2 teleportPos = nearestEnemy != null
                ? (Vector2)nearestEnemy.transform.position
                : (Vector2)transform.position + AngleToDir(Random.Range(0f, 360f)) * orbitRadius;

            if (evo1OrbPrefab != null)
            {
                var img = Instantiate(evo1OrbPrefab, prevPos, Quaternion.identity);
                Destroy(img, 0.15f);
            }

            _orbs[i].transform.position = teleportPos;
            GlitchBlast(teleportPos);
            StartCoroutine(ReturnToOrbit(i, teleportPos));
            _glitchTimers[i] = glitchInterval + Random.Range(-0.2f, 0.2f);
        }
    }

    Collider2D FindNearestEnemy(Vector2 from)
    {
        Vector2 playerPos = GameContext.PlayerTransform != null
            ? (Vector2)GameContext.PlayerTransform.position
            : (Vector2)transform.position;

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

        if (evo1OrbPrefab != null)
        {
            var img = Instantiate(evo1OrbPrefab, fromPos, Quaternion.identity);
            Destroy(img, 0.15f);
        }
        _orbs[orbIndex].transform.position = orbitPos;
    }

    void GlitchBlast(Vector2 pos)
    {
        float blastSize = orbSize * 2f;

        if (evo1BlastPrefab != null)
        {
            var blast = Instantiate(evo1BlastPrefab, pos, Quaternion.identity);
            blast.transform.localScale = Vector3.one * blastSize * 2f;
            Destroy(blast, 0.2f);
        }

        var hits = Physics2D.OverlapCircleAll(pos, blastSize);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy") || IsOnCooldown(hit, _hitCooldowns)) continue;
            hit.GetComponent<IDamageable>()?.OnDamaged(CurrentDamage * glitchDamageRatio, pos);
            SetCooldown(hit, _hitCooldowns);
        }
    }

    // ─── evo2 꼬리 (LineRenderer) ─────────────────────
    // 오브 뒤에 꼬리가 따라오며 꼬리에도 타격 판정 있음

    LineRenderer CreateTailLine()
    {
        var tailObj = new GameObject("OrbTail");
        tailObj.transform.SetParent(transform);

        var lr = tailObj.AddComponent<LineRenderer>();
        lr.positionCount = tailSegments;
        lr.startWidth = orbSize * 1.5f;
        lr.endWidth = 0f;
        lr.useWorldSpace = true;
        lr.sortingOrder = 4;
        lr.numCapVertices = 4;

        if (tailMaterial != null)
            lr.material = tailMaterial;
        else
            lr.material = new Material(Shader.Find("Sprites/Default"));

        Color c = new Color(0f, 0.8f, 1f);
        lr.startColor = new Color(c.r, c.g, c.b, 0.9f);
        lr.endColor = new Color(c.r, c.g, c.b, 0f);

        return lr;
    }

    void UpdateTails()
    {
        float step = 360f / _orbs.Count;

        for (int i = 0; i < _orbs.Count && i < _tailLines.Count; i++)
        {
            var lr = _tailLines[i];
            if (lr == null) continue;

            float orbAngle = _currentAngle + step * i;

            for (int j = 0; j < tailSegments; j++)
            {
                float t = (float)j / (tailSegments - 1);
                float tailAngle = orbAngle - t * tailAngleSpan;
                Vector2 pos = (Vector2)transform.position + AngleToDir(tailAngle) * orbitRadius;
                lr.SetPosition(j, pos);
            }
        }
    }

    void CheckTailHit()
    {
        float step = 360f / _orbs.Count;

        for (int i = 0; i < _orbs.Count && i < _tailLines.Count; i++)
        {
            float orbAngle = _currentAngle + step * i;

            for (int j = 1; j < tailSegments; j++)
            {
                float t = (float)j / (tailSegments - 1);
                float tailAngle = orbAngle - t * tailAngleSpan;
                Vector2 checkPos = (Vector2)transform.position + AngleToDir(tailAngle) * orbitRadius;
                float checkRadius = Mathf.Lerp(orbSize * 0.6f, orbSize * 0.1f, t);

                var hits = Physics2D.OverlapCircleAll(checkPos, checkRadius);
                foreach (var hit in hits)
                {
                    if (!hit.CompareTag("Enemy") || IsOnCooldown(hit, _tailHitCooldowns)) continue;
                    hit.GetComponent<IDamageable>()?.OnDamaged(CurrentDamage * 0.5f, checkPos);
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

    // ─── 레벨업 옵션 ──────────────────────────────────
    // 레벨업 UI에서 플레이어가 선택한 옵션에 따라 호출
    // UpgradeRotateSpeed() : 오브 회전속도 증가
    // UpgradeDamage() : 데미지 증가
    // UpgradeOrbCount() : 오브 개수 증가 (evo2는 꼬리 길이 증가)
    public void UpgradeRotateSpeed() { rotateSpeed += 30f; }
    public void UpgradeDamage() { dmgStat.addValue += 5f; }
    public void UpgradeOrbCount()
    {
        if (evoLevel == 2)
            tailAngleSpan += 10f;
        else
        {
            _orbCount = Mathf.Min(_orbCount + 1, maxOrbCount);
            if (_orbs.Count < _orbCount)
                SpawnOrb();
        }
    }
}