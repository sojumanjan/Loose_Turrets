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
        [Header("0단계 기준점 — 마지막 웨이브를 그대로 깔고 시작한다")]
        [Tooltip("켜면 마지막 웨이브의 스폰 간격 / 묶음 / 체력 배율을 그대로 0단계로 쓴다. " +
                 "웨이브를 다시 밸런싱해도 무한 모드가 알아서 따라온다. 끄면 아래 세 값을 쓴다. " +
                 "스폰 구역 수는 상속하지 않고 언제나 StartZoneCount 를 쓴다.")]
        public bool InheritLastWave = true;

        [Tooltip("InheritLastWave가 꺼져 있을 때만 쓰인다.")]
        [Min(0.05f)] public float StartSpawnInterval = 0.6f;

        [Tooltip("InheritLastWave가 꺼져 있을 때만 쓰인다.")]
        [Min(1)] public int StartBatchSize = 6;

        [Tooltip("InheritLastWave가 꺼져 있을 때만 쓰인다. 0단계의 적 체력 배율.")]
        [Min(0.1f)] public float StartHpMultiplier = 2f;

        [Header("난이도 상승 (한 단계 = StepSeconds)")]
        [Tooltip("난이도가 한 칸 오르는 주기(초).")]
        [Min(1f)] public float StepSeconds = 30f;

        [Tooltip("한 단계마다 적 체력에 곱해지는 값. 1.2면 30초마다 1.2배씩 적금처럼 누적된다.")]
        [Min(1f)] public float HpMultiplierPerStep = 1.2f;

        [Tooltip("한 단계마다 스폰 간격에서 빼는 초. 곱셈이 아니라 뺄셈이다.")]
        [Min(0f)] public float IntervalDecreasePerStep = 0.05f;

        [Tooltip("스폰 간격 하한. 여기 닿으면 더는 빨라지지 않는다.")]
        [Min(0.05f)] public float MinSpawnInterval = 0.4f;

        [Tooltip("한 단계마다 한 번에 나오는 적 수에 더하는 값.")]
        [Min(0)] public int BatchIncreasePerStep = 1;

        [Tooltip("한 번에 나오는 적 수의 상한.")]
        [Min(1)] public int MaxBatchSize = 10;

        [Header("스폰 구역")]
        [Tooltip("0단계에 열려 있는 구역 수.")]
        [Range(1, 8)] public int StartZoneCount = 1;

        [Tooltip("한 단계마다 새로 열리는 구역 수. 8개가 되면 멈춘다.")]
        [Min(0)] public int ZoneOpenPerStep = 1;

        [Header("공통")]
        [Tooltip("동시에 살아있을 수 있는 최대 적 수. 마지막 웨이브 값보다 작으면 그쪽이 이긴다.")]
        [Min(1)] public int MaxAliveEnemies = 140;

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
