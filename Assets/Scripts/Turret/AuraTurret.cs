// 오라 포탑. 조준하지 않고 자기 주변 원 안의 모든 적에게 일정 간격으로 지속 피해를 준다.
// 특수 강화를 받으면 같은 범위의 적을 감속시킨다.

using System.Collections.Generic;
using UnityEngine;

public class AuraTurret : TurretBase
{
    [Header("오라")]
    [Tooltip("특수 강화 후 적 이동 속도에 곱해지는 값. 0.7이면 30% 감속.")]
    [Range(0.05f, 1f)] [SerializeField] private float slowFactor = 0.7f;

    [Tooltip("감속 지속 시간. 발사 간격보다 길어야 범위 안에 있는 동안 끊기지 않는다.")]
    [SerializeField] private float slowDuration = 0.7f;

    // 매 틱마다 리스트를 새로 만들지 않도록 공용 버퍼를 쓴다.
    private static readonly List<EnemyBase> buffer = new List<EnemyBase>(64);

    // 오라는 범위가 곧 정체성이라 항상 보여준다.
    protected override bool AlwaysShowRange => true;

    protected override void Fire(EnemyBase target)
    {
        // target은 쓰지 않는다. 범위 안 전체가 대상이다.
        EnemyRegistry.FindAllInRange(transform.position, EffectiveRange, buffer);

        bool slows = SpecialLevel > 0;

        for (int i = 0; i < buffer.Count; i++)
        {
            EnemyBase enemy = buffer[i];
            if (enemy == null || !enemy.IsAlive) continue;

            if (slows) enemy.ApplySlow(slowFactor, slowDuration);

            // 데미지를 나중에 준다. 먼저 주면 이번 틱에 죽는 적에게 감속이 안 걸린다.
            enemy.TakeDamage(EffectiveDamage, transform.position);
        }
    }

    // 오라는 방향이 없으므로 회전도 반동도 하지 않는다.
    protected override void AimAt(Vector3 worldPosition) { }

    protected override void PlayRecoil() { }

    // 틱마다 발사음을 내면 기관총이 된다. 오라의 소리는 TurretDef의 LoopSfx가 맡는다.
    protected override void PlayFireSfx() { }
}
