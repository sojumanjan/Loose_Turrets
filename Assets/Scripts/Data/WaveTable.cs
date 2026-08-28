// 웨이브 진행 데이터. 한 행이 웨이브 하나이며, 그 웨이브의 모든 밸런싱 값이 한 칸 안에 들어있다.
// TSV의 w_<적id> 열이 Enemies 배열의 가중치로 들어온다.

using System;
using UnityEngine;

[CreateAssetMenu(fileName = "WaveTable", menuName = "Game Data/Wave Table")]
public class WaveTable : ScriptableObject
{
    [Serializable]
    public class EnemyWeight
    {
        public EnemyDef Def;

        [Tooltip("이 웨이브에서 뽑힐 상대 가중치. 0이면 이 웨이브에는 안 나온다.")]
        [Min(0f)] public float Weight = 1f;
    }

    [Serializable]
    public class Wave
    {
        public string Label = "wave";

        [Tooltip("이 웨이브가 지속되는 시간(초).")]
        [Min(1f)] public float Duration = 45f;

        [Tooltip("이 웨이브가 끝난 뒤 쉬는 시간(초). 마지막 웨이브에서는 무시된다.")]
        [Min(0f)] public float BreakAfter = 4f;

        [Tooltip("스폰 주기(초). 작을수록 자주 나온다.")]
        [Min(0.05f)] public float SpawnInterval = 1.2f;

        [Tooltip("한 번 스폰할 때 나오는 마리 수.")]
        [Min(1)] public int BatchSize = 1;

        [Tooltip("이 웨이브에서 동시에 살아있을 수 있는 최대 적 수.")]
        [Min(1)] public int MaxAliveEnemies = 80;

        [Tooltip("이 웨이브에서 스폰되는 모든 적의 체력 배율. 1이면 프리팹 그대로, 2면 두 배.")]
        [Min(0.1f)] public float HpMultiplier = 1f;

        [Tooltip("적이 나올 구역 번호(1~8). 비우면 8구역 전부에서 나온다. " +
                 "1,2=위  3,4=오른쪽  5,6=아래  7,8=왼쪽 (시계방향, 왼쪽 위가 1)")]
        public int[] SpawnZones;

        [Tooltip("이 웨이브에 나올 적 종류와 확률.")]
        public EnemyWeight[] Enemies;
    }

    [Serializable]
    public class EndlessEnemy
    {
        public EnemyDef Def;

        [Tooltip("시작 가중치.")]
        [Min(0f)] public float Weight = 1f;

        [Tooltip("한 단계(기본 30초)마다 가중치에 더해지는 양. 탱크를 점점 늘리려면 여기를 올린다.")]
        public float WeightPerStep;
    }

    [Serializable]
    public class EndlessConfig
    {
        [Tooltip("난이도가 한 칸 오르는 주기(초).")]
        [Min(1f)] public float StepSeconds = 30f;

        [Tooltip("한 단계마다 적 체력에 곱해지는 값. 1.2면 30초마다 1.2배씩 누적된다.")]
        [Min(1f)] public float HpMultiplierPerStep = 1.2f;

        [Tooltip("한 단계마다 스폰 간격에 곱해지는 값. 0.9면 10%씩 빨라진다.")]
        [Range(0.5f, 1f)] public float IntervalMultiplierPerStep = 0.9f;

        [Min(0.05f)] public float StartSpawnInterval = 1f;
        [Min(0.05f)] public float MinSpawnInterval = 0.15f;

        [Min(1)] public int BatchSize = 3;
        [Min(1)] public int MaxAliveEnemies = 140;

        [Tooltip("두 번째 스폰 구역이 열리는 시각(초). 시작은 항상 1개다.")]
        [Min(1f)] public float FirstZoneOpenSeconds = 30f;

        [Tooltip("그 뒤로 구역이 하나씩 더 열리는 주기(초).")]
        [Min(1f)] public float ZoneOpenIntervalSeconds = 60f;

        public EndlessEnemy[] Enemies;
    }

    public Wave[] Waves;

    [Header("무한 모드")]
    public EndlessConfig Endless = new EndlessConfig();

    public int Count => Waves != null ? Waves.Length : 0;

    /// <summary>waveNumber는 1부터. 범위를 넘으면 마지막 웨이브를 돌려준다.</summary>
    public Wave Get(int waveNumber)
    {
        if (Waves == null || Waves.Length == 0) return null;

        int index = Mathf.Clamp(waveNumber - 1, 0, Waves.Length - 1);
        return Waves[index];
    }
}
