// 웨이브별로 완전히 독립된 설정에 따라 적을 스폰한다.
// 한 웨이브의 길이 / 스폰 간격 / 한 번에 나오는 수 / 동시 생존 상한 / 적 종류별 확률이 전부 배열 한 칸 안에 들어있다.
// 진행(웨이브 전환)은 GameManager가 BeginWave / StopSpawning으로 지시한다.
// 스폰 위치는 맵을 둘러싼 사각형 둘레를 8구역으로 나눈 것이며, 웨이브마다 어느 구역에서 나올지 고를 수 있다.

using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("웨이브 데이터 (밸런싱은 이 에셋 / 엑셀에서)")]
    [SerializeField] private WaveTable waveTable;

    [Header("스폰 사각형 (화면 밖이어야 함)")]
    [Tooltip("적이 나오는 사각형 둘레의 절반 크기. 이 둘레를 8구역으로 나눈다.")]
    [SerializeField] private float ringHalfX = 17f;
    [SerializeField] private float ringHalfZ = 11f;

    [Header("기즈모")]
    [SerializeField] private bool drawZoneGizmos = true;

    [Header("디버그")]
    [Tooltip("무한 모드 단계가 오를 때마다 체력/간격/묶음/구역 수를 콘솔에 찍는다. 밸런싱할 때 켠다.")]
    [SerializeField] private bool logEndlessSteps = true;

    private int currentWaveIndex = -1;
    private bool spawning;
    private float nextSpawnTime;

    // ---- 무한 모드 ----
    private bool endless;
    private float endlessElapsed;
    private int endlessStep = -1;

    // 무한 모드 0단계의 기준값. BeginEndless에서 마지막 웨이브를 보고 정한다.
    private float endlessStartInterval = 0.6f;
    private int endlessStartBatch = 6;
    private int endlessMaxAlive = 140;
    private float endlessStartHp = 2f;

    // 단계가 오를 때 잠깐 스폰을 쉬는 구간. 이 시각까지는 적을 내보내지 않는다.
    private float endlessGraceUntil;

    // 쉬는 시간이 끝나는 순간을 한 번만 잡으려고 들고 있는다. 매 프레임 소리가 나면 안 된다.
    private bool inEndlessGrace;

    // 열린 구역은 둘레에서 이어진 한 덩어리로 관리한다. 시작 번호와 길이만 있으면 된다.
    private int arcStart = -1;
    private int arcLength;

    // 구역을 다시 뽑은 단계 번호. TickEndless가 매 프레임 도므로 단계마다 한 번만 굴리려고 들고 있는다.
    private int zonesRolledForStep = -1;

    private readonly List<int> openZones = new List<int>(ZoneCount);
    private readonly List<float> endlessWeights = new List<float>(8);

    public bool IsEndless => endless;
    public int EndlessStep => Mathf.Max(0, endlessStep);
    public float EndlessElapsed => endlessElapsed;
    public int OpenZoneCount => openZones.Count;

    /// <summary>스폰 구역 개수. 사각형 네 변을 반씩 쪼개 8개.</summary>
    public const int ZoneCount = SpawnZones.Count;

    public int WaveCount => waveTable != null ? waveTable.Count : 0;
    public int AliveCount => EnemyRegistry.Count;

    /// <summary>현재 진행 중인 웨이브 번호(1부터). 진행 중이 아니면 0.</summary>
    public int CurrentWaveNumber => currentWaveIndex + 1;

    private WaveTable.Wave CurrentWave => GetWave(CurrentWaveNumber);

    private void OnEnable()
    {
        // static 리스트는 씬을 다시 로드해도 남으므로 여기서 비운다.
        EnemyRegistry.Clear();

        spawning = false;
        currentWaveIndex = -1;
        endless = false;
        endlessElapsed = 0f;
        endlessStep = -1;
        endlessGraceUntil = 0f;
        inEndlessGrace = false;
        arcStart = -1;
        arcLength = 0;
        zonesRolledForStep = -1;
        openZones.Clear();
        EnemyBase.ResetHpMultiplier();
    }

    // ---------------- GameManager가 부르는 API ----------------

    /// <summary>waveNumber는 1부터. 없는 번호면 마지막 웨이브 설정을 쓴다.</summary>
    public float GetWaveDuration(int waveNumber)
    {
        WaveTable.Wave wave = GetWave(waveNumber);
        return wave != null ? wave.Duration : 30f;
    }

    public float GetBreakAfter(int waveNumber)
    {
        WaveTable.Wave wave = GetWave(waveNumber);
        return wave != null ? wave.BreakAfter : 4f;
    }

    public void BeginWave(int waveNumber)
    {
        currentWaveIndex = Mathf.Clamp(waveNumber - 1, 0, Mathf.Max(0, WaveCount - 1));
        spawning = true;

        // 웨이브가 시작되면 기다리지 않고 바로 첫 무리를 낸다.
        nextSpawnTime = Time.time;

        WaveTable.Wave wave = CurrentWave;
        EnemyBase.SetHpMultiplier(wave != null ? wave.HpMultiplier : 1f);
    }

    public void StopSpawning()
    {
        spawning = false;
    }

    /// <summary>무한 모드를 시작한다. 웨이브 표는 더 이상 쓰지 않는다.</summary>
    public void BeginEndless()
    {
        endless = true;
        spawning = true;
        endlessElapsed = 0f;
        endlessStep = -1;
        endlessGraceUntil = 0f;
        inEndlessGrace = false;

        // 0단계 기준점을 먼저 정해야 아래 계산이 전부 맞는다.
        ResolveEndlessStart();

        arcStart = -1;
        arcLength = 0;
        openZones.Clear();

        RollZones(ZoneCountForStep(EndlessConfig, 0));
        zonesRolledForStep = 0;

        // 시작 구역은 한 번에 모아서 알린다.
        if (SpawnZoneWarning.Instance != null) SpawnZoneWarning.Instance.Warn(openZones);

        nextSpawnTime = Time.time;
        ApplyEndlessStep(0);
    }

    /// <summary>
    /// 무한 모드 0단계의 기준값. 기본은 마지막 웨이브를 그대로 깔고 시작하는 것이라,
    /// 무한 모드가 마지막 웨이브보다 약해지는 일이 없다.
    /// </summary>
    private void ResolveEndlessStart()
    {
        WaveTable.EndlessConfig cfg = EndlessConfig;
        if (cfg == null) return;

        WaveTable.Wave last = waveTable != null && waveTable.Count > 0 ? waveTable.Get(waveTable.Count) : null;

        if (cfg.InheritLastWave && last != null)
        {
            endlessStartInterval = last.SpawnInterval;
            endlessStartBatch = last.BatchSize;
            endlessStartHp = last.HpMultiplier;

            // 무한 모드가 마지막 웨이브보다 좁아지면 안 된다.
            endlessMaxAlive = Mathf.Max(last.MaxAliveEnemies, cfg.MaxAliveEnemies);
            return;
        }

        endlessStartInterval = cfg.StartSpawnInterval;
        endlessStartBatch = cfg.StartBatchSize;
        endlessStartHp = cfg.StartHpMultiplier;
        endlessMaxAlive = cfg.MaxAliveEnemies;
    }

    private WaveTable.EndlessConfig EndlessConfig =>
        waveTable != null && waveTable.Endless != null ? waveTable.Endless : null;

    private void TickEndless()
    {
        WaveTable.EndlessConfig cfg = EndlessConfig;
        if (cfg == null) return;

        endlessElapsed += Time.deltaTime;

        int step = Mathf.FloorToInt(endlessElapsed / Mathf.Max(1f, cfg.StepSeconds));
        if (step != endlessStep)
        {
            ApplyEndlessStep(step);

            // 단계가 오를 때마다 잠깐 숨을 돌린다. 웨이브 사이 쉬는 시간과 같은 역할이다.
            // 0단계는 무한 모드에 막 들어온 참이라 건너뛴다.
            if (step > 0 && cfg.StepBreakSeconds > 0f)
                endlessGraceUntil = Time.time + cfg.StepBreakSeconds;
        }

        OpenZonesForStep(cfg);

        // 쉬는 동안에는 스폰만 멈춘다. 난이도 상승과 구역 경고는 그대로 흘러간다.
        if (Time.time < endlessGraceUntil)
        {
            inEndlessGrace = true;

            // 쉬는 시간이 끝나는 순간 곧바로 첫 무리가 나오도록 맞춰둔다.
            nextSpawnTime = endlessGraceUntil;
            return;
        }

        // 유예가 막 끝났다. 경고음이 사그라든 뒤 이어서 울리는 자리라 웨이브 시작과 역할이 같다.
        if (inEndlessGrace)
        {
            inEndlessGrace = false;
            SfxManager.Play(SfxManager.Common?.WaveStart);
        }

        if (Time.time < nextSpawnTime) return;

        // 스폰 간격은 곱셈이 아니라 뺄셈으로 줄어든다. 하한에 닿으면 멈춘다.
        float interval = Mathf.Max(cfg.MinSpawnInterval,
            endlessStartInterval - cfg.IntervalDecreasePerStep * endlessStep);

        nextSpawnTime = Time.time + interval;

        // 한 번에 나오는 수는 단계마다 더해지고, 상한에서 멈춘다.
        int batch = Mathf.Min(cfg.MaxBatchSize, endlessStartBatch + cfg.BatchIncreasePerStep * endlessStep);

        for (int i = 0; i < batch; i++)
        {
            if (EnemyRegistry.Count >= EffectiveMaxAlive(cfg)) return;
            SpawnEndlessOne(cfg);
        }
    }

    /// <summary>체력 배율처럼 단계가 바뀔 때만 갱신하면 되는 것들.</summary>
    private void ApplyEndlessStep(int step)
    {
        WaveTable.EndlessConfig cfg = EndlessConfig;
        if (cfg == null) return;

        endlessStep = Mathf.Max(0, step);

        // 마지막 웨이브 배율에서 출발해 적금처럼 누적된다. 30초마다 현재 체력의 1.2배.
        EnemyBase.SetHpMultiplier(endlessStartHp * Mathf.Pow(cfg.HpMultiplierPerStep, endlessStep));

        if (!logEndlessSteps) return;

        float interval = Mathf.Max(cfg.MinSpawnInterval,
            endlessStartInterval - cfg.IntervalDecreasePerStep * endlessStep);
        int batch = Mathf.Min(cfg.MaxBatchSize, endlessStartBatch + cfg.BatchIncreasePerStep * endlessStep);
        int zoneTarget = ZoneCountForStep(cfg, endlessStep);

        Debug.Log($"[무한] {endlessStep + 1}단계 ({endlessElapsed:0}초)  체력 x{EnemyBase.HpMultiplier:0.00}"
                  + $"  간격 {interval:0.00}  묶음 {batch}  구역 {openZones.Count}→{zoneTarget}"
                  + $"  동시상한 {EffectiveMaxAlive(cfg)}");
    }

    /// <summary>단계가 오를 때마다 스폰 구역이 하나씩 열린다. 8개가 되면 멈춘다.</summary>
    /// <summary>
    /// 단계가 바뀔 때마다 구역을 새로 뽑는다. 개수가 3->3 처럼 그대로여도 자리는 반드시 옮긴다.
    /// 자리가 고정되면 한 번 세운 방어선이 끝까지 통해서 유예 시간을 쓸 이유가 없어진다.
    /// </summary>
    private void OpenZonesForStep(WaveTable.EndlessConfig cfg)
    {
        if (zonesRolledForStep == endlessStep) return;
        zonesRolledForStep = endlessStep;

        RollZones(ZoneCountForStep(cfg, endlessStep));

        // 개수가 같아도 자리가 바뀌었으니 매번 알린다.
        if (SpawnZoneWarning.Instance != null) SpawnZoneWarning.Instance.Warn(openZones);
    }

    /// <summary>아직 안 열린 구역 중에서 무작위로 골라 target 개가 될 때까지 연다. 하나라도 열었으면 true.</summary>
    /// <summary>
    /// 열린 구역을 둘레에서 이어진 한 덩어리(호)로 새로 뽑는다.
    /// 무작위로 흩뿌리면 3|6|8 처럼 사방에서 찔끔찔끔 들어와 방어선을 세울 수가 없어서 항상 붙여 놓는다.
    /// 시작점은 직전과 반드시 다른 자리로 고른다.
    /// </summary>
    private void RollZones(int count)
    {
        count = Mathf.Clamp(count, 1, ZoneCount);

        // 전 구역이 열리면 시작점은 의미가 없다.
        if (count >= ZoneCount)
        {
            arcStart = 1;
            arcLength = ZoneCount;
            RebuildOpenZones();
            return;
        }

        if (arcStart < 0)
        {
            arcStart = UnityEngine.Random.Range(1, ZoneCount + 1);
        }
        else
        {
            // 1~7칸을 돌려서 반드시 다른 시작점이 나오게 한다.
            arcStart = WrapZone(arcStart + UnityEngine.Random.Range(1, ZoneCount));
        }

        arcLength = count;
        RebuildOpenZones();
    }

    /// <summary>이 단계의 동시 생존 상한. 0단계 기준값에서 단계마다 늘어난다.</summary>
    private int EffectiveMaxAlive(WaveTable.EndlessConfig cfg)
    {
        if (cfg == null) return endlessMaxAlive;
        return endlessMaxAlive + cfg.MaxAliveIncreasePerStep * endlessStep;
    }

    /// <summary>이 단계에 열려 있어야 할 구역 수. 표를 넘어선 단계는 마지막 값을 그대로 쓴다.</summary>
    private static int ZoneCountForStep(WaveTable.EndlessConfig cfg, int step)
    {
        int[] table = cfg != null ? cfg.ZoneCountPerStep : null;
        if (table == null || table.Length == 0) return 1;

        int index = Mathf.Clamp(step, 0, table.Length - 1);
        return Mathf.Clamp(table[index], 1, ZoneCount);
    }

    /// <summary>1~8을 벗어난 번호를 둘레를 따라 되감는다. 0이면 8, 9면 1.</summary>
    private static int WrapZone(int zone)
    {
        return ((zone - 1) % ZoneCount + ZoneCount) % ZoneCount + 1;
    }

    private void RebuildOpenZones()
    {
        openZones.Clear();
        for (int i = 0; i < arcLength; i++) openZones.Add(WrapZone(arcStart + i));
    }

    private void SpawnEndlessOne(WaveTable.EndlessConfig cfg)
    {
        EnemyBase prefab = PickEndlessPrefab(cfg);
        if (prefab == null) return;

        int zone = openZones.Count > 0 ? openZones[UnityEngine.Random.Range(0, openZones.Count)] : 1;

        Vector3 a, b;
        GetZoneSegment(zone, out a, out b);

        Instantiate(prefab, Vector3.Lerp(a, b, UnityEngine.Random.value), Quaternion.identity);
    }

    /// <summary>단계가 오를수록 WeightPerStep 이 붙은 적(탱크 등)의 비중이 커진다.</summary>
    private EnemyBase PickEndlessPrefab(WaveTable.EndlessConfig cfg)
    {
        if (cfg.Enemies == null || cfg.Enemies.Length == 0) return null;

        endlessWeights.Clear();
        float total = 0f;

        for (int i = 0; i < cfg.Enemies.Length; i++)
        {
            WaveTable.EndlessEnemy entry = cfg.Enemies[i];

            float w = entry == null || entry.Def == null || entry.Def.Prefab == null
                ? 0f
                : Mathf.Max(0f, entry.Weight + entry.WeightPerStep * endlessStep);

            endlessWeights.Add(w);
            total += w;
        }

        if (total <= 0f) return null;

        float roll = UnityEngine.Random.value * total;

        for (int i = 0; i < endlessWeights.Count; i++)
        {
            roll -= endlessWeights[i];
            if (roll <= 0f) return cfg.Enemies[i].Def.Prefab;
        }

        for (int i = endlessWeights.Count - 1; i >= 0; i--)
        {
            if (endlessWeights[i] > 0f) return cfg.Enemies[i].Def.Prefab;
        }

        return null;
    }

    // ---------------- 스폰 ----------------

    private void Update()
    {
        if (!spawning) return;

        if (endless)
        {
            PlayerController alive = PlayerController.Instance;
            if (alive == null || !alive.IsAlive) return;

            TickEndless();
            return;
        }

        WaveTable.Wave wave = CurrentWave;
        if (wave == null) return;

        PlayerController player = PlayerController.Instance;
        if (player == null || !player.IsAlive) return;

        if (Time.time < nextSpawnTime) return;

        nextSpawnTime = Time.time + Mathf.Max(0.05f, wave.SpawnInterval);
        SpawnBatch(wave);
    }

    private void SpawnBatch(WaveTable.Wave wave)
    {
        for (int i = 0; i < wave.BatchSize; i++)
        {
            if (EnemyRegistry.Count >= wave.MaxAliveEnemies) return;
            SpawnOne(wave);
        }
    }

    private void SpawnOne(WaveTable.Wave wave)
    {
        EnemyBase prefab = PickPrefab(wave);
        if (prefab == null) return;

        Instantiate(prefab, PickSpawnPosition(wave), Quaternion.identity);
    }

    // ---------------- 스폰 구역 ----------------

    /// <summary>이 웨이브가 허용한 구역 중 하나를 골라, 그 선분 위의 임의 지점을 돌려준다.</summary>
    private Vector3 PickSpawnPosition(WaveTable.Wave wave)
    {
        Vector3 a, b;
        GetZoneSegment(PickZone(wave), out a, out b);

        return Vector3.Lerp(a, b, UnityEngine.Random.value);
    }

    private static int PickZone(WaveTable.Wave wave)
    {
        int[] zones = wave != null ? wave.SpawnZones : null;

        // 지정이 없으면 8구역 전부에서 나온다.
        if (zones == null || zones.Length == 0) return UnityEngine.Random.Range(1, ZoneCount + 1);

        int picked = zones[UnityEngine.Random.Range(0, zones.Length)];
        return Mathf.Clamp(picked, 1, ZoneCount);
    }

    /// <summary>구역 번호(1~8)에 해당하는 스폰 선분. 규칙은 SpawnZones 가 들고 있다.</summary>
    public void GetZoneSegment(int zone, out Vector3 a, out Vector3 b)
    {
        SpawnZones.GetSegment(zone, new Vector2(ringHalfX, ringHalfZ), out a, out b);
    }

    /// <summary>이 웨이브가 쓰는 구역 목록. 비어 있으면 전 구역이라는 뜻이다.</summary>
    public int[] GetWaveZones(int waveNumber)
    {
        WaveTable.Wave wave = GetWave(waveNumber);
        return wave != null ? wave.SpawnZones : null;
    }


    /// <summary>이 웨이브의 가중치로 적 종류를 하나 뽑는다.</summary>
    private static EnemyBase PickPrefab(WaveTable.Wave wave)
    {
        if (wave.Enemies == null || wave.Enemies.Length == 0) return null;

        float total = 0f;
        for (int i = 0; i < wave.Enemies.Length; i++)
        {
            if (IsUsable(wave.Enemies[i])) total += wave.Enemies[i].Weight;
        }

        if (total <= 0f) return null;

        float roll = UnityEngine.Random.value * total;

        for (int i = 0; i < wave.Enemies.Length; i++)
        {
            if (!IsUsable(wave.Enemies[i])) continue;

            roll -= wave.Enemies[i].Weight;
            if (roll <= 0f) return wave.Enemies[i].Def.Prefab;
        }

        // 부동소수점 오차로 다 빠져나온 경우 마지막 후보를 준다.
        for (int i = wave.Enemies.Length - 1; i >= 0; i--)
        {
            if (IsUsable(wave.Enemies[i])) return wave.Enemies[i].Def.Prefab;
        }

        return null;
    }

    private static bool IsUsable(WaveTable.EnemyWeight entry)
    {
        return entry != null && entry.Def != null && entry.Def.Prefab != null && entry.Weight > 0f;
    }

    /// <summary>waveNumber는 1부터. 범위를 넘으면 마지막 웨이브를 돌려준다.</summary>
    private WaveTable.Wave GetWave(int waveNumber)
    {
        return waveTable != null ? waveTable.Get(waveNumber) : null;
    }

#if UNITY_EDITOR
    // 구역 번호를 눈으로 확인할 수 있게 씬 뷰에 그린다. Handles는 에디터 전용이라 통째로 감싼다.
    private void OnDrawGizmosSelected()
    {
        if (!drawZoneGizmos) return;

        for (int zone = 1; zone <= ZoneCount; zone++)
        {
            Vector3 a, b;
            GetZoneSegment(zone, out a, out b);

            // 짝수/홀수로 색을 번갈아 구역 경계가 보이게 한다.
            Gizmos.color = zone % 2 == 0
                ? new Color(1f, 0.55f, 0.3f, 0.9f)
                : new Color(0.4f, 0.85f, 1f, 0.9f);

            Gizmos.DrawLine(a, b);
            Gizmos.DrawSphere(a, 0.25f);

            Vector3 mid = (a + b) * 0.5f;
            UnityEditor.Handles.Label(mid + Vector3.up * 0.6f, zone.ToString());
        }
    }
#endif
}
