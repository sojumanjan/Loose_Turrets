// 플레이어를 향해 직진으로 쫓아오는 가장 기본 적. EnemyBase의 기본 이동을 그대로 쓴다.

using UnityEngine;

public class ChaserEnemy : EnemyBase
{
    protected override void Move(Vector3 targetPosition)
    {
        MoveStraightTo(targetPosition);
    }
}
