// 경험치 / 레벨 / 업그레이드 적용을 담당하는 중앙 매니저. 레벨업하면 게임을 멈추고 3택 UI를 띄운다.

using System;
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
    [SerializeField] private float healAmount = 30f;

    [Header("새 포탑 배치")]
    [SerializeField] private float newTurretDistance = 2.5f;

    [Header("웨이브")]
    [Tooltip("웨이브별 세부 설정(길이 / 스폰 간격 / 개수 / 적 확률)은 EnemySpawner의 waves 배열에서 조절한다.")]
    [SerializeField] private EnemySpawner spawner;

    [Tooltip("스포너가 연결되지 않았을 때만 쓰는 예비값.")]
    [SerializeField] private int fallbackWaveCount = 5;
    [SerializeField] private float fallbackWaveDuration = 45f;
    [SerializeField] private float fallbackBreakDuration = 4f;

    [Header("디버그")]
    [Tooltip("F1 = 즉시 레벨업. 빌드에 남겨도 되지만 배포 전엔 끄는 게 좋다.")]
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

        // static 배율과 timeScale은 씬을 다시 로드해도 남는다. 새 판은 항상 여기서 초기화한다.
        TurretBase.ResetMultipliers();
        EnemyBase.ResetHpMultiplier();

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
        State = GameState.Break;
        StateTimeLeft = 0.4f;   // 곧바로 웨이브 1이 몰려온다

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
        ShowBanner("ENDLESS", new Color(1f, 0.55f, 0.85f));
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
                    ShowBanner("CLEAR THE FIELD", new Color(1f, 0.86f, 0.36f));
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
        ShowBanner("WAVE " + Wave, new Color(0.55f, 0.8f, 1f));
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
        Time.timeScale = 0f;

        if (ResultUI.Instance != null) ResultUI.Instance.Show(false);
    }

    private void EnterCleared()
    {
        State = GameState.Cleared;
        StopSpawning();
        Time.timeScale = 0f;

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
        else if (keyboard.f2Key.wasPressedThisFrame) ForceEndless();
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

    public void Restart()
    {
        // static 상태는 씬을 다시 로드해도 남으므로 여기서 전부 되돌린다.
        Time.timeScale = 1f;
        EnemyRegistry.Clear();
        TurretBase.ResetMultipliers();
        EnemyBase.ResetHpMultiplier();
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
        LevelUpUI.Instance.Show(RollOptions(3), ApplyUpgrade);
    }

    public void ApplyUpgrade(UpgradeOption option)
    {
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

            case UpgradeType.PlayerHeal:
                if (PlayerController.Instance != null) PlayerController.Instance.Heal(healAmount);
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
                "ALL DAMAGE +" + Percent(allDamageStep), "Every turret hits harder", neutralCardColor));

        if (allFireRateStep > 0f)
            pool.Add(new UpgradeOption(UpgradeType.AllFireRate,
                "ALL FIRE RATE +" + Percent(allFireRateStep), "Every turret shoots faster", neutralCardColor));

        if (allRangeStep > 0f)
            pool.Add(new UpgradeOption(UpgradeType.AllRange,
                "ALL RANGE +" + Percent(allRangeStep), "Every turret reaches farther", neutralCardColor));

        // ---- 플레이어 ----
        if (playerSpeedStep > 0f)
            pool.Add(new UpgradeOption(UpgradeType.PlayerSpeed,
                "MOVE SPEED +" + playerSpeedStep.ToString("0.#"), "Dodge more easily", playerCardColor));

        if (healAmount > 0f)
            pool.Add(new UpgradeOption(UpgradeType.PlayerHeal,
                "HEAL " + healAmount.ToString("0"), "Restore health right now", playerCardColor));

        // ---- 포탑별 ----
        if (turretChoices != null)
        {
            for (int i = 0; i < turretChoices.Length; i++)
            {
                TurretDef choice = turretChoices[i];
                if (choice == null || choice.Prefab == null) continue;

                int owned = CountTurretsLike(choice.Prefab);

                if (owned < choice.MaxCount)
                {
                    pool.Add(MakeTurretOption(UpgradeType.NewTurret, i,
                        "NEW " + choice.DisplayName, choice.Description));
                }

                // 가지고 있지 않은 포탑, 그리고 강화 상한에 닿은 포탑은 강화 카드를 내지 않는다.
                if (owned <= 0) continue;
                if (GetUpgradeCount(i) >= Mathf.Max(1, choice.MaxUpgrades)) continue;

                if (choice.DamageStep > 0f)
                    pool.Add(MakeTurretOption(UpgradeType.TypeDamage, i,
                        choice.DisplayName + " DAMAGE +" + Percent(choice.DamageStep),
                        choice.DisplayName + " turrets only"));

                if (choice.FireRateStep > 0f)
                    pool.Add(MakeTurretOption(UpgradeType.TypeFireRate, i,
                        choice.DisplayName + " FIRE RATE +" + Percent(choice.FireRateStep),
                        choice.DisplayName + " turrets only"));

                if (choice.RangeStep > 0f)
                    pool.Add(MakeTurretOption(UpgradeType.TypeRange, i,
                        choice.DisplayName + " RANGE +" + Percent(choice.RangeStep),
                        choice.DisplayName + " turrets only"));
            }
        }

        // Fisher-Yates로 섞고 앞에서 채운다.
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            UpgradeOption temp = pool[i];
            pool[i] = pool[j];
            pool[j] = temp;
        }

        List<UpgradeOption> result = new List<UpgradeOption>(count);

        // 별을 다 채운 포탑의 특수 강화는 무조건 자리를 차지한다.
        if (turretChoices != null)
        {
            for (int i = 0; i < turretChoices.Length && result.Count < count; i++)
            {
                if (!IsSpecialReady(i)) continue;
                result.Add(MakeSpecialOption(i));
            }
        }

        for (int i = 0; i < pool.Count && result.Count < count; i++) result.Add(pool[i]);
        return result;
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
            Mathf.Max(1, choice.MaxUpgrades));
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
                choiceIndex, -1, 0, used, max);

        return new UpgradeOption(type, title, description, choice.CardColor,
            choiceIndex, Mathf.Min(GetProgress(choiceIndex), threshold), threshold, used, max);
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

        TurretBase prefab = turretChoices[choiceIndex].Prefab;
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
    }
}
