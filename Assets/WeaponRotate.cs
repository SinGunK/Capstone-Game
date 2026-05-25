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
    readonly List<OrbVfx> _orbVfxList = new();
    readonly Dictionary<Collider2D, float> _hitCooldowns = new();

    // evo1
    readonly List<float> _glitchTimers = new();
    readonly List<GameObject> _afterImagePool = new();

    // evo2
    readonly List<List<SpriteRenderer>> _tailDots = new();
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

    // ─── 오브 생성 ────────────────────────────────────

    void SpawnOrb()
    {
        var orb = new GameObject("Orb");
        orb.transform.SetParent(transform);

        var col = orb.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = orbSize;

        var vfxObj = new GameObject("OrbVfx");
        vfxObj.transform.SetParent(orb.transform);
        vfxObj.transform.localPosition = Vector3.zero;
        var vfx = vfxObj.AddComponent<OrbVfx>();
        vfx.Init(orbSize, OrbColor());

        _orbs.Add(orb);
        _orbVfxList.Add(vfx);

        if (evoLevel == 1)
            _glitchTimers.Add(Random.Range(0f, glitchInterval));

        if (evoLevel == 2)
            _tailDots.Add(CreateTailDots());
    }

    void RebuildOrbs()
    {
        foreach (var o in _orbs) Destroy(o);
        _orbs.Clear();
        _orbVfxList.Clear();
        _glitchTimers.Clear();
        _hitCooldowns.Clear();
        _tailHitCooldowns.Clear();

        foreach (var img in _afterImagePool) Destroy(img);
        _afterImagePool.Clear();

        foreach (var dots in _tailDots)
            foreach (var d in dots)
                if (d != null) Destroy(d.gameObject);
        _tailDots.Clear();

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
        }
    }

    // ─── 공통 오브 접촉 판정 ──────────────────────────

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

    void SpawnAfterImage(Vector2 pos)
    {
        GameObject img = null;
        foreach (var pooled in _afterImagePool)
            if (!pooled.activeSelf) { img = pooled; break; }

        if (img == null)
        {
            img = new GameObject("GlitchAfterImage");
            var vfxObj = new GameObject("Vfx");
            vfxObj.transform.SetParent(img.transform);
            vfxObj.transform.localPosition = Vector3.zero;
            var vfx = vfxObj.AddComponent<OrbVfx>();
            vfx.Init(orbSize * 0.85f, Color.white);
            _afterImagePool.Add(img);
        }

        img.transform.position = pos;
        img.SetActive(true);
        StartCoroutine(FadeOutAfterImage(img, 0.15f));
    }

    IEnumerator FadeOutAfterImage(GameObject obj, float duration)
    {
        var vfx = obj.GetComponentInChildren<OrbVfx>();
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            vfx?.SetColor(new Color(1f, 1f, 1f, Mathf.Lerp(0.8f, 0f, t / duration)));
            yield return null;
        }
        if (obj != null) obj.SetActive(false);
    }

    void UpdateGlitch()
    {
        while (_glitchTimers.Count < _orbs.Count)
            _glitchTimers.Add(Random.Range(0f, glitchInterval));

        for (int i = 0; i < _orbs.Count; i++)
        {
            _glitchTimers[i] -= Time.deltaTime;

            // 텔포 임박하면 깜빡임 빠르게
            float flickerSpeed = _glitchTimers[i] < glitchInterval * 0.4f ? 40f : 8f;
            bool visible = Mathf.Sin(Time.time * flickerSpeed) > 0f;
            _orbVfxList[i].SetColor(visible ? Color.white : new Color(0.05f, 0.05f, 0.05f, 0.2f));

            if (_glitchTimers[i] > 0f) continue;

            Vector2 prevPos = _orbs[i].transform.position;

            // 가장 가까운 적 탐색
            Collider2D nearestEnemy = FindNearestEnemy(prevPos);

            Vector2 teleportPos;
            if (nearestEnemy != null)
                teleportPos = (Vector2)nearestEnemy.transform.position;
            else
                teleportPos = (Vector2)transform.position + AngleToDir(Random.Range(0f, 360f)) * orbitRadius;

            SpawnAfterImage(prevPos);
            _orbs[i].transform.position = teleportPos;
            GlitchBlast(teleportPos);

            // 0.15초 후 궤도 복귀
            StartCoroutine(ReturnToOrbit(i, teleportPos));

            _glitchTimers[i] = glitchInterval + Random.Range(-0.2f, 0.2f);
        }
    }

    Collider2D FindNearestEnemy(Vector2 from)
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, orbitRadius * 4f);
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

        SpawnAfterImage(fromPos);
        _orbs[orbIndex].transform.position = orbitPos;
    }

    void GlitchBlast(Vector2 pos)
    {
        var hits = Physics2D.OverlapCircleAll(pos, glitchBlastRadius);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy") || IsOnCooldown(hit, _hitCooldowns)) continue;
            hit.GetComponent<IDamageable>()?.OnDamaged(CurrentDamage * glitchDamageRatio, pos);
            SetCooldown(hit, _hitCooldowns);
        }
    }

    // ─── evo2 꼬리 ────────────────────────────────────

    List<SpriteRenderer> CreateTailDots()
    {
        var dots = new List<SpriteRenderer>();
        var sprite = OrbVfx.GetCircleSprite();

        for (int i = 0; i < tailDotCount; i++)
        {
            var dotObj = new GameObject("TailDot");
            dotObj.transform.SetParent(transform, false);
            dotObj.transform.localScale = Vector3.one;

            var sr = dotObj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 4;

            dots.Add(sr);
        }
        return dots;
    }

    void UpdateTailDots()
    {
        float step = 360f / _orbs.Count;
        Color c = OrbColor();

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

                float alpha = Mathf.Lerp(0.75f, 0f, t);
                dots[j].color = new Color(c.r, c.g, c.b, alpha);
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

    Color OrbColor() => evoLevel switch
    {
        0 => Color.white,
        1 => Color.white,
        2 => new Color(0f, 0.8f, 1f),
        _ => Color.white
    };

    Vector2 AngleToDir(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }
}