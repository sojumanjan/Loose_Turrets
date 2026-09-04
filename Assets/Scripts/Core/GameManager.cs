// 경험치 / 레벨 / 업그레이드 적용을 담당하는 중앙 매니저. 레벨업하면 게임을 멈추고 3택 UI를 띄운다.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Menu,       // 시작 화면. START를 누르기 전까지 게임이 멈춰 있다.
        Playing,    // 웨이브 진행 중 (스폰 켜짐)
        Break,      // 웨이브 사이 쉬는 시간 (스폰 꺼짐)
        GameOver
    }

    public static GameManager Instance { get; private set; }

    [Header("게임 데이터 (포탑 종류 / 레벨 표는 여기서)")]
    [Tooltip("엑셀 TSV로 관리되는 데이터의 입구. 포탑 목록과 레벨업 요구치가 여기서 나온다.")]
    [SerializeField] private GameDatabase database;

    [Header("전체 포탑 강화 (약하지만 넓게)")]
    [SerializeField] private float allDamageStep = 0.25f;
    [SerializeField] private float allFireRateStep = 0.20f;
    [SerializeField] private float allRangeStep = 0.15f;

    [Header("플레이어 강화")]
    [SerializeField] private float playerSpeedStep = 0.7f;

    [Header("새 포탑 배치")]
    [SerializeField] private float newTurretDistance = 2.5f;

    [Header("웨이브")]
    [Tooltip("웨이브별 세부 설정(길이 / 스폰 간격 / 개수 / 적 확률)은 EnemySpawner의 waves 배열에서 조절한다.")]
    [SerializeField] private EnemySpawner spawner;

    [Tooltip("포탑 소환 카드가 다른 카드보다 얼마나 더 자주 나올지. 0.15면 15% 더 자주 나온다. " +
             "카드 10장 중 소환이 3장이면 균등일 때 각 10%인데, 0.15를 주면 각 11.5%가 되고 남은 몫을 나머지가 나눠 갖는다.")]
    [Min(0f)] [SerializeField] private float newTurretCardBonus = 0.15f;

    // 보스 웨이브 / 체력 / 문구 / 프리팹은 전부 WaveTable 에셋의 "보스 웨이브" 항목에 있다.
    // 웨이브마다 값이 달라서, 웨이브 데이터가 사는 곳에 같이 두는 편이 찾기 쉽다.

    [Tooltip("그 웨이브부터 포탑 종류별로 더 놓을 수 있는 개수. 대포 최대 1개면 그 뒤로는 2개가 된다.")]
    [Min(0)] [SerializeField] private int extendedWaveExtraTurrets = 1;

    [Tooltip("세 번째 특수를 하나 먹은 뒤 몇 번의 레벨업을 일반 카드로만 채울지. " +
             "4면 다음 4레벨은 일반만 나오고 그 다음 레벨업에서 또 한 장이 확정 등장한다. " +
             "0이면 쿨다운 없이 후보가 남아 있는 동안 매 레벨업마다 확정으로 한 장 나온다.")]
    [Min(0)] [SerializeField] private int special3CooldownLevels;

    [Header("사망 연출")]
    [Tooltip("플레이어가 죽을 때 터뜨릴 파티클. 비우면 연출 없이 곧바로 결과창이 뜬다.")]
    [SerializeField] private GameObject deathEffect;

    [Tooltip("폭발이 터지고 결과창이 뜨기까지의 시간. 이 동안에는 시간이 흐른다.")]
    [Min(0f)] [SerializeField] private float deathResultDelay = 3f;

    [Header("웨이브 유예")]
    [Tooltip("게임을 시작하고 첫 웨이브가 몰려오기까지의 유예. 웨이브 사이 쉬는 시간과 같은 역할이다. " +
             "이 동안 경고 표시를 보고 포탑을 옮길 수 있다.")]
    [Min(0f)] [SerializeField] private float firstWaveDelay = 5f;

    [Tooltip("스포너가 연결되지 않았을 때만 쓰는 예비값.")]
    [SerializeField] private int fallbackWaveCount = 5;
    [SerializeField] private float fallbackWaveDuration = 45f;
    [SerializeField] private float fallbackBreakDuration = 4f;

#if UNITY_EDITOR
    // 아래 디버그 묶음은 에디터에서만 컴파일된다. 빌드에는 키 입력도 메서드도 들어가지 않는다.
    // 인스펙터 값은 씬에 그대로 남아 있으므로, 에디터에서는 예전처럼 F1~F5가 다 먹는다.

    [Header("디버그 (에디터 전용)")]
    [Tooltip("F1 즉시 레벨업 / F2 무적 / F3 다음 웨이브 / F4 결과창 / F5 중반 건너뛰기. 빌드에는 안 들어간다.")]
    [SerializeField] private bool enableDebugKeys = true;

    [Tooltip("F5로 건너뛸 웨이브 번호.")]
    [Min(1)] [SerializeField] private int debugSkipWave = 6;

    [Tooltip("F5로 맞출 레벨. 여기까지 필요한 XP를 한 번에 몰아줘서 카드를 연달아 고르게 한다.")]
    [Min(1)] [SerializeField] private int debugSkipLevel = 23;
