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
        FinalSweep, // 마지막 웨이브 종료, 남은 적 정리 대기
        Endless,    // 웨이브를 다 깬 뒤 이어가는 무한 모드
        Cleared,
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

    [Tooltip("무한 모드에서 포탑 종류별로 더 놓을 수 있는 개수. 대포 최대 1개면 무한에서는 2개가 된다.")]
    [Min(0)] [SerializeField] private int endlessExtraTurrets = 1;

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
    [Tooltip("F1 즉시 레벨업 / F2 무적 / F3 무한 모드 / F4 결과창. 배포 전엔 끄는 게 좋다.")]
    [SerializeField] private bool enableDebugKeys = true;

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
    public int TotalWaves => spawner != null && spawner.WaveCount > 0 ? spawner.WaveCount : fallbackWaveCount;
    /// <summary>현재 상태가 끝나기까지 남은 시간. FinalSweep/Cleared/GameOver에서는 의미 없음.</summary>
    public float StateTimeLeft { get; private set; }
    public bool IsOver => State == GameState.Cleared || State == GameState.GameOver;

    /// <summary>무한 모드 진행 정보. HUD가 읽는다.</summary>
    /// <summary>이 판이 무한 모드까지 갔는가. 게임 오버 뒤에도 남아야 해서 스포너에게 묻는다.</summary>
    public bool ReachedEndless => spawner != null && spawner.IsEndless;

    public int EndlessStep => spawner != null ? spawner.EndlessStep : 0;
    public float EndlessElapsed => spawner != null ? spawner.EndlessElapsed : 0f;
    public int OpenZoneCount => spawner != null ? spawner.OpenZoneCount : 0;

    public event Action OnStatsChanged;

    private static readonly TurretDef[] NoTurrets = new TurretDef[0];

    /// <summary>데이터베이스의 포탑 목록. 이름을 유지해 아래 로직을 그대로 쓴다.</summary>
    private TurretDef[] turretChoices =>
        database != null && database.Turrets != null ? database.Turrets : NoTurrets;

    private int pendingLevelUps;
    private readonly List<UpgradeOption> pool = new List<UpgradeOption>();

    // 포탑 종류별로 "관련 강화를 몇 번 골랐는지". SpecialThreshold에 닿으면 특수 강화가 확정 등장한다.
    private int[] typeProgress;

    // 특수 강화는 한 판에 종류당 한 번만 가져갈 수 있다.
    private bool[] specialTaken;

    // 포탑 종류별로 쓴 일반 강화 횟수. MaxUpgrades에 닿으면 그 포탑의 강화 카드가 더 안 나온다.
    private int[] typeUpgradeCount;

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
        typeUpgradeCount = new int[choiceCount];

        Wave = 0;
        State = GameState.Menu;
        StateTimeLeft = 0f;
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

    /// <summary>스테이지를 클리어한 뒤에만 들어갈 수 있다. 결과 화면의 ENDLESS가 부른다.</summary>
    public void StartEndless()
    {
        // 디버그 키로 들어오는 경우를 빼면 클리어 상태에서만 허용한다.
        if (State != GameState.Cleared) return;

        Time.timeScale = 1f;
        State = GameState.Endless;

        if (spawner != null) spawner.BeginEndless();
        ShowBanner("무한 모드", new Color(1f, 0.55f, 0.85f));
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
                StateTimeLeft -= Time.deltaTime;
                if (StateTimeLeft > 0f) break;

                StopSpawning();

                if (Wave >= TotalWaves)
                {
                    State = GameState.FinalSweep;
                    ShowBanner("남은 적을 정리하라", new Color(1f, 0.86f, 0.36f));
                }
                else
                {
                    State = GameState.Break;
                    StateTimeLeft = GetBreakAfter(Wave);

                    // 쉬는 동안 다음 웨이브가 어디서 오는지 미리 알려준다.
                    WarnNextWaveZones(Wave + 1);
                }
                break;

            case GameState.Break:
                StateTimeLeft -= Time.deltaTime;
                if (StateTimeLeft <= 0f) StartWave(Wave + 1);
                break;

            case GameState.Endless:
                // 진행은 스포너가 스스로 한다. 여기서는 아무것도 안 해도 된다.
                break;

            case GameState.FinalSweep:
                // 마지막 웨이브 뒤 남은 적을 다 잡아야 클리어. 끝맺음이 있어야 완성본처럼 느껴진다.
                if (EnemyRegistry.Count == 0) EnterCleared();
                break;
        }
    }

    private void StartWave(int index)
    {
        Wave = index;
        State = GameState.Playing;
        StateTimeLeft = GetWaveDuration(index);

        if (spawner != null) spawner.BeginWave(index);
        ShowBanner(Wave + " 웨이브", new Color(0.55f, 0.8f, 1f));
        SfxManager.Play(SfxManager.Common?.WaveStart);
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
        if (ResultUI.Instance != null) ResultUI.Instance.Show(false);
    }

    private void EnterCleared()
    {
        State = GameState.Cleared;
        StopSpawning();
        Time.timeScale = 0f;

        SfxManager.Play(SfxManager.Common?.StageClear);
        if (ResultUI.Instance != null) ResultUI.Instance.Show(true);
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
        else if (keyboard.f3Key.wasPressedThisFrame) ForceEndless();
        else if (keyboard.f4Key.wasPressedThisFrame) ForceResult();
    }

    /// <summary>결과창을 바로 띄운다. 배치와 문구를 확인하려고 쓴다.</summary>
    private void ForceResult()
    {
        // 이미 끝난 판이면 결과창이 떠 있으므로 아무것도 하지 않는다.
        if (IsOver) return;

        EnterCleared();
    }

    /// <summary>디버그용. 플레이어 무적을 켜고 끈다. 무한 모드 밸런싱할 때 안 죽고 지켜보려고 쓴다.</summary>
    public void ToggleInvincible()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        bool on = !player.Invincible;
        player.SetInvincible(on);

        ShowBanner(on ? "무적 ON" : "무적 OFF",
            on ? new Color(1f, 0.86f, 0.36f) : new Color(0.7f, 0.72f, 0.78f));
    }

    /// <summary>디버그용. 웨이브를 건너뛰고 무한 모드로 바로 들어간다. 밸런싱할 때만 쓴다.</summary>
    public void ForceEndless()
    {
        if (State == GameState.Menu || State == GameState.Endless) return;

        State = GameState.Cleared;   // StartEndless 의 조건을 만족시킨다
        StartEndless();
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
                if (!IsSpecialReady(i)) continue;
                result.Add(MakeSpecialOption(i));
            }
        }

        DrawWeighted(pool, result, count);
        return result;
    }

    /// <summary>이 포탑을 지금 몇 개까지 놓을 수 있는가. 무한 모드에서는 한 개씩 더 준다.</summary>
    private int EffectiveMaxCount(TurretDef def)
    {
        int baseMax = Mathf.Max(1, def.MaxCount);
        return State == GameState.Endless ? baseMax + endlessExtraTurrets : baseMax;
    }

    /// <summary>
    /// 소환 카드를 조금 더 자주 뽑는다. 가중치를 1.15로 주면 분모도 같이 커져 보너스가 희석되므로,
    /// 소환 카드 한 장의 확률을 "균등값 x (1 + 보너스)"로 못박고 남은 몫을 나머지가 똑같이 나눠 갖게 한다.
    /// 카드 10장 중 소환 3장이면 소환은 각 11.5%, 나머지 7장은 남은 65.5%를 나눠 9.36%씩 된다.
    /// 한 장 뽑을 때마다 목록이 줄어드니 매번 다시 계산한다.
    /// </summary>
    private void DrawWeighted(List<UpgradeOption> source, List<UpgradeOption> into, int count)
    {
        while (into.Count < count && source.Count > 0)
        {
            int n = source.Count;

            int boosted = 0;
            for (int i = 0; i < n; i++)
            {
                if (source[i].Type == UpgradeType.NewTurret) boosted++;
            }

            float even = 1f / n;
            float summonShare = even * (1f + newTurretCardBonus);
            float rest = 1f - summonShare * boosted;

            // 소환 카드가 너무 많아 남는 몫이 없으면 보너스를 포기하고 균등하게 뽑는다.
            bool saturated = boosted >= n || rest <= 0f;
            if (saturated) summonShare = even;

            float otherShare = saturated || boosted >= n ? even : rest / (n - boosted);

            float total = summonShare * boosted + otherShare * (n - boosted);
            float roll = UnityEngine.Random.value * total;

            int picked = n - 1;
            for (int i = 0; i < n; i++)
            {
                roll -= source[i].Type == UpgradeType.NewTurret ? summonShare : otherShare;
                if (roll <= 0f) { picked = i; break; }
            }

            into.Add(source[picked]);
            source.RemoveAt(picked);
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

    private UpgradeOption MakeSpecialOption(int choiceIndex)
    {
        TurretDef choice = turretChoices[choiceIndex];
        int threshold = Mathf.Max(1, choice.SpecialThreshold);

        return new UpgradeOption(UpgradeType.TypeSpecial,
            choice.SpecialTitle,
            choice.SpecialDescription,
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
        int threshold = Mathf.Max(1, choice.SpecialThreshold);

        int used = GetUpgradeCount(choiceIndex);
        int max = Mathf.Max(1, choice.MaxUpgrades);

        // 특수 강화를 이미 가져갔으면 더 채울 별이 없으므로 별만 숨기고 강화 횟수는 계속 보여준다.
        if (IsSpecialTaken(choiceIndex))
            return new UpgradeOption(type, title, description, choice.CardColor,
                choiceIndex, -1, 0, used, max, choice.CardIcon);

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
