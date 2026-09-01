// 웨이브별로 완전히 독립된 설정에 따라 적을 스폰한다.
// 한 웨이브의 길이 / 스폰 간격 / 한 번에 나오는 수 / 동시 생존 상한 / 적 종류별 확률이 전부 배열 한 칸 안에 들어있다.
// 표를 다 쓴 뒤로는 WaveTable.Extended 설정이 이어받아 웨이브 번호만 계속 올라간다. 끝나는 웨이브는 없다.
// 진행(웨이브 전환)은 표 안이든 밖이든 언제나 GameManager가 BeginWave / StopSpawning으로 지시한다.
// 스폰 위치는 맵을 둘러싼 사각형 둘레를 8구역으로 나눈 것이며, 웨이브마다 어느 구역에서 나올지 고를 수 있다.

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
    [Tooltip("표를 넘어선 웨이브가 시작될 때마다 체력/간격/묶음/구역 수를 콘솔에 찍는다. 밸런싱할 때 켠다.")]
    [SerializeField] private bool logExtendedWaves = true;

    private int currentWaveNumber;
    private bool spawning;
    private float nextSpawnTime;

    // ---- 표를 넘어선 웨이브 ----
    // 출발점. 표의 마지막 웨이브를 보고 한 판에 한 번만 정한다.
    private float extendedStartInterval = 0.6f;
    private int extendedStartBatch = 6;
    private int extendedMaxAlive = 140;
    private float extendedStartHp = 2f;
    private bool extendedResolved;

    // 열린 구역은 둘레에서 이어진 한 덩어리로 관리한다. 시작 번호와 길이만 있으면 된다.
    private int arcStart = -1;
    private int arcLength;

    // 구역을 뽑아둔 웨이브 번호. 쉬는 시간의 경고와 실제 스폰이 같은 답을 보게 한 번만 굴린다.
    private int zonesRolledForWave = -1;

    private readonly List<int> openZones = new List<int>(ZoneCount);
    private readonly List<float> extendedWeights = new List<float>(8);

    public int OpenZoneCount => openZones.Count;

    /// <summary>스폰 구역 개수. 사각형 네 변을 반씩 쪼개 8개.</summary>
    public const int ZoneCount = SpawnZones.Count;

    /// <summary>표에 적혀 있는 웨이브 수. 이보다 큰 번호는 Extended 설정으로 굴러간다.</summary>
    public int TableWaveCount => waveTable != null ? waveTable.Count : 0;

    public int AliveCount => EnemyRegistry.Count;

    /// <summary>현재 진행 중인 웨이브 번호(1부터). 시작 전이면 0.</summary>
    public int CurrentWaveNumber => currentWaveNumber;

    /// <summary>표를 넘어선 웨이브인가. 여기서부터는 Extended 설정이 굴린다.</summary>
    public bool IsExtendedWave(int waveNumber) => TableWaveCount > 0 && waveNumber > TableWaveCount;

    /// <summary>표를 넘어선 뒤 몇 번째 웨이브인지. 첫 확장 웨이브가 0이다.</summary>
    private int ExtraStep(int waveNumber) => Mathf.Max(0, waveNumber - TableWaveCount - 1);

    /// <summary>
    /// 확장 웨이브를 두 구간으로 쪼갠 걸음 수. 표가 5웨이브이고 LateWaveStart가 21이면
    /// 6~20은 중반 값으로 14걸음, 21부터는 후반 값으로 한 걸음씩 쌓인다.
    /// 20 -> 21 로 넘어갈 때 이미 후반 증가폭이 적용되므로, 21웨이브부터 체감이 달라진다.
    /// </summary>
    private void SplitSteps(WaveTable.ExtendedConfig cfg, int step, out int midSteps, out int lateSteps)
    {
        // 중반 구간이 몇 걸음인지. 표 다음 웨이브(첫 확장)는 0걸음이므로 -2 를 한다.
        int midCount = cfg != null ? Mathf.Max(0, cfg.LateWaveStart - TableWaveCount - 2) : int.MaxValue;

        midSteps = Mathf.Min(step, midCount);
        lateSteps = Mathf.Max(0, step - midCount);
    }

    private WaveTable.ExtendedConfig ExtendedConfig =>
        waveTable != null && waveTable.Extended != null ? waveTable.Extended : null;

    private void OnEnable()
    {
        // static 리스트는 씬을 다시 로드해도 남으므로 여기서 비운다.
        EnemyRegistry.Clear();

        spawning = false;
        currentWaveNumber = 0;
        extendedResolved = false;
        arcStart = -1;
        arcLength = 0;
        zonesRolledForWave = -1;
        openZones.Clear();
        EnemyBase.ResetHpMultiplier();
    }

    // ---------------- GameManager가 부르는 API ----------------

    /// <summary>waveNumber는 1부터. 표를 넘어선 번호는 Extended 설정의 길이를 쓴다.</summary>
    public float GetWaveDuration(int waveNumber)
    {
        if (IsExtendedWave(waveNumber))
        {
            WaveTable.ExtendedConfig cfg = ExtendedConfig;
            return cfg != null ? Mathf.Max(1f, cfg.WaveSeconds) : 30f;
        }

        WaveTable.Wave wave = GetWave(waveNumber);
        return wave != null ? wave.Duration : 30f;
    }

    public float GetBreakAfter(int waveNumber)
    {
        if (IsExtendedWave(waveNumber))
        {
            WaveTable.ExtendedConfig cfg = ExtendedConfig;
            return cfg != null ? cfg.WaveBreakSeconds : 4f;
        }

        WaveTable.Wave wave = GetWave(waveNumber);
        return wave != null ? wave.BreakAfter : 4f;
    }

    public void BeginWave(int waveNumber)
    {
        currentWaveNumber = Mathf.Max(1, waveNumber);
        spawning = true;

        // 웨이브가 시작되면 기다리지 않고 바로 첫 무리를 낸다.
        nextSpawnTime = Time.time;

        if (IsExtendedWave(currentWaveNumber))
        {
            BeginExtendedWave();
            return;
        }

        WaveTable.Wave wave = GetWave(currentWaveNumber);
        EnemyBase.SetHpMultiplier(wave != null ? wave.HpMultiplier : 1f);
    }

    public void StopSpawning()
    {
        spawning = false;
    }

    // ---------------- 표를 넘어선 웨이브 ----------------

    private void BeginExtendedWave()
    {
        ResolveExtendedStart();

        WaveTable.ExtendedConfig cfg = ExtendedConfig;
        if (cfg == null) return;

        int step = ExtraStep(currentWaveNumber);

        // 표의 마지막 웨이브 배율에서 출발해 적금처럼 누적된다. 구간마다 이자율이 다르다.
        EnemyBase.SetHpMultiplier(HpMultiplierFor(cfg, step));

        // 쉬는 시간이 0이라 경고를 건너뛴 경우를 대비해 여기서도 확인한다. 이미 뽑았으면 그대로 쓴다.
        EnsureZonesFor(currentWaveNumber);

        LogExtendedWave(cfg, step);
    }

    /// <summary>
    /// 확장 웨이브의 출발점. 기본은 표의 마지막 웨이브를 그대로 깔고 가는 것이라,
    /// 표를 넘어선 순간 갑자기 약해지는 일이 없다. 한 판에 한 번만 정한다.
    /// </summary>
    private void ResolveExtendedStart()
    {
        if (extendedResolved) return;
        extendedResolved = true;

        WaveTable.ExtendedConfig cfg = ExtendedConfig;
        if (cfg == null) return;

        WaveTable.Wave last = waveTable != null && waveTable.Count > 0 ? waveTable.Get(waveTable.Count) : null;

        if (cfg.InheritLastWave && last != null)
        {
            extendedStartInterval = last.SpawnInterval;
            extendedStartBatch = last.BatchSize;
            extendedStartHp = last.HpMultiplier;

            // 표를 넘어선 웨이브가 마지막 웨이브보다 좁아지면 안 된다.
            extendedMaxAlive = Mathf.Max(last.MaxAliveEnemies, cfg.MaxAliveEnemies);
            return;
        }

        extendedStartInterval = cfg.StartSpawnInterval;
        extendedStartBatch = cfg.StartBatchSize;
        extendedStartHp = cfg.StartHpMultiplier;
        extendedMaxAlive = cfg.MaxAliveEnemies;
    }

    /// <summary>체력 배율. 중반 구간까지는 중반 이자율, 그 뒤로는 후반 이자율로 이어 곱한다.</summary>
    private float HpMultiplierFor(WaveTable.ExtendedConfig cfg, int step)
    {
        int mid, late;
        SplitSteps(cfg, step, out mid, out late);

        return extendedStartHp
               * Mathf.Pow(cfg.HpMultiplierPerWave, mid)
               * Mathf.Pow(cfg.LateHpMultiplierPerWave, late);
    }

    private float IntervalFor(WaveTable.ExtendedConfig cfg, int step)
    {
        int mid, late;
        SplitSteps(cfg, step, out mid, out late);

        float drop = cfg.IntervalDecreasePerWave * mid + cfg.LateIntervalDecreasePerWave * late;
        return Mathf.Max(cfg.MinSpawnInterval, extendedStartInterval - drop);
    }

    private int BatchFor(WaveTable.ExtendedConfig cfg, int step)
    {
        int mid, late;
        SplitSteps(cfg, step, out mid, out late);

        int add = cfg.BatchIncreasePerWave * mid + cfg.LateBatchIncreasePerWave * late;
        return Mathf.Min(cfg.MaxBatchSize, extendedStartBatch + add);
    }

    private int MaxAliveFor(WaveTable.ExtendedConfig cfg, int step)
    {
        int mid, late;
        SplitSteps(cfg, step, out mid, out late);

        return extendedMaxAlive + cfg.MaxAliveIncreasePerWave * mid + cfg.LateMaxAliveIncreasePerWave * late;
    }

    private void TickExtended()
    {
        WaveTable.ExtendedConfig cfg = ExtendedConfig;
        if (cfg == null) return;

        int step = ExtraStep(currentWaveNumber);

        // 스폰 간격은 곱셈이 아니라 뺄셈으로 줄어든다. 하한에 닿으면 멈춘다.
        nextSpawnTime = Time.time + IntervalFor(cfg, step);

        int batch = BatchFor(cfg, step);
        int maxAlive = MaxAliveFor(cfg, step);

        for (int i = 0; i < batch; i++)
        {
            if (EnemyRegistry.Count >= maxAlive) return;
            SpawnExtendedOne(cfg);
        }
    }

    private void LogExtendedWave(WaveTable.ExtendedConfig cfg, int step)
    {
        if (!logExtendedWaves) return;

        int mid, late;
        SplitSteps(cfg, step, out mid, out late);

        Debug.Log($"[웨이브 {currentWaveNumber}] {(late > 0 ? "후반" : "중반")} 구간"
                  + $"  체력 x{EnemyBase.HpMultiplier:0.00}"
                  + $"  간격 {IntervalFor(cfg, step):0.00}  묶음 {BatchFor(cfg, step)}"
                  + $"  구역 {openZones.Count}  동시상한 {MaxAliveFor(cfg, step)}"
                  + $"  (중반 {mid}걸음 + 후반 {late}걸음)");
    }

    /// <summary>
    /// 웨이브마다 열린 구역을 새로 뽑는다. 개수가 3->3 처럼 그대로여도 자리는 반드시 옮긴다.
    /// 자리가 고정되면 한 번 세운 방어선이 끝까지 통해서 쉬는 시간을 쓸 이유가 없어진다.
    /// 쉬는 동안 GameManager가 경고를 띄우려고 먼저 물어보므로, 여기서 한 번만 굴려 두 쪽이 같은 답을 본다.
    /// </summary>
    private void EnsureZonesFor(int waveNumber)
    {
        if (zonesRolledForWave == waveNumber) return;
        zonesRolledForWave = waveNumber;

        RollZones(ZoneCountForStep(ExtendedConfig, ExtraStep(waveNumber)));
    }

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
            arcStart = Random.Range(1, ZoneCount + 1);
        }
        else
        {
            // 1~7칸을 돌려서 반드시 다른 시작점이 나오게 한다.
            arcStart = WrapZone(arcStart + Random.Range(1, ZoneCount));
        }

        arcLength = count;
        RebuildOpenZones();
    }

    /// <summary>이 웨이브에 열려 있어야 할 구역 수. 표를 넘어선 웨이브는 마지막 값을 그대로 쓴다.</summary>
    private static int ZoneCountForStep(WaveTable.ExtendedConfig cfg, int step)
    {
        int[] table = cfg != null ? cfg.ZoneCountPerWave : null;
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

    private void SpawnExtendedOne(WaveTable.ExtendedConfig cfg)
    {
        EnemyBase prefab = PickExtendedPrefab(cfg);
        if (prefab == null) return;

        int zone = openZones.Count > 0 ? openZones[Random.Range(0, openZones.Count)] : 1;

        Vector3 a, b;
        GetZoneSegment(zone, out a, out b);

        Spawn(prefab, Vector3.Lerp(a, b, Random.value));
    }

    /// <summary>웨이브가 오를수록 WeightPerWave 가 붙은 적(탱크 등)의 비중이 커진다.</summary>
    private EnemyBase PickExtendedPrefab(WaveTable.ExtendedConfig cfg)
    {
        if (cfg.Enemies == null || cfg.Enemies.Length == 0) return null;

        int step = ExtraStep(currentWaveNumber);

        extendedWeights.Clear();
        float total = 0f;

        for (int i = 0; i < cfg.Enemies.Length; i++)
        {
            WaveTable.ExtendedEnemy entry = cfg.Enemies[i];

            float w = entry == null || entry.Def == null || entry.Def.Prefab == null
                ? 0f
                : Mathf.Max(0f, entry.Weight + entry.WeightPerWave * step);

            extendedWeights.Add(w);
            total += w;
        }

        if (total <= 0f) return null;

        float roll = Random.value * total;

        for (int i = 0; i < extendedWeights.Count; i++)
        {
            roll -= extendedWeights[i];
            if (roll <= 0f) return cfg.Enemies[i].Def.Prefab;
        }

        for (int i = extendedWeights.Count - 1; i >= 0; i--)
        {
            if (extendedWeights[i] > 0f) return cfg.Enemies[i].Def.Prefab;
        }

        return null;
    }

    // ---------------- 스폰 ----------------

    private void Update()
    {
        if (!spawning) return;

        PlayerController player = PlayerController.Instance;
        if (player == null || !player.IsAlive) return;

        if (Time.time < nextSpawnTime) return;

        if (IsExtendedWave(currentWaveNumber))
        {
            TickExtended();
            return;
        }

        WaveTable.Wave wave = GetWave(currentWaveNumber);
        if (wave == null) return;

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

        Spawn(prefab, PickSpawnPosition(wave));
    }

    /// <summary>적은 풀에서 꺼내 쓴다. 풀이 씬에 없으면 예전처럼 그 자리에서 만든다.</summary>
    private static void Spawn(EnemyBase prefab, Vector3 position)
    {
        if (EnemyPool.Instance != null) EnemyPool.Instance.Spawn(prefab, position);
        else Instantiate(prefab, position, Quaternion.identity);
    }

    // ---------------- 스폰 구역 ----------------

    /// <summary>이 웨이브가 허용한 구역 중 하나를 골라, 그 선분 위의 임의 지점을 돌려준다.</summary>
    private Vector3 PickSpawnPosition(WaveTable.Wave wave)
    {
        Vector3 a, b;
        GetZoneSegment(PickZone(wave), out a, out b);

        return Vector3.Lerp(a, b, Random.value);
    }

    private static int PickZone(WaveTable.Wave wave)
    {
        int[] zones = wave != null ? wave.SpawnZones : null;

        // 지정이 없으면 8구역 전부에서 나온다.
        if (zones == null || zones.Length == 0) return Random.Range(1, ZoneCount + 1);

        int picked = zones[Random.Range(0, zones.Length)];
        return Mathf.Clamp(picked, 1, ZoneCount);
    }

    /// <summary>구역 번호(1~8)에 해당하는 스폰 선분. 규칙은 SpawnZones 가 들고 있다.</summary>
    public void GetZoneSegment(int zone, out Vector3 a, out Vector3 b)
    {
        SpawnZones.GetSegment(zone, new Vector2(ringHalfX, ringHalfZ), out a, out b);
    }

    /// <summary>
    /// 이 웨이브가 쓰는 구역 목록. 비어 있으면 전 구역이라는 뜻이다.
    /// 표를 넘어선 웨이브는 여기서 자리를 뽑아 확정하므로, 경고에 뜬 자리가 곧 실제 스폰 자리다.
    /// </summary>
    public int[] GetWaveZones(int waveNumber)
    {
        if (!IsExtendedWave(waveNumber))
        {
            WaveTable.Wave wave = GetWave(waveNumber);
            return wave != null ? wave.SpawnZones : null;
        }

        EnsureZonesFor(waveNumber);
        return openZones.ToArray();
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

        float roll = Random.value * total;

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
