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

    [Header("보스")]
    [Tooltip("보스가 나오는 웨이브. 이 웨이브는 시간이 아니라 보스의 죽음으로 끝난다. 0이면 보스 없음.")]
    [Min(0)] [SerializeField] private int bossWave = 15;

    [Tooltip("보스 프리팹. 비우면 보스 웨이브를 건너뛴다.")]
    [SerializeField] private BossEnemy bossPrefab;

    [Tooltip("보스 웨이브 동안 일반 적 스폰을 멈출지. 끄면 보스와 잡몹이 같이 나온다. " +
             "적 200마리 위에 보스까지 겹치면 프레임도 가독성도 무너지므로 켜두는 것을 권한다.")]
    [SerializeField] private bool bossWaveStopsNormalSpawns = true;

    [Tooltip("그 웨이브부터 포탑 종류별로 더 놓을 수 있는 개수. 대포 최대 1개면 그 뒤로는 2개가 된다.")]
    [Min(0)] [SerializeField] private int extendedWaveExtraTurrets = 1;

    [Tooltip("세 번째 특수를 하나 먹은 뒤 몇 번의 레벨업을 일반 카드로만 채울지. " +
             "4면 다음 4레벨은 일반만 나오고 그 다음 레벨업에서 또 한 장이 확정 등장한다.")]
    [Min(0)] [SerializeField] private int special3CooldownLevels = 4;

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

    [Header("디버그")]
    [Tooltip("F1 즉시 레벨업 / F2 무적 / F3 다음 웨이브 / F4 결과창 / F5 중반 건너뛰기. 배포 전엔 끄는 게 좋다.")]
    [SerializeField] private bool enableDebugKeys = true;

    [Tooltip("F5로 건너뛸 웨이브 번호.")]
    [Min(1)] [SerializeField] private int debugSkipWave = 6;

    [Tooltip("F5로 맞출 레벨. 여기까지 필요한 XP를 한 번에 몰아줘서 카드를 연달아 고르게 한다.")]
    [Min(1)] [SerializeField] private int debugSkipLevel = 23;

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

    /// <summary>웨이브 표에 적힌 웨이브 수. 이 번호를 넘어서면 표 대신 Extended 설정이 굴린다.</summary>
    public int TableWaveCount =>
        spawner != null && spawner.TableWaveCount > 0 ? spawner.TableWaveCount : fallbackWaveCount;

    /// <summary>표를 다 쓴 뒤의 웨이브인가.</summary>
    public bool InExtendedWaves => Wave > TableWaveCount;

    /// <summary>보스를 잡았는가. 포탑 추가 슬롯이 여기 달려 있다.</summary>
    public bool BossDefeated { get; private set; }

    /// <summary>포탑을 종류당 하나씩 더 놓을 수 있는가. 보스를 잡아야 열린다.</summary>
    public bool HasExtraTurretSlot => BossDefeated;

    /// <summary>이 번호가 보스 웨이브인가.</summary>
    public bool IsBossWave(int waveNumber) => bossWave > 0 && bossPrefab != null && waveNumber == bossWave;

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

    // 세 번째 특수 후보 목록. 여러 포탑이 동시에 준비되면 그중 하나를 뽑는다.
    private readonly List<int> special3Candidates = new List<int>(8);

    // 포탑 종류별로 "관련 강화를 몇 번 골랐는지". SpecialThreshold에 닿으면 특수 강화가 확정 등장한다.
    private int[] typeProgress;

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
        specialTaken = new bool[choiceCount];
        special2Taken = new bool[choiceCount];
        special3Taken = new bool[choiceCount];
        special3Cooldown = 0;
        typeUpgradeCount = new int[choiceCount];

        Wave = 0;
        State = GameState.Menu;
        StateTimeLeft = 0f;
        BossDefeated = false;
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

        HandleDebugInput();

        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        if (!player.IsAlive)
        {
            EnterGameOver();
            return;
        }

        Elapsed += Time.deltaTime;
        TickWaves();
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

    private void StartBossWave()
    {
        if (GameHud.Instance != null) GameHud.Instance.HideBossWarning();

        // 보스 웨이브에도 스포너는 웨이브 번호를 알아야 한다. 뒤 웨이브 난이도가 번호에서 나오기 때문이다.
        if (spawner != null) spawner.BeginWave(Wave, bossWaveStopsNormalSpawns);

        // 보스는 웨이브 체력 배율을 타지 않는다. 밸런싱할 숫자를 프리팹 하나로 묶어둔다.
        EnemyBase.SetHpMultiplier(1f);

        SpawnBoss();

        ShowBanner("보스 등장", new Color(1f, 0.35f, 0.35f));
        SfxManager.Play(SfxManager.Common?.WaveStart);
    }

    /// <summary>스폰 구역 8곳 중 한 곳에서 보스를 내보낸다.</summary>
    private void SpawnBoss()
    {
        if (bossPrefab == null || spawner == null) return;

        int zone = UnityEngine.Random.Range(1, EnemySpawner.ZoneCount + 1);

        Vector3 a, b;
        spawner.GetZoneSegment(zone, out a, out b);

        Instantiate(bossPrefab, Vector3.Lerp(a, b, UnityEngine.Random.value), Quaternion.identity);
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
        if (BossDefeated) return;

        BossDefeated = true;

        ShowBanner("포탑을 하나씩 더 놓을 수 있다!", new Color(1f, 0.86f, 0.36f));
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
        if (IsBossWave(waveNumber))
        {
            if (GameHud.Instance != null) GameHud.Instance.ShowBossWarning(StateTimeLeft);
            return;
        }

        if (spawner == null || SpawnZoneWarning.Instance == null) return;

        SpawnZoneWarning.Instance.Warn(spawner.GetWaveZones(waveNumber));
    }

    private static void ShowBanner(string text, Color color)
    {
        if (GameHud.Instance != null) GameHud.Instance.ShowBanner(text, color);
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

        if (pendingLevelUps > 0) ShowNextLevelUp();
        else if (!IsOver) Time.timeScale = 1f;
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

                if (choice.RangeStep > 0f)
                    pool.Add(MakeTurretOption(UpgradeType.TypeRange, i,
                        choice.DisplayName + " 사거리 +" + Percent(choice.RangeStep),
                        "사거리가 " + Percent(choice.RangeStep) + " 증가한다"));
            }
        }

        List<UpgradeOption> result = new List<UpgradeOption>(count);

        // 별을 다 채운 포탑의 특수 강화는 무조건 자리를 차지한다. 가중치 추첨을 거치지 않는다.
        if (turretChoices != null)
        {
            for (int i = 0; i < turretChoices.Length && result.Count < count; i++)
            {
                // 두 번째 특수가 먼저다. 일반 강화를 다 채운 보상이라 우선순위가 높다.
                if (IsSpecial2Ready(i)) { result.Add(MakeSpecial2Option(i)); continue; }

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
        while (into.Count < count && source.Count > 0)
        {
            BuildDrawWeights(source);

            float total = 0f;
            for (int i = 0; i < drawWeights.Count; i++) total += drawWeights[i];

            if (total <= 0f)
            {
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

    /// <summary>일반 강화를 상한까지 다 채웠는가. 채웠으면 두 번째 특수가 확정 등장한다.</summary>
    private bool IsSpecial2Ready(int choiceIndex)
    {
        TurretDef choice = GetChoice(choiceIndex);
        if (choice == null || choice.Prefab == null) return false;
        if (IsSpecial2Taken(choiceIndex)) return false;         // 한 판에 한 번뿐
        if (CountTurretsLike(choice.Prefab) <= 0) return false;

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
            choice.CardIcon);
    }

    private bool IsSpecial2Taken(int choiceIndex)
    {
        return special2Taken != null && choiceIndex >= 0 && choiceIndex < special2Taken.Length
               && special2Taken[choiceIndex];
    }

    /// <summary>
    /// 카드 풀에 공용 강화만 남았는가. 두 조건을 다 만족해야 한다.
    ///   1) 모든 포탑을 놓을 수 있는 만큼 다 소환했다  -> 소환 카드가 사라진다
    ///   2) 모든 포탑의 일반 강화를 상한까지 다 썼다    -> 포탑별 강화 카드가 사라진다
    /// 이때부터 세 번째 특수를 섞어줄 자리가 생긴다.
    /// </summary>
    private bool OnlyCommonCardsLeft()
    {
        if (turretChoices == null) return false;

        bool anyTurret = false;

        for (int i = 0; i < turretChoices.Length; i++)
        {
            TurretDef choice = turretChoices[i];
            if (choice == null || choice.Prefab == null) continue;

            anyTurret = true;

            // 아직 더 놓을 수 있으면 소환 카드가 남아 있다.
            if (CountTurretsLike(choice.Prefab) < EffectiveMaxCount(choice)) return false;

            // 아직 강화 여지가 있으면 그 포탑 카드가 남아 있다.
            if (GetUpgradeCount(i) < Mathf.Max(1, choice.MaxUpgrades)) return false;
        }

        return anyTurret;
    }

    /// <summary>세 번째 특수 카드를 지금 낼 수 있는가. 확정 등장이 아니라 추첨에 섞이는 것이다.</summary>
    private bool IsSpecial3Available(int choiceIndex)
    {
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
    /// 조건을 만족하면 세 번째 특수 카드 한 장을 확정으로 끼워 넣는다.
    /// 후보가 여럿이면 그중 하나를 무작위로 고른다. 확률 추첨이 아니라 무조건 한 장이다.
    /// </summary>
    private void TryAddSpecial3(List<UpgradeOption> result)
    {
        if (turretChoices == null) return;

        // 소환 카드와 포탑별 강화 카드가 모두 사라진 뒤부터 열린다.
        if (!OnlyCommonCardsLeft()) return;

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
            choice.CardIcon);
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
            choice.CardIcon);
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
            // 조건이 "일반 강화를 상한까지"이므로 칸 수도 강화 상한을 그대로 쓴다.
            if (HasSpecial2(choiceIndex) && !IsSpecial2Taken(choiceIndex))
                return new UpgradeOption(type, title, description, choice.CardColor,
                    choiceIndex, Mathf.Min(used, max), max, used, max, choice.CardIcon);

            // 둘 다 가져갔으면 더 채울 칸이 없으므로 다이아몬드만 숨기고 강화 횟수는 계속 보여준다.
            return new UpgradeOption(type, title, description, choice.CardColor,
                choiceIndex, -1, 0, used, max, choice.CardIcon);
        }

        int threshold = Mathf.Max(1, choice.SpecialThreshold);

        return new UpgradeOption(type, title, description, choice.CardColor,
            choiceIndex, Mathf.Min(GetProgress(choiceIndex), threshold), threshold, used, max,
            choice.CardIcon);
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