#endif

    [Header("카드 색")]
    [SerializeField] private Color neutralCardColor = new Color(0.55f, 0.58f, 0.66f);
    [SerializeField] private Color playerCardColor = new Color(0.4f, 0.85f, 0.5f);

    public int Level { get; private set; }
    public int Xp { get; private set; }
    public int XpToNext { get; private set; }
    public int Kills { get; private set; }
    public float Elapsed { get; private set; }
    public float XpRatio => XpToNext <= 0 ? 0f : Mathf.Clamp01((float)Xp / XpToNext);

    public GameState State { get; private set; }
    public int Wave { get; private set; }

    /// <summary>웨이브 표에 적힌 웨이브 수. 이 번호를 넘어서면 표 대신 5웨이브 블록이 굴린다.</summary>
    public int TableWaveCount =>
        spawner != null && spawner.TableWaveCount > 0 ? spawner.TableWaveCount : fallbackWaveCount;

    /// <summary>표를 다 쓴 뒤의 웨이브인가.</summary>
    public bool InExtendedWaves => Wave > TableWaveCount;

    /// <summary>보스를 한 번이라도 잡았는가. 보상 자체는 아래 두 개가 따로 관리한다.</summary>
    public bool BossDefeated { get; private set; }

    /// <summary>포탑을 종류당 하나씩 더 놓을 수 있는가.
    /// 어느 보스가 이걸 주는지는 WaveTable의 보스 웨이브마다 따로 정한다.</summary>
    public bool HasExtraTurretSlot { get; private set; }

    /// <summary>두 번째 특수가 카드에 뜰 수 있는가. 이것도 WaveTable에서 보스별로 정한다.</summary>
    public bool Special2Unlocked { get; private set; }

    /// <summary>세 번째 특수가 카드에 뜰 수 있는가. 두 번째와 따로 열린다.
    /// 조건(1·2특을 다 먹음)을 채워도 이 보스를 잡기 전에는 나오지 않는다.</summary>
    public bool Special3Unlocked { get; private set; }

    /// <summary>이 번호가 보스 웨이브인가.</summary>
    public bool IsBossWave(int waveNumber) => GetBossWave(waveNumber) != null;

    /// <summary>이 웨이브의 보스 설정. 보스 웨이브가 아니면 null. 값은 전부 WaveTable에 있다.</summary>
    public WaveTable.BossWave GetBossWave(int waveNumber)
    {
        WaveTable table = database != null ? database.Waves : null;
        return table != null ? table.GetBossWave(waveNumber) : null;
    }

    /// <summary>지금 진행 중인 보스 웨이브의 설정. 보스전이 아니면 null. BossEnemy가 문구를 꺼낼 때 쓴다.</summary>
    public WaveTable.BossWave CurrentBossWave => GetBossWave(Wave);

    /// <summary>지금 보스와 싸우는 중인가. HUD가 체력바를 켜는 데 쓴다.</summary>
    public bool InBossFight => State == GameState.Playing && IsBossWave(Wave);

    /// <summary>현재 상태가 끝나기까지 남은 시간. GameOver에서는 의미 없음.</summary>
    public float StateTimeLeft { get; private set; }
    public bool IsOver => State == GameState.GameOver;

    public int OpenZoneCount => spawner != null ? spawner.OpenZoneCount : 0;

    public event Action OnStatsChanged;

    private static readonly TurretDef[] NoTurrets = new TurretDef[0];

    /// <summary>데이터베이스의 포탑 목록. 이름을 유지해 아래 로직을 그대로 쓴다.</summary>
    private TurretDef[] turretChoices =>
        database != null && database.Turrets != null ? database.Turrets : NoTurrets;

    private int pendingLevelUps;
    private readonly List<UpgradeOption> pool = new List<UpgradeOption>();

    // 매번 리스트를 새로 만들지 않도록 재사용하는 확률 버퍼.
    private readonly List<float> drawWeights = new List<float>(32);

    // 특수 강화 후보 목록. 여러 포탑이 동시에 준비되면 그중 하나만 뽑는다.
    private readonly List<int> special2Candidates = new List<int>(8);
    private readonly List<int> special3Candidates = new List<int>(8);

    // 포탑 종류별로 "관련 강화를 몇 번 골랐는지". SpecialThreshold에 닿으면 특수 강화가 확정 등장한다.
    private int[] typeProgress;

    // 사거리 강화는 포탑 종류당 한 판에 한 번만. 사거리만 연달아 뽑혀 운으로 벌어지는 일을 막는다.
    private bool[] rangeTaken;

    // 특수 강화는 한 판에 종류당 한 번만 가져갈 수 있다.
    private bool[] specialTaken;
    private bool[] special2Taken;
    private bool[] special3Taken;

    // 세 번째 특수를 먹은 뒤 남은 대기 레벨업 수. 0이면 다음 레벨업에서 한 장이 확정 등장한다.
    private int special3Cooldown;

    // 포탑 종류별로 쓴 일반 강화 횟수. MaxUpgrades에 닿으면 그 포탑의 강화 카드가 더 안 나온다.
    private int[] typeUpgradeCount;

    // 보스를 실제로 화면에서 본 적이 있는가. 소환 직후 한 프레임을 죽음으로 오해하지 않으려고 둔다.
    private bool bossSeen;

    // 중간부터 시작할 때 미뤄둔 보상의 기준 웨이브. 0이면 미뤄둔 것이 없다.
    // 몰아서 뜬 카드를 다 고른 순간 이 웨이브 앞의 보스 보상이 한꺼번에 열린다.
    private int deferredRewardWave;

    // 중간부터 시작할 때 미뤄둔 웨이브 경고. 카드를 다 고른 순간에 낸다.
    private int deferredWarningWave;

    private void Awake()
    {
        Instance = this;

        // 적이 수백 마리 깔리면 각자 피격/펀치 트윈을 돌려 기본 용량(200)을 넘긴다.
        // 넘길 때마다 런타임에 재할당이 일어나 끊기므로 시작할 때 미리 잡아둔다.
        DG.Tweening.DOTween.SetTweensCapacity(500, 125);

        // static 배율과 timeScale은 씬을 다시 로드해도 남는다. 새 판은 항상 여기서 초기화한다.
        TurretBase.ResetMultipliers();
        EnemyBase.ResetHpMultiplier();
        DamageStats.Clear();

        // 메인 메뉴에서 START를 누를 때까지 멈춰 있는다.
        Time.timeScale = 0f;

        Level = 1;
        Xp = 0;
        XpToNext = GetXpRequirement(1);
        Kills = 0;
        Elapsed = 0f;
        pendingLevelUps = 0;

        int choiceCount = turretChoices != null ? turretChoices.Length : 0;
        typeProgress = new int[choiceCount];
        rangeTaken = new bool[choiceCount];
        specialTaken = new bool[choiceCount];
        special2Taken = new bool[choiceCount];
        special3Taken = new bool[choiceCount];
        special3Cooldown = 0;
        typeUpgradeCount = new int[choiceCount];

        Wave = 0;
        State = GameState.Menu;
        StateTimeLeft = 0f;
        BossDefeated = false;
        HasExtraTurretSlot = false;
        Special2Unlocked = false;
        Special3Unlocked = false;
        deferredRewardWave = 0;
        deferredWarningWave = 0;
        bossSeen = false;
    }

    /// <summary>메인 메뉴의 START가 부른다.</summary>
    public void StartGame()
    {
        if (State != GameState.Menu) return;

        Time.timeScale = 1f;
        // 경고를 먼저 띄우고 유예가 지난 뒤에 첫 웨이브가 몰려온다. 웨이브 사이 흐름과 같다.
        State = GameState.Break;
        StateTimeLeft = firstWaveDelay;

        WarnNextWaveZones(1);
    }

    /// <summary>
    /// 해금된 구간에서 바로 시작한다. 메인 메뉴의 "N웨이브부터" 버튼이 부른다.
    /// 웨이브는 정상 경로로 시작해 스폰 구역·경고·배너가 함께 맞고,
    /// 레벨은 요구치를 몰아줘서 카드를 연달아 고르게 한다. 디버그 건너뛰기와 같은 방식이다.
    /// </summary>
    public void StartGameAt(int startWave, int startLevel, float startElapsed)
    {
        if (State != GameState.Menu) return;

        Time.timeScale = 1f;

        int wave = Mathf.Max(1, startWave);

        // 직전 보스의 보상만 카드를 다 고른 뒤에 준다.
        // 시작하자마자 열어주면 몰아서 뜨는 카드 중에 2특이 섞여, 1부터 올라온 판보다 훨씬 앞서 나간다.
        // 1부터 왔다면 그 시점에는 강화를 채우느라 2특을 아직 못 먹었을 자리다.
        // 그보다 앞선 보스의 보상은 정상 진행이었어도 이미 몇 웨이브 전에 손에 있었을 것이라 지금 연다.
        deferredRewardWave = wave;
        ApplyOldBossRewards(wave);

        // 버틴 시간도 그 구간까지 온 것으로 맞춘다. 0부터 세면 기록이 실제 진행과 어긋난다.
        Elapsed = Mathf.Max(0f, startElapsed);

        // 곧바로 몹을 내보내지 않고 쉬는 시간부터 시작한다. 웨이브 사이 흐름과 같아야
        // 어느 구역이 열렸는지 빨간 테두리로 먼저 보고 포탑을 옮길 수 있다.
        // 카드를 고르는 동안에는 timeScale이 0이라 이 시간이 흐르지 않는다.
        Wave = wave - 1;
        State = GameState.Break;
        StateTimeLeft = wave <= 1 ? firstWaveDelay : GetBreakAfter(Wave);

        // 경고도 카드를 다 고른 뒤에 낸다. 카드가 쌓여 있는 동안 울려봐야
        // 소리는 카드 넘기는 소리에 묻히고 빨간 테두리는 카드에 가려 안 보인다.
        deferredWarningWave = wave;

        int level = Mathf.Max(1, startLevel);
        if (level > Level)
        {
            int need = -Xp;
            for (int lv = Level; lv < level; lv++) need += GetXpRequirement(lv);

            if (need > 0) AddXp(need);
        }

        // 몰아줄 XP가 없어 카드가 한 장도 안 뜨는 경우엔 여기서 바로 준다.
        if (pendingLevelUps <= 0) ReleaseDeferredStart();

        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// 중간부터 시작할 때 몰아준 카드를 전부 고른 순간. 미뤄둔 것이 여기서 한꺼번에 나간다.
    /// 보상을 먼저 열고 경고를 낸다. 처치 문구가 먼저 뜨고 그 뒤에 다음 웨이브 경고가 오는 순서다.
    /// </summary>
    private void ReleaseDeferredStart()
    {
        ReleaseDeferredBossRewards();
        ReleaseDeferredWarning();
    }

    /// <summary>미뤄둔 웨이브 경고를 낸다. 여기서 비로소 경고음·빨간 테두리가 나온다.</summary>
    private void ReleaseDeferredWarning()
    {
        if (deferredWarningWave <= 0) return;

        int wave = deferredWarningWave;
        deferredWarningWave = 0;

        WarnNextWaveZones(wave);
    }

    /// <summary>건너뛴 구간에서 가장 늦게 지나친 보스의 웨이브 번호. 없으면 0.</summary>
    private int LatestSkippedBossWave(int startWave)
    {
        WaveTable table = database != null ? database.Waves : null;
        if (table == null || table.BossWaves == null) return 0;

        int latest = 0;

        for (int i = 0; i < table.BossWaves.Length; i++)
        {
            WaveTable.BossWave entry = table.BossWaves[i];
            if (entry == null || entry.Wave >= startWave) continue;

            if (entry.Wave > latest) latest = entry.Wave;
        }

        return latest;
    }

    /// <summary>
    /// 건너뛴 보스 중 직전 보스를 뺀 나머지의 보상을 시작하는 순간 연다.
    /// 16웨이브로 시작하면 10웨이브 보스의 2특은 정상 진행이었어도 다섯 웨이브 전에 이미 손에 있었을
    /// 것이라 잠가 둘 이유가 없다. 미루는 것은 바로 직전 보스(15웨이브)의 보상뿐이다.
    /// 11웨이브 시작은 직전 보스가 10웨이브 하나뿐이라 여기서 아무것도 열리지 않는다.
    /// 문구는 띄우지 않는다. 판이 시작하기도 전에 지나간 일을 알리는 배너다.
    /// </summary>
    private void ApplyOldBossRewards(int startWave)
    {
        WaveTable table = database != null ? database.Waves : null;
        if (table == null || table.BossWaves == null) return;

        int latest = LatestSkippedBossWave(startWave);

        for (int i = 0; i < table.BossWaves.Length; i++)
        {
            WaveTable.BossWave entry = table.BossWaves[i];
            if (entry == null || entry.Wave >= startWave || entry.Wave == latest) continue;

            BossDefeated = true;

            if (entry.GrantsExtraTurretSlot) HasExtraTurretSlot = true;
            if (entry.UnlocksSpecial2) Special2Unlocked = true;
            if (entry.UnlocksSpecial3) Special3Unlocked = true;
        }
    }

    /// <summary>
    /// 미뤄둔 직전 보스의 보상을 준다. 시작 시 몰아준 카드를 전부 고른 순간에 불린다.
    /// 그래서 그 보스가 주는 해금은 "남은 강화를 다 고른 뒤"에 열린다.
    /// </summary>
    private void ReleaseDeferredBossRewards()
    {
        if (deferredRewardWave <= 0) return;

        int startWave = deferredRewardWave;
        deferredRewardWave = 0;

        WaveTable table = database != null ? database.Waves : null;
        if (table == null || table.BossWaves == null) return;

        int latest = LatestSkippedBossWave(startWave);
        if (latest <= 0) return;

        WaveTable.BossWave last = null;

        for (int i = 0; i < table.BossWaves.Length; i++)
        {
            WaveTable.BossWave entry = table.BossWaves[i];
            if (entry != null && entry.Wave == latest) { last = entry; break; }
        }

        if (last == null) return;

        BossDefeated = true;

        if (last.GrantsExtraTurretSlot) HasExtraTurretSlot = true;
        if (last.UnlocksSpecial2) Special2Unlocked = true;
        if (last.UnlocksSpecial3) Special3Unlocked = true;

        ShowBanner(last.DefeatBanner, last.DefeatBannerColor, last.DefeatBannerHold);

        OnStatsChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (State == GameState.Menu) return;

        if (IsOver)
        {
            HandleRestartInput();
            return;
        }

#if UNITY_EDITOR
        HandleDebugInput();
#endif

        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        if (!player.IsAlive)
        {
            EnterGameOver();
            return;
        }

        if (!IsBossTimeFrozen) Elapsed += Time.deltaTime;

        TickWaves();
    }

    /// <summary>
    /// 보스전 동안 버틴 시간이 멈추는가. 경고가 뜨는 순간부터 보스를 눕히는 순간까지 참이다.
    /// 보스는 시간이 아니라 화력으로 끝내는 구간이라, 오래 끌수록 기록이 좋아지면 앞뒤가 맞지 않는다.
    /// 보스가 죽으면 웨이브 번호는 그대로인 채 Break로 돌아가고, 그때 Wave + 1은 보스가 아니라 풀린다.
    /// </summary>
    private bool IsBossTimeFrozen
    {
        get
        {
            // 보스 웨이브 진행 중.
            if (State == GameState.Playing && IsBossWave(Wave)) return true;

            // 쉬는 시간인데 다음이 보스 웨이브. 화면에 경고가 떠 있는 구간이다.
            if (State == GameState.Break && IsBossWave(Wave + 1)) return true;

            return false;
        }
    }

    // ---------------- 웨이브 ----------------

    private void TickWaves()
    {
        switch (State)
        {
            case GameState.Playing:
                // 보스 웨이브는 제한 시간이 없다. 보스를 눕혀야 끝난다.
                if (IsBossWave(Wave))
                {
                    TickBossWave();
                    break;
                }

                StateTimeLeft -= Time.deltaTime;
                if (StateTimeLeft > 0f) break;

                StopSpawning();

                State = GameState.Break;
                StateTimeLeft = GetBreakAfter(Wave);

                // 쉬는 동안 다음 웨이브가 어디서 오는지 미리 알려준다.
                // 표를 넘어선 웨이브는 이때 스포너가 자리를 확정하므로, 경고에 뜬 자리가 곧 실제 스폰 자리다.
                WarnNextWaveZones(Wave + 1);
                break;

            case GameState.Break:
                StateTimeLeft -= Time.deltaTime;
                if (StateTimeLeft <= 0f) StartWave(Wave + 1);
                break;
        }
    }

    private void StartWave(int index)
    {
        Wave = index;
        State = GameState.Playing;
        StateTimeLeft = GetWaveDuration(index);

        if (IsBossWave(index))
        {
            StartBossWave();
            return;
        }

        if (spawner != null) spawner.BeginWave(index);
        ShowBanner(Wave + " 웨이브", new Color(0.55f, 0.8f, 1f));
        SfxManager.Play(SfxManager.Common?.WaveStart);
    }

    // ---------------- 보스 ----------------

    /// <summary>보스 프리팹. WaveTable이 들고 있다.</summary>
    private BossEnemy BossPrefab => database != null && database.Waves != null ? database.Waves.BossPrefab : null;

    private void StartBossWave()
    {
        if (GameHud.Instance != null) GameHud.Instance.HideBossWarning();

        WaveTable.BossWave entry = GetBossWave(Wave);

        // 보스 웨이브에도 스포너는 웨이브 번호를 알아야 한다. 뒤 웨이브 난이도가 번호에서 나오기 때문이다.
        bool stopSpawns = database != null && database.Waves != null && database.Waves.BossStopsNormalSpawns;
        if (spawner != null) spawner.BeginWave(Wave, stopSpawns);

        // 보스는 웨이브 체력 배율을 타지 않는다. 대신 웨이브별로 정해둔 체력을 배율로 환산해 넣는다.
        // 배율을 쓰는 이유는 EnemyBase가 OnEnable에서 체력을 확정하기 때문이다.
        // Instantiate 직후에 값을 넣으면 이미 늦는다.
        EnemyBase.SetHpMultiplier(BossHpMultiplierFor(entry));

        SpawnBoss();

        if (entry != null) ShowBanner(entry.AppearBanner, entry.AppearBannerColor);
        SfxManager.Play(SfxManager.Common?.WaveStart);
    }

    /// <summary>웨이브에 적힌 체력을 프리팹 기본 체력으로 나눈 배율. 0이면 프리팹 값을 그대로 쓴다.</summary>
    private float BossHpMultiplierFor(WaveTable.BossWave entry)
    {
        BossEnemy prefab = BossPrefab;
        if (entry == null || entry.MaxHp <= 0f || prefab == null) return 1f;

        float baseHp = prefab.BaseMaxHp;
        return baseHp > 0f ? entry.MaxHp / baseHp : 1f;
    }

    /// <summary>스폰 구역 8곳 중 한 곳에서 보스를 내보낸다.</summary>
    private void SpawnBoss()
    {
        BossEnemy prefab = BossPrefab;
        if (prefab == null || spawner == null) return;

        int zone = UnityEngine.Random.Range(1, EnemySpawner.ZoneCount + 1);

        Vector3 a, b;
        spawner.GetZoneSegment(zone, out a, out b);

        Instantiate(prefab, Vector3.Lerp(a, b, UnityEngine.Random.value), Quaternion.identity);
    }

    /// <summary>보스 웨이브는 타이머 대신 보스의 생사를 본다.</summary>
    private void TickBossWave()
    {
        // 소환 직후 한 프레임은 아직 등록 전일 수 있으므로 살아있는 보스를 한 번은 봐야 한다.
        if (BossEnemy.Current != null)
        {
            bossSeen = true;
            return;
        }

        if (!bossSeen) return;

        bossSeen = false;

        StopSpawning();
        State = GameState.Break;
        StateTimeLeft = GetBreakAfter(Wave);
        WarnNextWaveZones(Wave + 1);
    }

    /// <summary>BossEnemy가 죽으면서 부른다. 보상은 포탑 추가 슬롯 하나다.</summary>
    public void OnBossDefeated()
    {
        BossDefeated = true;

        // 다음 판에서 이 웨이브 뒤부터 시작할 수 있게 남긴다. 판을 넘어 유지되는 유일한 진행 기록이다.
        WaveUnlocks.MarkBossCleared(Wave);

        // 보상과 문구는 WaveTable이 웨이브별로 들고 있다. 방금 끝난 보스 웨이브의 것을 쓴다.
        // 보스가 여럿이므로 "이미 하나 잡았으면 무시" 로 막지 않는다. 보스마다 자기 보상을 준다.
        WaveTable.BossWave entry = GetBossWave(Wave);
        if (entry != null)
        {
            if (entry.GrantsExtraTurretSlot) HasExtraTurretSlot = true;
            if (entry.UnlocksSpecial2) Special2Unlocked = true;
            if (entry.UnlocksSpecial3) Special3Unlocked = true;

            // 해금을 알리는 문구라 웨이브 배너보다 오래 띄운다.
            ShowBanner(entry.DefeatBanner, entry.DefeatBannerColor, entry.DefeatBannerHold);
        }

        SfxManager.Play(SfxManager.Common?.StageClear);

        OnStatsChanged?.Invoke();
    }

    private float GetWaveDuration(int waveNumber) =>
        spawner != null ? spawner.GetWaveDuration(waveNumber) : fallbackWaveDuration;

    private float GetBreakAfter(int waveNumber) =>
        spawner != null ? spawner.GetBreakAfter(waveNumber) : fallbackBreakDuration;

    private void StopSpawning()
    {
        if (spawner != null) spawner.StopSpawning();
    }

    private void WarnNextWaveZones(int waveNumber)
    {
        // 보스는 어느 구역에서 나올지 알려주지 않는다. 대신 화면 중앙에 크게 알린다.
        WaveTable.BossWave entry = GetBossWave(waveNumber);
        if (entry != null)
        {
            // 구역 경계선은 다음 Warn 까지 남는다. 보스전은 Warn 을 부르지 않으므로
            // 여기서 치우지 않으면 직전 웨이브의 빨간 테두리가 보스전 내내 깔려 있다.
            if (SpawnZoneWarning.Instance != null) SpawnZoneWarning.Instance.ClearAll();

            // 구역 경고와 같은 소리를 쓴다. 보스전이라고 조용히 지나가면 경고가 눈에만 남는다.
            SfxManager.Play(SfxManager.Common?.ZoneWarning);

            // 문구는 WaveTable이 웨이브별로 들고 있다. 아직 소환 전이라 인스턴스에서는 못 꺼낸다.
            if (GameHud.Instance != null) GameHud.Instance.ShowBossWarning(entry.WarningMessage);
            return;
        }

        if (spawner == null || SpawnZoneWarning.Instance == null) return;

        SpawnZoneWarning.Instance.Warn(spawner.GetWaveZones(waveNumber));
    }

    private static void ShowBanner(string text, Color color, float holdDuration = 0f)
    {
        if (GameHud.Instance != null) GameHud.Instance.ShowBanner(text, color, holdDuration);
    }

    // ---------------- 종료 / 재시작 ----------------

    private void EnterGameOver()
    {
        State = GameState.GameOver;
        StopSpawning();

        // 결과창이 늦게 떠도 기록은 여기서 남긴다. 항목별로 최고값만 갱신된다.
        BestRecords.Submit(Elapsed, Wave, Kills, DamageStats.Total);

        // 폭발이 보여야 하므로 여기서 시간을 멈추지 않는다. 결과창을 띄우는 순간에 멈춘다.
        // 게임오버 효과음도 사망 순간이 아니라 결과창과 함께 낸다.
        SpawnDeathEffect();

        if (deathResultDelay > 0f) StartCoroutine(ShowResultAfterDelay());
        else ShowGameOverResult();
    }

    /// <summary>플레이어가 있던 자리에서 폭발을 터뜨린다.</summary>
    private void SpawnDeathEffect()
    {
        if (deathEffect == null) return;

        PlayerController player = PlayerController.Instance;
        Vector3 at = player != null ? player.transform.position : Vector3.zero;

        GameObject fx = Instantiate(deathEffect, at, Quaternion.identity);

        // 프리팹이 무한 반복이라 그대로 두면 계속 터진다. 한 번만 터지게 끈다.
        ParticleSystem[] systems = fx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            main.loop = false;
        }

        Destroy(fx, deathResultDelay + 2f);
    }

    private IEnumerator ShowResultAfterDelay()
    {
        yield return new WaitForSeconds(deathResultDelay);

        ShowGameOverResult();
    }

    private void ShowGameOverResult()
    {
        Time.timeScale = 0f;

        SfxManager.Play(SfxManager.Common?.GameOver);
        if (ResultUI.Instance != null) ResultUI.Instance.Show();
    }

