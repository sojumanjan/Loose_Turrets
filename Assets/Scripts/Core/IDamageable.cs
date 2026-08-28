// 데미지를 받을 수 있는 대상이 구현하는 인터페이스. 총알과 적이 대상 종류를 몰라도 때릴 수 있게 해준다.

using UnityEngine;

public interface IDamageable
{
    bool IsAlive { get; }
    Transform Transform { get; }

    /// <summary>데미지를 입힌다. hitFrom은 넉백/이펙트 방향 계산용 피격 지점.</summary>
    void TakeDamage(float amount, Vector3 hitFrom);
}
