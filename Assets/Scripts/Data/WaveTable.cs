// 웨이브 진행 데이터. 한 행이 웨이브 하나이며, 그 웨이브의 모든 밸런싱 값이 한 칸 안에 들어있다.
// TSV의 w_<적id> 열이 Enemies 배열의 가중치로 들어온다.
// 표를 다 쓴 뒤로는 Blocks(5웨이브 단위)가 이어받아 웨이브 번호만 계속 올라간다. 끝나는 웨이브는 없다.

using System;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Tooltip("이 웨이브가 끝난 뒤 쉬는 시간(초).")]
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

    /// <summary>
    /// 표를 다 쓴 뒤의 웨이브를 5개씩 묶은 덩어리. 덩어리마다 모든 값을 처음부터 직접 적는다.
    /// 앞 덩어리에서 물려받는 것이 없으므로, 한 구간을 손대도 다른 구간이 흔들리지 않는다.
    /// </summary>
    [Serializable]
    public class WaveBlock
    {
        [Tooltip("인스펙터에서 알아보기 위한 이름. 게임에는 안 쓰인다.")]
        public string Label = "6~10";

        [Header("블록 첫 웨이브의 값")]
        [Tooltip("이 블록의 웨이브 하나가 지속되는 시간(초). 블록 안에서는 안 변한다.")]
        [Min(1f)] public float Duration = 25f;

        [Tooltip("웨이브가 끝난 뒤 쉬는 시간(초). 블록 안에서는 안 변한다.")]
        [Min(0f)] public float BreakAfter = 5f;

        [Tooltip("블록 첫 웨이브의 스폰 주기(초).")]
        [Min(0.05f)] public float SpawnInterval = 0.7f;

        [Tooltip("블록 첫 웨이브에서 한 번에 나오는 마리 수.")]
        [Min(1)] public int BatchSize = 6;

        [Tooltip("블록 첫 웨이브의 동시 생존 상한.")]
        [Min(1)] public int MaxAliveEnemies = 150;

        [Tooltip("블록 첫 웨이브의 적 체력 배율. 앞 블록에서 이어받지 않고 이 값에서 시작한다.")]
        [Min(0.1f)] public float HpMultiplier = 2f;

        [Header("블록 안에서 웨이브가 하나 오를 때마다")]
        [Tooltip("체력 배율에 곱해지는 값. 1.13이면 블록 안에서 웨이브마다 1.13배씩 누적된다.")]
        [Min(1f)] public float HpMultiplierPerWave = 1.13f;

        [Tooltip("스폰 주기에서 빼는 초. 곱셈이 아니라 뺄셈이다.")]
        [Min(0f)] public float IntervalDecreasePerWave = 0.05f;

        [Tooltip("한 번에 나오는 마리 수에 더하는 값.")]
        [Min(0)] public int BatchIncreasePerWave = 1;

        [Tooltip("동시 생존 상한에 더하는 값.")]
        [Min(0)] public int MaxAliveIncreasePerWave = 10;

        [Header("이 블록의 한계")]
        [Tooltip("스폰 주기 하한. 이 블록 안에서는 여기보다 빨라지지 않는다.")]
        [Min(0.05f)] public float MinSpawnInterval = 0.4f;

        [Tooltip("한 번에 나오는 마리 수 상한. 이 블록 안에서는 여기보다 많아지지 않는다.")]
        [Min(1)] public int MaxBatchSize = 12;

        [Header("스폰 구역")]
        [Tooltip("블록 안 웨이브별로 열려 있을 구역 수. 앞에서부터 첫 웨이브, 둘째 웨이브... 순서다. " +
                 "칸이 모자라면 마지막 값을 계속 쓴다. 열린 구역은 항상 둘레에서 이어진 한 덩어리다.")]
        public int[] ZoneCountPerWave = { 2, 3, 3, 4, 4 };

        [Header("적 등장 비율")]
        [Tooltip("이 블록에서 나올 적 종류와 가중치. 블록 안에서는 안 변한다.")]
        public EnemyWeight[] Enemies;
    }


    /// <summary>보스가 나오는 웨이브 하나. 체력도 문구도 웨이브마다 다르게 줄 수 있다.</summary>
    [Serializable]
    public class BossWave
    {
        [Tooltip("보스가 나올 웨이브 번호.")]
        [Min(1)] public int Wave = 10;

        [Tooltip("이 웨이브 보스의 최대 체력. 0이면 프리팹(또는 EnemyDef) 값을 그대로 쓴다.")]
        [Min(0f)] public float MaxHp;

        [Header("보상 — 보스마다 따로 준다")]
        [Tooltip("잡으면 포탑을 종류당 하나씩 더 놓을 수 있게 된다.")]
        public bool GrantsExtraTurretSlot;

        [Tooltip("잡으면 두 번째 특수 강화 카드가 열린다.")]
        public bool UnlocksSpecial2;

        [Tooltip("잡으면 세 번째 특수 강화 카드가 열린다. 두 번째와 따로 관리하므로, " +
                 "두 번째만 열린 동안에는 세 번째가 조건을 채워도 나오지 않는다.")]
        public bool UnlocksSpecial3;

        [Header("문구")]
        [Tooltip("보스가 오기 전 쉬는 시간에 화면 중앙에 뜨는 경고.")]
        [TextArea(1, 2)] public string WarningMessage = "아주 강력한 적이 다가오고 있습니다!!";

        [Tooltip("보스가 나오는 순간의 배너.")]
        public string AppearBanner = "보스 등장";

        public Color AppearBannerColor = new Color(1f, 0.35f, 0.35f);

        [Tooltip("체력 절반을 넘겨 2페이즈에 들어갈 때의 배너.")]
        public string Phase2Banner = "보스가 각성했다!";

        public Color Phase2BannerColor = new Color(1f, 0.4f, 0.35f);

        [Tooltip("보스를 잡았을 때의 배너.")]
        public string DefeatBanner = "포탑을 하나씩 더 놓을 수 있다!";

        public Color DefeatBannerColor = new Color(1f, 0.86f, 0.36f);

        [Tooltip("처치 문구가 화면에 머무는 시간(초). 해금을 알리는 문구라 웨이브 배너보다 길게 둔다. " +
                 "0이면 GameHud의 기본 유지 시간을 쓴다.")]
        [Min(0f)] public float DefeatBannerHold = 2.5f;
    }

    public Wave[] Waves;

    [Header("표를 다 쓴 뒤의 웨이브 — 5개씩 묶은 블록")]
    [Tooltip("블록 하나가 몇 웨이브를 담당할지. 5면 6~10 / 11~15 / 16~20 ... 순서로 끊어진다.")]
    [Min(1)] public int BlockWaveCount = 5;

    [Tooltip("표 다음 웨이브부터 순서대로 적용된다. 마지막 블록은 그 뒤 웨이브에도 계속 쓰이고, " +
             "증가분이 멈추지 않고 계속 쌓여서 끝없이 어려워진다.")]
    public WaveBlock[] Blocks;

    /// <summary>이 웨이브를 담당하는 블록과, 그 블록 안에서 몇 번째 웨이브인지. 블록이 없으면 null.</summary>
    public WaveBlock GetBlock(int waveNumber, out int stepInBlock)
    {
        stepInBlock = 0;
        if (Blocks == null || Blocks.Length == 0) return null;

        int size = Mathf.Max(1, BlockWaveCount);
        int offset = Mathf.Max(0, waveNumber - Count - 1);   // 표 다음 웨이브가 0

        int index = offset / size;

        // 마지막 블록을 넘어서면 그 블록을 계속 쓴다. 걸음 수는 멈추지 않고 이어져 계속 어려워진다.
        if (index >= Blocks.Length)
        {
            index = Blocks.Length - 1;
            stepInBlock = offset - index * size;
        }
        else
        {
            stepInBlock = offset % size;
        }

        return Blocks[index];
    }

    [Header("보스 웨이브")]
    [Tooltip("보스 프리팹. 비우면 보스 웨이브가 통째로 꺼진다.")]
    public BossEnemy BossPrefab;

    [Tooltip("보스 웨이브 동안 일반 적 스폰을 멈출지. 끄면 보스와 잡몹이 같이 나온다.")]
    public bool BossStopsNormalSpawns = true;

    [Tooltip("보스가 나오는 웨이브들. 이 웨이브는 시간이 아니라 보스의 죽음으로 끝난다. " +
             "비워두면 보스가 안 나온다. 순서는 상관없다.")]
    public BossWave[] BossWaves;

    [Header("보스 잡몹 — 후반에 보스가 일반 적으로 섞여 나온다")]
    [Tooltip("잡몹으로 섞여 나올 보스의 EnemyDef. 비우면 이 기능이 통째로 꺼진다. " +
             "보스 웨이브의 보스와는 다른 에셋이다. 각성하지 않고 그냥 걸어온다.")]
    public EnemyDef BossMobDef;

    [Tooltip("이 웨이브부터 섞여 나오기 시작한다. 그 전 웨이브에서는 한 마리도 안 나온다.")]
    [Min(1)] public int BossMobStartWave = 26;

    [Tooltip("적 한 마리를 뽑을 때 이 확률로 보스 잡몹이 대신 나온다. " +
             "0.02면 50마리에 한 마리꼴. 한 번 스폰에 12마리씩 나오므로 조금만 올려도 확 늘어난다.")]
    [Range(0f, 1f)] public float BossMobChance = 0.02f;

    /// <summary>
    /// 지금 뽑는 한 마리를 보스 잡몹으로 바꿀지. 마리마다 따로 굴리므로 한 번에 여럿이 나올 수도 있다.
    /// 시작 웨이브 전이거나 에셋이 안 꽂혀 있으면 언제나 false다.
    /// </summary>
    public bool RollBossMob(int waveNumber)
    {
        if (BossMobDef == null || BossMobDef.Prefab == null) return false;
        if (waveNumber < BossMobStartWave) return false;

        return UnityEngine.Random.value < BossMobChance;
    }

    /// <summary>이 웨이브의 보스 설정. 보스 웨이브가 아니거나 프리팹이 없으면 null.</summary>
    public BossWave GetBossWave(int waveNumber)
    {
        if (BossPrefab == null || BossWaves == null) return null;

        for (int i = 0; i < BossWaves.Length; i++)
            if (BossWaves[i] != null && BossWaves[i].Wave == waveNumber) return BossWaves[i];

        return null;
    }

    public int Count => Waves != null ? Waves.Length : 0;

    /// <summary>waveNumber는 1부터. 범위를 넘으면 마지막 웨이브를 돌려준다.</summary>
    public Wave Get(int waveNumber)
    {
        if (Waves == null || Waves.Length == 0) return null;

        int index = Mathf.Clamp(waveNumber - 1, 0, Waves.Length - 1);
        return Waves[index];
    }
}