#if UNITY_EDITOR
    // ---------------- 디버그 (에디터 전용) ----------------
    // F1~F5와 그 아래 Force* 메서드는 전부 여기 묶여 있다. 빌드에는 통째로 빠진다.

    private void HandleDebugInput()
    {
        if (!enableDebugKeys) return;

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;

        // 선택 카드가 떠 있는 동안에는 무시한다. 안 그러면 레벨업이 계속 쌓인다.
        if (LevelUpUI.Instance != null && LevelUpUI.Instance.IsOpen) return;

        if (keyboard.f1Key.wasPressedThisFrame) ForceLevelUp();
        else if (keyboard.f2Key.wasPressedThisFrame) ToggleInvincible();
        else if (keyboard.f3Key.wasPressedThisFrame) ForceNextWave();
        else if (keyboard.f4Key.wasPressedThisFrame) ForceResult();
        else if (keyboard.f5Key.wasPressedThisFrame) ForceSkipToMidGame();
    }

    /// <summary>
    /// 디버그용. 웨이브와 레벨을 한 번에 중반으로 끌어올린다.
    /// 부족한 XP를 몰아주면 AddXp 가 레벨업을 쌓아두므로 카드가 연달아 뜬다.
    /// 뒷 구간 밸런싱이나 3특 확인을 처음부터 굴리지 않고 보려고 쓴다.
    /// </summary>
    public void ForceSkipToMidGame()
    {
        if (State == GameState.Menu || IsOver) return;

        int targetWave = Mathf.Max(1, debugSkipWave);
        int targetLevel = Mathf.Max(1, debugSkipLevel);

        // 웨이브는 정상 경로로 시작해야 스폰 구역·경고·배너가 함께 맞는다.
        if (targetWave > Wave)
        {
            StopSpawning();
            Wave = targetWave - 1;
            StartWave(targetWave);
        }

        // 목표 레벨까지의 요구치를 다 더하고, 이미 모아둔 XP는 뺀다.
        if (targetLevel > Level)
        {
            int need = -Xp;
            for (int lv = Level; lv < targetLevel; lv++) need += GetXpRequirement(lv);

            if (need > 0) AddXp(need);
        }

        Debug.Log($"[디버그] 웨이브 {Wave} · 레벨 {Level} 로 건너뛰었습니다. 대기 중인 레벨업 {pendingLevelUps}회");
    }

    /// <summary>결과창을 바로 띄운다. 배치와 문구를 확인하려고 쓴다. 사망 연출은 건너뛴다.</summary>
    private void ForceResult()
    {
        // 이미 끝난 판이면 결과창이 떠 있으므로 아무것도 하지 않는다.
        if (IsOver) return;

        State = GameState.GameOver;
        StopSpawning();
        ShowGameOverResult();
    }

    /// <summary>디버그용. 플레이어 무적을 켜고 끈다. 뒷 웨이브 밸런싱할 때 안 죽고 지켜보려고 쓴다.</summary>
    public void ToggleInvincible()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        bool on = !player.Invincible;
        player.SetInvincible(on);

        ShowBanner(on ? "무적 ON" : "무적 OFF",
            on ? new Color(1f, 0.86f, 0.36f) : new Color(0.7f, 0.72f, 0.78f));
    }

    /// <summary>디버그용. 지금 웨이브를 즉시 끝낸다. 뒷 웨이브까지 4분씩 기다리지 않으려고 쓴다.</summary>
    public void ForceNextWave()
    {
        if (State != GameState.Playing && State != GameState.Break) return;

        StateTimeLeft = 0f;
    }

    /// <summary>디버그용. 다음 레벨까지 필요한 XP를 즉시 채워 레벨업시킨다.</summary>
    public void ForceLevelUp()
    {
        if (State == GameState.Menu || IsOver) return;

        AddXp(Mathf.Max(1, XpToNext - Xp));
    }

    // ---------------- 디버그 끝 ----------------
