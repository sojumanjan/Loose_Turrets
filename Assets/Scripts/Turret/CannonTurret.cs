// 대포 포탑. 크고 아주 느린 관통 포탄을 낮은 연사로 쏜다.
// 특수 강화(연발)를 받으면 포탄이 한 발씩 늘어나는데, 연발은 매번 "아직 안 쏜 다른 적"을 찾아 쏜다.
// 사거리 안에 다른 적이 없을 때만 같은 적을 다시 노린다.

using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class CannonTurret : TurretBase
{
    [Header("포탄")]
    [SerializeField] private Bullet shellPrefab;

    [Header("특수 강화: 연발")]
    [Tooltip("연발 사이 간격(초). 두두둥 하는 느낌을 주는 값.")]
    [SerializeField] private float burstDelay = 0.16f;

    [Tooltip("특수 강화(DOUBLE SHOT) 1회당 늘어나는 포탄 수.")]
    [Min(1)] [SerializeField] private int shellsPerSpecial = 1;

    // 이번 연발에서 이미 노린 적들. 다음 발이 다른 적을 고르게 하는 데 쓴다.
    private readonly List<EnemyBase> burstTargets = new List<EnemyBase>(8);

    private Tween cannonRecoil;
    private Coroutine burstRoutine;

    // TurretDef가 연결돼 있으면 CSV 값이 이긴다.
    private int ShellsPerSpecial => Def != null ? Mathf.Max(1, Def.SpecialAmount) : shellsPerSpecial;

    protected override void Fire(EnemyBase target)
    {
        burstTargets.Clear();
        if (target != null) burstTargets.Add(target);

        FireShell(target);

        int extraShots = SpecialLevel * ShellsPerSpecial;
        if (extraShots <= 0) return;

        // 이전 연발이 아직 남아있으면 겹치지 않게 끊는다.
        if (burstRoutine != null) StopCoroutine(burstRoutine);
        burstRoutine = StartCoroutine(FireBurst(extraShots));
    }

    private IEnumerator FireBurst(int remainingShots)
    {
        for (int i = 0; i < remainingShots; i++)
        {
            yield return new WaitForSeconds(burstDelay);

            if (!isActiveAndEnabled) yield break;

            FireShell(PickNextBurstTarget());

            // 첫 발은 TurretBase가 울려줬다. 연발로 더 나가는 발은 발마다 직접 울린다.
            PlayFireSfx();
            PlayRecoil();
        }

        burstRoutine = null;
    }

    /// <summary>사거리 안에서 아직 안 쏜 적을 우선 고른다. 없으면 현재 타깃으로 되돌아간다.</summary>
    private EnemyBase PickNextBurstTarget()
    {
        EnemyBase next = EnemyRegistry.FindNearestExcluding(transform.position, EffectiveRange, burstTargets);

        if (next == null) next = CurrentTarget;
        if (next != null && !burstTargets.Contains(next)) burstTargets.Add(next);

        return next;
    }

    private void FireShell(EnemyBase target)
    {
        if (shellPrefab == null || BulletPool.Instance == null) return;

        Vector3 origin = muzzle.position;

        Vector3 direction = target != null
            ? target.transform.position - origin
            : transform.forward;

        BulletPool.Instance.Fire(shellPrefab, origin, direction, EffectiveDamage);
    }

    protected override void PlayRecoil()
    {
        if (IsHeld || IsSettling) return;

        cannonRecoil?.Kill(true);
        cannonRecoil = transform.DOPunchScale(new Vector3(0f, 0f, -recoilStrength), recoilDuration, 5, 0.7f);
    }

    // base.OnDisable이 포탑 등록을 해제하므로 반드시 override로 이어받아야 한다.
    protected override void OnDisable()
    {
        base.OnDisable();

        burstRoutine = null;
        burstTargets.Clear();
    }
}