#endif

    private void HandleRestartInput()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.rKey.wasPressedThisFrame) Restart();
    }

    /// <summary>씬을 다시 불러온 뒤 메인 메뉴를 건너뛰고 바로 시작할지. 씬 로드를 넘어가야 해서 static 이다.</summary>
    private static bool autoStartAfterReload;

    /// <summary>MainMenuUI가 열린 직후 한 번 물어본다. 한 번 읽으면 꺼진다.</summary>
    public static bool ConsumeAutoStart()
    {
        bool value = autoStartAfterReload;
        autoStartAfterReload = false;
        return value;
    }

    /// <summary>새 판을 곧바로 시작한다. 일시정지 메뉴의 '다시하기'가 쓴다.</summary>
    public void RestartAndPlay()
    {
        autoStartAfterReload = true;
        Restart();
    }

    /// <summary>씬을 다시 불러와 메인 메뉴로 돌아간다.</summary>
    public void Restart()
    {
        // static 상태는 씬을 다시 로드해도 남으므로 여기서 전부 되돌린다.
        Time.timeScale = 1f;
        EnemyRegistry.Clear();
        TurretBase.ResetMultipliers();
        EnemyBase.ResetHpMultiplier();
        DamageStats.Clear();
        MouseWorld.ResetCache();

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnEnemyKilled(int xpReward)
    {
        Kills++;
        AddXp(xpReward);
    }

    public void AddXp(int amount)
    {
        if (amount <= 0) return;

        Xp += amount;

        // 한 번에 여러 레벨이 오를 수 있다. 선택은 한 번에 하나씩 처리한다.
        while (Xp >= XpToNext)
        {
            Xp -= XpToNext;
            Level++;
            XpToNext = GetXpRequirement(Level);
            pendingLevelUps++;
        }

        OnStatsChanged?.Invoke();

        if (pendingLevelUps > 0) ShowNextLevelUp();
    }

    /// <summary>해당 레벨에서 다음 레벨까지 필요한 XP. 표를 넘어가면 마지막 값에서 배율로 이어간다.</summary>
    private int GetXpRequirement(int level)
    {
        if (database != null && database.Levels != null) return database.Levels.GetRequirement(level);

        // 데이터가 없어도 게임이 멈추지 않도록 하는 예비 곡선.
        return Mathf.Max(1, Mathf.RoundToInt(5f * Mathf.Pow(1.3f, Mathf.Max(0, level - 1))));
    }

    private void ShowNextLevelUp()
    {
        // 게임이 끝난 뒤에는 남은 총알이 적을 잡아도 레벨업 창을 띄우지 않는다.
        if (IsOver)
        {
            pendingLevelUps = 0;
            return;
        }

        // UI가 없으면 게임이 영영 멈추는 사고가 나므로, 그냥 넘어간다.
        if (LevelUpUI.Instance == null)
        {
            pendingLevelUps = 0;
            Time.timeScale = 1f;
            return;
        }

        if (LevelUpUI.Instance.IsOpen) return;

        Time.timeScale = 0f;
        SfxManager.Play(SfxManager.Common?.LevelUp);
        LevelUpUI.Instance.Show(RollOptions(3), ApplyUpgrade);
    }

    public void ApplyUpgrade(UpgradeOption option)
    {
        SfxManager.Play(SfxManager.Common?.CardSelect);

        Type kind = GetChoiceKind(option.TurretIndex);
        TurretDef choice = GetChoice(option.TurretIndex);

        switch (option.Type)
        {
            case UpgradeType.AllDamage:
                TurretBase.AddGlobalMods(allDamageStep, 0f, 0f);
                break;

            case UpgradeType.AllFireRate:
                TurretBase.AddGlobalMods(0f, allFireRateStep, 0f);
                break;

            case UpgradeType.AllRange:
                TurretBase.AddGlobalMods(0f, 0f, allRangeStep);
                break;

            case UpgradeType.TypeDamage:
                if (choice != null) TurretBase.AddTypeMods(kind, choice.DamageStep, 0f, 0f);
                break;

            case UpgradeType.TypeFireRate:
                if (choice != null) TurretBase.AddTypeMods(kind, 0f, choice.FireRateStep, 0f);
                break;

            case UpgradeType.TypeRange:
                if (choice != null) TurretBase.AddTypeMods(kind, 0f, 0f, choice.RangeStep);
                break;

            case UpgradeType.TypeSpecial:
                TurretBase.AddSpecialLevel(kind);
                break;

            case UpgradeType.TypeSpecial2:
                TurretBase.AddSpecial2Level(kind);
                break;

            case UpgradeType.TypeSpecial3:
                TurretBase.AddSpecial3Level(kind);
                break;

            case UpgradeType.NewTurret:
                SpawnTurret(option.TurretIndex);
                break;

            case UpgradeType.PlayerSpeed:
                if (PlayerController.Instance != null) PlayerController.Instance.AddMoveSpeed(playerSpeedStep);
                break;
        }

        AdvanceTypeProgress(option);

        pendingLevelUps = Mathf.Max(0, pendingLevelUps - 1);
        OnStatsChanged?.Invoke();

        if (pendingLevelUps > 0)
        {
            ShowNextLevelUp();
            return;
        }

        // 몰아서 뜬 카드를 전부 골랐다. 미뤄둔 보스 보상과 웨이브 경고가 이 순간에 나간다.
        ReleaseDeferredStart();

        if (!IsOver) Time.timeScale = 1f;
    }

    private List<UpgradeOption> RollOptions(int count)
    {
        pool.Clear();

        // 수치가 0이면 그 카드는 아예 만들지 않는다. "+0%" 는 뽑아봐야 손해인 함정 카드이므로,
        // 0을 "이 강화는 쓰지 않음" 스위치로 쓴다. 특정 포탑에만 주고 싶은 강화는 나머지를 0으로 두면 된다.

        // ---- 전체 포탑 강화 ----
        if (allDamageStep > 0f)
            pool.Add(new UpgradeOption(UpgradeType.AllDamage,
                "전체 공격력 +" + Percent(allDamageStep), "모든 포탑이 더 세게 때린다", neutralCardColor));

        if (allFireRateStep > 0f)
            pool.Add(new UpgradeOption(UpgradeType.AllFireRate,
                "전체 공격속도 +" + Percent(allFireRateStep), "모든 포탑이 더 빨리 쏜다", neutralCardColor));

        if (allRangeStep > 0f)
            pool.Add(new UpgradeOption(UpgradeType.AllRange,
                "전체 사거리 +" + Percent(allRangeStep), "모든 포탑이 더 멀리 닿는다", neutralCardColor));

        // ---- 플레이어 ----
        if (playerSpeedStep > 0f)
            pool.Add(new UpgradeOption(UpgradeType.PlayerSpeed,
                "이동 속도 +" + playerSpeedStep.ToString("0.#"), "적을 더 쉽게 피한다", playerCardColor));

        // ---- 포탑별 ----
        if (turretChoices != null)
        {
            for (int i = 0; i < turretChoices.Length; i++)
            {
                TurretDef choice = turretChoices[i];
                if (choice == null || choice.Prefab == null) continue;

                int owned = CountTurretsLike(choice.Prefab);

                if (owned < EffectiveMaxCount(choice))
                {
                    pool.Add(MakeTurretOption(UpgradeType.NewTurret, i,
                        choice.DisplayName + " 소환", choice.Description));
                }

                // 가지고 있지 않은 포탑, 그리고 강화 상한에 닿은 포탑은 강화 카드를 내지 않는다.
                if (owned <= 0) continue;
                if (GetUpgradeCount(i) >= Mathf.Max(1, choice.MaxUpgrades)) continue;

                // 어느 포탑인지는 제목과 아이콘에 이미 나와 있으므로, 설명에는 실제 효과만 적는다.
                if (choice.DamageStep > 0f)
                    pool.Add(MakeTurretOption(UpgradeType.TypeDamage, i,
                        choice.DisplayName + " 공격력 +" + Percent(choice.DamageStep),
                        "공격력이 " + Percent(choice.DamageStep) + " 증가한다"));

                if (choice.FireRateStep > 0f)
                    pool.Add(MakeTurretOption(UpgradeType.TypeFireRate, i,
                        choice.DisplayName + " 공격속도 +" + Percent(choice.FireRateStep),
                        "공격속도가 " + Percent(choice.FireRateStep) + " 빨라진다"));

                // 사거리는 종류당 한 번뿐이다. 한 포탑의 사거리가 운으로 연달아 늘어나는 것을 막는다.
                if (choice.RangeStep > 0f && !IsRangeTaken(i))
                    pool.Add(MakeTurretOption(UpgradeType.TypeRange, i,
                        choice.DisplayName + " 사거리 +" + Percent(choice.RangeStep),
                        "사거리가 " + Percent(choice.RangeStep) + " 증가한다 (종류당 1회)"));
            }
        }

        List<UpgradeOption> result = new List<UpgradeOption>(count);

        // 별을 다 채운 포탑의 특수 강화는 무조건 자리를 차지한다. 가중치 추첨을 거치지 않는다.
        if (turretChoices != null)
        {
            // 두 번째 특수가 먼저다. 일반 강화를 다 채운 보상이라 우선순위가 높다.
            // 후보가 여럿이어도 한 장만 뽑는다. 다 깔면 카드 세 장이 전부 2특이 되어 고를 것이 없어진다.
            if (result.Count < count) TryAddSpecial2(result);

            for (int i = 0; i < turretChoices.Length && result.Count < count; i++)
            {
                if (!IsSpecialReady(i)) continue;
                result.Add(MakeSpecialOption(i));
            }
        }

        // 세 번째 특수도 확정 등장이다. 다만 한 번에 한 장이고, 먹으면 몇 레벨 쉰다.
        if (result.Count < count) TryAddSpecial3(result);

        DrawWeighted(pool, result, count);
        return result;
    }

    /// <summary>이 포탑을 지금 몇 개까지 놓을 수 있는가. 표를 넘어선 웨이브부터는 한 개씩 더 준다.</summary>
    private int EffectiveMaxCount(TurretDef def)
    {
        int baseMax = Mathf.Max(1, def.MaxCount);
        return HasExtraTurretSlot ? baseMax + extendedWaveExtraTurrets : baseMax;
    }

    /// <summary>
    /// 가중치 추첨. 소환 카드만 보너스를 얹어 조금 더 자주 나오고, 나머지는 균등하다.
    /// 특수 강화 카드들은 여기 오기 전에 확정 슬롯으로 이미 자리를 잡았다.
    /// 한 장 뽑을 때마다 목록이 줄어들므로 매번 다시 계산한다.
    /// </summary>
    private void DrawWeighted(List<UpgradeOption> source, List<UpgradeOption> into, int count)
    {
        int commonPicked = 0;
        for (int i = 0; i < into.Count; i++) if (IsCommonCard(into[i].Type)) commonPicked++;

        while (into.Count < count && source.Count > 0)
        {
            // 공용 카드(전체 강화 3종 + 이동 속도)가 판을 통째로 덮으면 고를 것이 없는 카드가 된다.
            // 다른 후보가 남아 있는 한 공용은 count - 1 장까지만 허용한다.
            if (commonPicked >= count - 1) DropCommonCardsIfOthersLeft(source);
            if (source.Count == 0) break;

            BuildDrawWeights(source);

            float total = 0f;
            for (int i = 0; i < drawWeights.Count; i++) total += drawWeights[i];

            if (total <= 0f)
            {
                if (IsCommonCard(source[0].Type)) commonPicked++;
                into.Add(source[0]);
                source.RemoveAt(0);
                continue;
            }

            float roll = UnityEngine.Random.value * total;

            int picked = source.Count - 1;
            for (int i = 0; i < drawWeights.Count; i++)
            {
                roll -= drawWeights[i];
                if (roll <= 0f) { picked = i; break; }
            }

            if (IsCommonCard(source[picked].Type)) commonPicked++;

            into.Add(source[picked]);
            source.RemoveAt(picked);
        }
    }

    /// <summary>
    /// 소환 카드 한 장의 확률을 "균등값 x (1 + 보너스)"로 못박고 남은 몫을 나머지가 똑같이 나눠 갖는다.
    /// 가중치를 1.15로 그냥 주면 분모도 같이 커져 보너스가 희석되므로 이렇게 계산한다.
    /// </summary>
    private void BuildDrawWeights(List<UpgradeOption> source)
    {
        drawWeights.Clear();

        int n = source.Count;

        int summon = 0;
        for (int i = 0; i < n; i++)
        {
            if (source[i].Type == UpgradeType.NewTurret) summon++;
        }

        float even = n > 0 ? 1f / n : 0f;
        float summonEach = even * (1f + newTurretCardBonus);
        float rest = 1f - summonEach * summon;

        // 소환 카드가 너무 많아 남는 몫이 없으면 보너스를 포기하고 균등하게 뽑는다.
        if (summon >= n || rest <= 0f)
        {
            summonEach = even;
            rest = 1f - summonEach * summon;
        }

        int others = n - summon;
        float otherEach = others > 0 ? rest / others : 0f;

        for (int i = 0; i < n; i++)
        {
            drawWeights.Add(source[i].Type == UpgradeType.NewTurret ? summonEach : otherEach);
        }
    }

    // ---------------- 특수 강화 / 진행도 ----------------

    /// <summary>이 포탑을 보유했고 관련 강화를 임계치만큼 쌓았는가.</summary>
    private bool IsSpecialReady(int choiceIndex)
    {
        TurretDef choice = GetChoice(choiceIndex);
        if (choice == null || choice.Prefab == null) return false;
        if (IsSpecialTaken(choiceIndex)) return false;          // 한 판에 한 번뿐
        if (CountTurretsLike(choice.Prefab) <= 0) return false;

        return GetProgress(choiceIndex) >= Mathf.Max(1, choice.SpecialThreshold);
    }

    /// <summary>일반 강화를 상한까지 다 채웠는가. 채웠으면 두 번째 특수가 확정 등장한다.
    /// 단 첫 보스를 잡기 전에는 조건을 채워도 나오지 않는다.</summary>
    private bool IsSpecial2Ready(int choiceIndex)
    {
        if (!Special2Unlocked) return false;                     // 보스를 잡아야 열린다

        TurretDef choice = GetChoice(choiceIndex);
        if (choice == null || choice.Prefab == null) return false;
        if (IsSpecial2Taken(choiceIndex)) return false;         // 한 판에 한 번뿐
        if (CountTurretsLike(choice.Prefab) <= 0) return false;

        // 첫 특수를 건너뛰고 두 번째만 먹는 것은 막는다.
        // 이 검사가 없으면 1특을 고르지 않은 채 강화 상한을 채운 포탑이 2특을 먼저 받아,
        // 다이아몬드 게이지가 1특 쪽을 가리키는데 2특 카드가 뜨는 상태가 된다.
        if (!IsSpecialTaken(choiceIndex)) return false;

        // 제목이 비어 있으면 이 포탑에는 두 번째 특수가 아직 없다는 뜻이다. 빈 카드를 내지 않는다.
        if (!HasSpecial2(choiceIndex)) return false;

        return GetUpgradeCount(choiceIndex) >= Mathf.Max(1, choice.MaxUpgrades);
    }

    /// <summary>이 포탑에 두 번째 특수가 준비돼 있는가. 프리팹의 제목이 비어 있으면 아직 없다는 뜻이다.</summary>
    private bool HasSpecial2(int choiceIndex)
    {
        TurretDef choice = GetChoice(choiceIndex);
        return choice != null && choice.Prefab != null && !string.IsNullOrEmpty(choice.Prefab.Special2Title);
    }

    private UpgradeOption MakeSpecial2Option(int choiceIndex)
    {
        TurretDef choice = turretChoices[choiceIndex];
        int max = Mathf.Max(1, choice.MaxUpgrades);

        // 첫 특수와 똑같이 다 채워진 다이아몬드를 보여준다. 이 카드가 뜬 이유가 곧 그 칸이다.
        return new UpgradeOption(UpgradeType.TypeSpecial2,
            choice.Prefab.Special2Title,
            choice.Prefab.Special2Description,
            choice.CardColor,
            choiceIndex,
            max,
            max,
            GetUpgradeCount(choiceIndex),
            max,
            choice.CurrentCardIcon);
    }

    private bool IsSpecial2Taken(int choiceIndex)
    {
        return special2Taken != null && choiceIndex >= 0 && choiceIndex < special2Taken.Length
               && special2Taken[choiceIndex];
    }

    /// <summary>세 번째 특수 카드를 지금 낼 수 있는가. 확정 등장이 아니라 추첨에 섞이는 것이다.</summary>
    private bool IsSpecial3Available(int choiceIndex)
    {
        if (!Special3Unlocked) return false;                     // 정해진 보스를 잡아야 열린다

        TurretDef choice = GetChoice(choiceIndex);
        if (choice == null || choice.Prefab == null) return false;
        if (IsSpecial3Taken(choiceIndex)) return false;          // 한 판에 한 번뿐
        if (CountTurretsLike(choice.Prefab) <= 0) return false;

        // 제목이 비어 있으면 이 포탑에는 세 번째 특수가 아직 없다는 뜻이다.
        if (string.IsNullOrEmpty(choice.Prefab.Special3Title)) return false;

        // 앞의 두 특수를 건너뛰고 세 번째만 먹는 것은 막는다.
        return IsSpecialTaken(choiceIndex) && IsSpecial2Taken(choiceIndex);
    }

    /// <summary>
    /// 두 번째 특수 카드 한 장을 확정으로 끼워 넣는다.
    /// 준비된 포탑이 여럿이면 그중 하나를 무작위로 고른다. 세 번째 특수와 같은 방식이다.
    /// </summary>
    private void TryAddSpecial2(List<UpgradeOption> result)
    {
        if (turretChoices == null) return;

        special2Candidates.Clear();

        for (int i = 0; i < turretChoices.Length; i++)
        {
            if (IsSpecial2Ready(i)) special2Candidates.Add(i);
        }

        if (special2Candidates.Count == 0) return;

        int pick = special2Candidates[UnityEngine.Random.Range(0, special2Candidates.Count)];
        result.Add(MakeSpecial2Option(pick));
    }

    /// <summary>
    /// 조건을 만족하면 세 번째 특수 카드 한 장을 확정으로 끼워 넣는다.
    /// 후보가 여럿이면 그중 하나를 무작위로 고른다. 확률 추첨이 아니라 무조건 한 장이다.
    /// </summary>
    private void TryAddSpecial3(List<UpgradeOption> result)
    {
        if (turretChoices == null) return;

        // 방금 하나 먹었으면 정해진 횟수만큼 일반 카드로만 채운다.
        if (special3Cooldown > 0)
        {
            special3Cooldown--;
            return;
        }

        special3Candidates.Clear();

        for (int i = 0; i < turretChoices.Length; i++)
        {
            if (IsSpecial3Available(i)) special3Candidates.Add(i);
        }

        if (special3Candidates.Count == 0) return;

        int pick = special3Candidates[UnityEngine.Random.Range(0, special3Candidates.Count)];
        result.Add(MakeSpecial3Option(pick));
    }

    private bool IsSpecial3Taken(int choiceIndex)
    {
        return special3Taken != null && choiceIndex >= 0 && choiceIndex < special3Taken.Length
               && special3Taken[choiceIndex];
    }

    private UpgradeOption MakeSpecial3Option(int choiceIndex)
    {
        TurretDef choice = turretChoices[choiceIndex];
        int max = Mathf.Max(1, choice.MaxUpgrades);

        return new UpgradeOption(UpgradeType.TypeSpecial3,
            choice.Prefab.Special3Title,
            choice.Prefab.Special3Description,
            choice.CardColor,
            choiceIndex,
            max,
            max,
            GetUpgradeCount(choiceIndex),
            max,
            choice.CurrentCardIcon);
    }

    private UpgradeOption MakeSpecialOption(int choiceIndex)
    {
        TurretDef choice = turretChoices[choiceIndex];
        int threshold = Mathf.Max(1, choice.SpecialThreshold);

        return new UpgradeOption(UpgradeType.TypeSpecial,
            choice.Prefab.SpecialTitle,
            choice.Prefab.SpecialDescription,
            choice.CardColor,
            choiceIndex,
            threshold,
            threshold,
            GetUpgradeCount(choiceIndex),
            Mathf.Max(1, choice.MaxUpgrades),
            choice.CurrentCardIcon);
    }

    /// <summary>포탑 관련 일반 카드. 현재 진행도를 별로 함께 보여준다.</summary>
    private UpgradeOption MakeTurretOption(UpgradeType type, int choiceIndex, string title, string description)
    {
        TurretDef choice = turretChoices[choiceIndex];

        int used = GetUpgradeCount(choiceIndex);
        int max = Mathf.Max(1, choice.MaxUpgrades);

        if (IsSpecialTaken(choiceIndex))
        {
            // 첫 특수를 가져간 뒤에는 다이아몬드가 두 번째 특수를 향해 다시 채워진다.
            // 조건이 "일반 강화를 상한까지"이므로 칸 수도 강화 상한을 그대로 쓴다. 한 칸이 강화 한 번이다.
            // 잠겨 있던 동안 쌓인 강화도 그대로 세므로, 해금되는 순간 이미 채워진 채로 등장한다.
            // 아직 잠겨 있으면 숨긴다. 채워봐야 나오지 않는 칸을 보여주면 속인 것이 된다.
            if (Special2Unlocked && HasSpecial2(choiceIndex) && !IsSpecial2Taken(choiceIndex))
                return new UpgradeOption(type, title, description, choice.CardColor,
                    choiceIndex, Mathf.Min(used, max), max, used, max, choice.CurrentCardIcon);

            // 둘 다 가져갔으면 더 채울 칸이 없으므로 다이아몬드만 숨기고 강화 횟수는 계속 보여준다.
            return new UpgradeOption(type, title, description, choice.CardColor,
                choiceIndex, -1, 0, used, max, choice.CurrentCardIcon);
        }

        // 1특을 아직 안 먹은 포탑은 2특이 열렸든 아니든 1특 게이지를 그대로 쓴다.
        int threshold = Mathf.Max(1, choice.SpecialThreshold);

        return new UpgradeOption(type, title, description, choice.CardColor,
            choiceIndex, Mathf.Min(GetProgress(choiceIndex), threshold), threshold, used, max,
            choice.CurrentCardIcon);
    }

    /// <summary>어느 포탑에도 묶이지 않은 공용 카드인가. 전체 강화 3종과 이동 속도가 여기 든다.</summary>
    private static bool IsCommonCard(UpgradeType type)
    {
        return type == UpgradeType.AllDamage
               || type == UpgradeType.AllFireRate
               || type == UpgradeType.AllRange
               || type == UpgradeType.PlayerSpeed;
    }

    /// <summary>공용이 아닌 후보가 남아 있으면 공용 카드를 후보에서 빼버린다. 없으면 그대로 둔다.</summary>
    private static void DropCommonCardsIfOthersLeft(List<UpgradeOption> source)
    {
        bool hasOther = false;
        for (int i = 0; i < source.Count; i++)
        {
            if (!IsCommonCard(source[i].Type)) { hasOther = true; break; }
        }

        if (!hasOther) return;

        for (int i = source.Count - 1; i >= 0; i--)
        {
            if (IsCommonCard(source[i].Type)) source.RemoveAt(i);
        }
    }

    private bool IsRangeTaken(int choiceIndex)
    {
        return rangeTaken != null && choiceIndex >= 0 && choiceIndex < rangeTaken.Length && rangeTaken[choiceIndex];
    }

    private bool IsSpecialTaken(int choiceIndex)
    {
        return specialTaken != null && choiceIndex >= 0 && choiceIndex < specialTaken.Length && specialTaken[choiceIndex];
    }

    private int GetUpgradeCount(int choiceIndex)
    {
        if (typeUpgradeCount == null || choiceIndex < 0 || choiceIndex >= typeUpgradeCount.Length) return 0;
        return typeUpgradeCount[choiceIndex];
    }

    private int GetProgress(int choiceIndex)
    {
        if (typeProgress == null || choiceIndex < 0 || choiceIndex >= typeProgress.Length) return 0;
        return typeProgress[choiceIndex];
    }

    /// <summary>포탑 관련 카드를 고를 때마다 진행도를 올리고, 특수 강화를 쓰면 0으로 되돌린다.</summary>
    private void AdvanceTypeProgress(UpgradeOption option)
    {
        int index = option.TurretIndex;
        if (typeProgress == null || index < 0 || index >= typeProgress.Length) return;

        if (option.Type == UpgradeType.TypeSpecial)
        {
            typeProgress[index] = 0;
            if (specialTaken != null && index < specialTaken.Length) specialTaken[index] = true;
            return;
        }

        if (option.Type == UpgradeType.TypeSpecial2)
        {
            if (special2Taken != null && index < special2Taken.Length) special2Taken[index] = true;
            return;
        }

        if (option.Type == UpgradeType.TypeSpecial3)
        {
            if (special3Taken != null && index < special3Taken.Length) special3Taken[index] = true;

            // 먹은 순간부터 쉬는 구간이 시작된다. 다음 레벨업부터 이 수만큼 일반 카드만 나온다.
            special3Cooldown = Mathf.Max(0, special3CooldownLevels);
            return;
        }

        // 사거리는 한 번 가져가면 그 포탑의 사거리 카드가 다시 나오지 않는다.
        if (option.Type == UpgradeType.TypeRange
            && rangeTaken != null && index < rangeTaken.Length) rangeTaken[index] = true;

        // 포탑 추가는 "강화"가 아니므로 상한에도, 별에도 포함하지 않는다.
        bool isStatUpgrade =
            option.Type == UpgradeType.TypeDamage ||
            option.Type == UpgradeType.TypeFireRate ||
            option.Type == UpgradeType.TypeRange;

        if (!isStatUpgrade) return;

        // 상한은 특수 강화를 가져갔든 아니든 항상 센다. 여기서 빠지면 5회 제한이 무력화된다.
        if (typeUpgradeCount != null && index < typeUpgradeCount.Length) typeUpgradeCount[index]++;

        // 별은 특수 강화를 이미 가져갔으면 더 쌓지 않는다.
        if (IsSpecialTaken(index)) return;

        typeProgress[index]++;
    }

    private TurretDef GetChoice(int choiceIndex)
    {
        if (turretChoices == null || choiceIndex < 0 || choiceIndex >= turretChoices.Length) return null;
        return turretChoices[choiceIndex];
    }

    private Type GetChoiceKind(int choiceIndex)
    {
        if (turretChoices == null || choiceIndex < 0 || choiceIndex >= turretChoices.Length) return null;

        TurretDef choice = turretChoices[choiceIndex];
        return choice != null && choice.Prefab != null ? choice.Prefab.GetType() : null;
    }

    /// <summary>같은 종류(같은 스크립트 타입)의 포탑이 현재 몇 개인지.</summary>
    private static int CountTurretsLike(TurretBase prefab)
    {
        if (prefab == null) return 0;

        Type kind = prefab.GetType();
        int count = 0;

        var all = TurretBase.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && all[i].GetType() == kind) count++;
        }

        return count;
    }

    private static string Percent(float ratio) => Mathf.RoundToInt(ratio * 100f) + "%";

    private void SpawnTurret(int choiceIndex)
    {
        if (turretChoices == null || choiceIndex < 0 || choiceIndex >= turretChoices.Length) return;

        TurretDef choice = turretChoices[choiceIndex];

        TurretBase prefab = choice.Prefab;
        if (prefab == null) return;

        Vector3 origin = PlayerController.Instance != null
            ? PlayerController.Instance.transform.position
            : Vector3.zero;

        float angle = UnityEngine.Random.value * Mathf.PI * 2f;
        Vector3 position = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * newTurretDistance;

        Vector2 bounds = ArenaBounds.HalfSize;
        position.x = Mathf.Clamp(position.x, -bounds.x, bounds.x);
        position.z = Mathf.Clamp(position.z, -bounds.y, bounds.y);
        position.y = 0f;

        Instantiate(prefab, position, Quaternion.identity);

        // 포탑 전용 등장음이 있으면 그걸, 없으면 공용 등장음을 쓴다.
        SfxDef spawnSfx = choice.SpawnSfx != null ? choice.SpawnSfx : SfxManager.Common?.TurretSpawn;
        SfxManager.Play(spawnSfx, position);
    }
}
