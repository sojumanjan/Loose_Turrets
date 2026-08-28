// 엑셀(CSV) <-> ScriptableObject 왕복 도구.
//
// 핵심 규칙 1: 임포터는 숫자와 문자열만 덮어쓴다. Prefab 같은 오브젝트 참조는 절대 건드리지 않는다.
//              스프레드시트에는 문자열밖에 못 쓰므로, 표에는 id만 적고 id -> 프리팹 연결은 SO에서 한 번만 해둔다.
//
// 핵심 규칙 2: 셀 안에 쉼표를 쓰지 않는다. 따옴표 처리를 하지 않는 단순 파서라서,
//              쉼표가 들어가면 칸이 밀린다. 그런 줄이 있으면 임포트/익스포트 때 경고를 띄운다.
//
// 엑셀에서 저장할 때는 "CSV UTF-8(쉼표로 분리)" 을 고르면 한글이 안 깨진다.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class GameDataCsv
{
    private const string DataFolder = "Assets/Data";

    private const string EnemiesFile = "enemies.csv";
    private const string TurretsFile = "turrets.csv";
    private const string WavesFile = "waves.csv";
    private const string LevelsFile = "levels.csv";

    // ---------------------------------------------------------------- 메뉴

    [MenuItem("Tools/Game Data/Import CSV %#i")]
    public static void Import()
    {
        GameDatabase db = FindDatabase();
        if (db == null) return;

        int changed = 0;
        changed += ImportEnemies(db);
        changed += ImportTurrets(db);
        changed += ImportWaves(db);
        changed += ImportLevels(db);

        AssetDatabase.SaveAssets();

        if (changed == 0)
        {
            Debug.LogWarning("[GameDataCsv] 갱신된 항목이 없습니다. 위에 에러가 있으면 파일이 잠겨 있거나 id가 안 맞는 것입니다.");
            return;
        }

        Debug.Log($"[GameDataCsv] 임포트 완료. 갱신된 항목 {changed}개. (Prefab 참조는 건드리지 않았습니다)");
    }

    [MenuItem("Tools/Game Data/Export CSV")]
    public static void Export()
    {
        GameDatabase db = FindDatabase();
        if (db == null) return;

        // Export는 CSV를 SO 값으로 통째로 덮어쓴다.
        // CSV에서 방금 편집한 내용이 있는데 Import를 안 했다면 그게 그대로 날아간다.
        bool ok = EditorUtility.DisplayDialog(
            "CSV 덮어쓰기",
            "SO의 현재 값으로 CSV를 전부 덮어씁니다.\n\n"
            + "엑셀에서 방금 고친 내용이 있다면 먼저 Import(Ctrl+Shift+I)를 하세요. 안 하면 그 편집은 사라집니다.\n\n"
            + "계속할까요?",
            "덮어쓰기", "취소");

        if (!ok) return;

        Directory.CreateDirectory(DataFolder);

        // 밸런스 기준값은 GameDatabase 가 들고 있다. 계산 전에 받아둔다.
        speedBaseline = Mathf.Max(0.01f, db.SpeedBaseline);

        ExportEnemies(db);
        ExportTurrets(db);
        ExportWaves(db);
        ExportLevels(db);

        AssetDatabase.Refresh();
        Debug.Log($"[GameDataCsv] 현재 SO 값을 {DataFolder} 에 CSV로 내보냈습니다.");
    }

    [MenuItem("Tools/Game Data/Reveal Data Folder")]
    public static void Reveal()
    {
        Directory.CreateDirectory(DataFolder);
        EditorUtility.RevealInFinder(Path.GetFullPath(DataFolder));
    }

    // ---------------------------------------------------------------- 적

    private static int ImportEnemies(GameDatabase db)
    {
        List<Row> rows = ReadRows(EnemiesFile);
        if (rows == null) return 0;

        int changed = 0;

        foreach (Row row in rows)
        {
            string id = row.Get("id");
            EnemyDef def = db.FindEnemy(id);

            if (def == null)
            {
                Debug.LogWarning($"[GameDataCsv] {EnemiesFile}: id '{id}' 에 해당하는 EnemyDef를 찾지 못했습니다. 건너뜁니다.");
                continue;
            }

            Undo.RecordObject(def, "Import Enemy Data");

            def.MaxHp = row.Float("maxHp", def.MaxHp);
            def.MoveSpeed = row.Float("moveSpeed", def.MoveSpeed);
            def.XpReward = row.Int("xpReward", def.XpReward);
            def.ContactDamage = row.Float("contactDamage", def.ContactDamage);
            def.ContactRadius = row.Float("contactRadius", def.ContactRadius);
            def.ContactInterval = row.Float("contactInterval", def.ContactInterval);
            def.SeparationRadius = row.Float("separationRadius", def.SeparationRadius);
            def.SeparationStrength = row.Float("separationStrength", def.SeparationStrength);

            EditorUtility.SetDirty(def);
            changed++;
        }

        return changed;
    }

    private static void ExportEnemies(GameDatabase db)
    {
        // 데이터 A~I, 빈 칸 J~N, 밸런스 수식 O부터.
        List<EnemyDef> defs = ValidEnemies(db);

        int rowCount = defs.Count;
        int assumeRow = rowCount + 3;      // 데이터 뒤 빈 줄 하나 + #ASSUMPTIONS
        int speedRow = assumeRow + 1;

        StringBuilder sb = new StringBuilder();

        sb.AppendLine(Join("id", "maxHp", "moveSpeed", "xpReward", "contactDamage", "contactRadius",
                           "contactInterval", "separationRadius", "separationStrength",
                           "", "", "", "", "",
                           "aggro", "contactDps", "speedFactor", "threat", "xpPerThreat"));

        for (int i = 0; i < defs.Count; i++)
        {
            EnemyDef def = defs[i];
            string r = (i + 2).ToString();

            sb.AppendLine(Join(def.Id, N(def.MaxHp), N(def.MoveSpeed), def.XpReward.ToString(),
                               N(def.ContactDamage), N(def.ContactRadius), N(def.ContactInterval),
                               N(def.SeparationRadius), N(def.SeparationStrength),
                               "", "", "", "", "",
                               N(Aggro(def)),
                               "=E" + r + "/G" + r,
                               "=C" + r + "/$B$" + speedRow,
                               "=B" + r + "*P" + r + "*Q" + r + "*O" + r + "/10",
                               "=D" + r + "/R" + r));
        }

        // 가정값 블록. 첫 칸이 #라 임포터가 건너뛴다.
        sb.AppendLine();
        sb.AppendLine("#ASSUMPTIONS");
        sb.AppendLine(Join("#speedBaseline", N(speedBaseline), "속도 계수 = moveSpeed / 이 값 · GameDatabase 에셋에서 수정"));
        sb.AppendLine(Join("#threat", "체력 x 접촉DPS x 속도계수 x aggro / 10", ""));
        sb.AppendLine(Join("#aggro", "프리팹에서 계산됨 · 배회형은 안 쫓으므로 할인 · Export 때 갱신", ""));

        WriteFile(EnemiesFile, sb.ToString());
    }

    private static List<EnemyDef> ValidEnemies(GameDatabase db)
    {
        List<EnemyDef> result = new List<EnemyDef>();
        if (db.Enemies == null) return result;

        foreach (EnemyDef def in db.Enemies)
        {
            if (def != null && !string.IsNullOrEmpty(def.Id)) result.Add(def);
        }

        return result;
    }


    // ---------------------------------------------------------------- 포탑

    private static int ImportTurrets(GameDatabase db)
    {
        List<Row> rows = ReadRows(TurretsFile);
        if (rows == null) return 0;

        int changed = 0;

        foreach (Row row in rows)
        {
            string id = row.Get("id");
            TurretDef def = db.FindTurret(id);

            if (def == null)
            {
                Debug.LogWarning($"[GameDataCsv] {TurretsFile}: id '{id}' 에 해당하는 TurretDef를 찾지 못했습니다. 건너뜁니다.");
                continue;
            }

            Undo.RecordObject(def, "Import Turret Data");

            def.DisplayName = row.Get("displayName", def.DisplayName);
            def.Description = row.Get("description", def.Description);
            def.CardColor = row.Color("cardColor", def.CardColor);

            def.Range = row.Float("range", def.Range);
            def.FireInterval = row.Float("fireInterval", def.FireInterval);
            def.Damage = row.Float("damage", def.Damage);

            def.AoeTargets = row.Int("aoeTargets", def.AoeTargets);
            def.AoeFalloff = Mathf.Clamp(row.Float("aoeFalloff", def.AoeFalloff), 0.01f, 0.999f);
            def.SpecialAmount = row.Int("specialAmount", def.SpecialAmount);

            def.DamageStep = row.Float("damageStep", def.DamageStep);
            def.FireRateStep = row.Float("fireRateStep", def.FireRateStep);
            def.RangeStep = row.Float("rangeStep", def.RangeStep);

            def.MaxCount = row.Int("maxCount", def.MaxCount);
            def.MaxUpgrades = row.Int("maxUpgrades", def.MaxUpgrades);
            def.SpecialThreshold = row.Int("specialThreshold", def.SpecialThreshold);

            def.SpecialTitle = row.Get("specialTitle", def.SpecialTitle);
            def.SpecialDescription = row.Get("specialDescription", def.SpecialDescription);

            EditorUtility.SetDirty(def);
            changed++;
        }

        return changed;
    }

    private static void ExportTurrets(GameDatabase db)
    {
        // 데이터 A~R, 빈 칸 S, 밸런스 수식 T부터.
        //   A id  B displayName  C description  D cardColor
        //   E range  F fireInterval  G damage
        //   H damageStep  I fireRateStep  J rangeStep
        //   K aoeTargets  L aoeFalloff  M specialAmount
        //   N maxCount  O maxUpgrades  P specialThreshold  Q specialTitle  R specialDescription
        List<TurretDef> defs = ValidTurrets(db);

        int rowCount = defs.Count;
        int lastRow = rowCount + 1;
        int assumeRow = rowCount + 3;
        int rangeRow = assumeRow + 1;

        StringBuilder sb = new StringBuilder();

        sb.AppendLine(Join("id", "displayName", "description", "cardColor", "range", "fireInterval", "damage",
                           "damageStep", "fireRateStep", "rangeStep",
                           "aoeTargets", "aoeFalloff", "specialAmount",
                           "maxCount", "maxUpgrades", "specialThreshold", "specialTitle", "specialDescription",
                           "",
                           "aoeBase", "aoeMax", "singleDps", "crowdDps", "rangeFactor", "power",
                           "maxDmgDps", "maxRateDps", "maxSingleDps", "damageWins", "maxCrowdDps", "maxPower",
                           "teamPower", "teamMaxPower"));

        for (int i = 0; i < defs.Count; i++)
        {
            TurretDef def = defs[i];
            string r = (i + 2).ToString();

            // 등비수열 합. 감쇠가 1이면 0으로 나뉘므로 AoeFalloff 는 0.999 를 넘지 못하게 막아둔다.
            string aoeBase = "=(1-L" + r + "^K" + r + ")/(1-L" + r + ")";
            string aoeMax = "=(1-L" + r + "^(K" + r + "+M" + r + "))/(1-L" + r + ")";

            sb.AppendLine(Join(def.Id, def.DisplayName, def.Description,
                               "#" + ColorUtility.ToHtmlStringRGB(def.CardColor),
                               N(def.Range), N(def.FireInterval), N(def.Damage),
                               N(def.DamageStep), N(def.FireRateStep), N(def.RangeStep),
                               def.AoeTargets.ToString(), N(def.AoeFalloff), def.SpecialAmount.ToString(),
                               def.MaxCount.ToString(), def.MaxUpgrades.ToString(),
                               def.SpecialThreshold.ToString(), def.SpecialTitle, def.SpecialDescription,
                               "",
                               aoeBase,
                               aoeMax,
                               "=G" + r + "/F" + r,
                               "=V" + r + "*T" + r,
                               "=E" + r + "/$B$" + rangeRow,
                               "=W" + r + "*X" + r,
                               "=G" + r + "*(1+O" + r + "*H" + r + ")/F" + r,
                               "=G" + r + "/(F" + r + "/(1+O" + r + "*I" + r + "))",
                               "=MAX(Z" + r + ":AA" + r + ")",
                               "=(Z" + r + ">=AA" + r + ")*1",
                               "=AB" + r + "*U" + r,
                               "=AD" + r + "*X" + r,
                               "=Y" + r + "*N" + r,
                               "=AE" + r + "*N" + r));
        }

        sb.AppendLine();
        sb.AppendLine("#ASSUMPTIONS");
        sb.AppendLine(Join("#rangeBaseline", N(db.RangeBaseline), "사거리 계수 = range / 이 값 · GameDatabase 에셋에서 수정"));
        sb.AppendLine(Join("#aoeFalloff", "1을 쓰면 수식이 0으로 나뉜다 · 감쇠 없이 전부 같은 데미지면 0.999", ""));
        sb.AppendLine(Join("#damageWins", "1이면 데미지 몰빵이 유리 · 0이면 연사 몰빵이 유리", ""));
        sb.AppendLine();
        sb.AppendLine("#TOTALS");
        sb.AppendLine(Join("#teamPower", "=SUM(AF2:AF" + lastRow + ")", "필드에 다 깔았을 때 기본 전투력 합"));
        sb.AppendLine(Join("#teamMaxPower", "=SUM(AG2:AG" + lastRow + ")", "전부 최대 강화했을 때 · 이론상 최대치"));

        WriteFile(TurretsFile, sb.ToString());
    }

    private static List<TurretDef> ValidTurrets(GameDatabase db)
    {
        List<TurretDef> result = new List<TurretDef>();
        if (db.Turrets == null) return result;

        foreach (TurretDef def in db.Turrets)
        {
            if (def != null && !string.IsNullOrEmpty(def.Id)) result.Add(def);
        }

        return result;
    }

    /// <summary>포탑을 전부 깔고 전부 최대 강화했을 때의 전투력 합. waves 의 dpsRatio 기준값으로 쓴다.</summary>
    private static float TeamMaxPower(GameDatabase db)
    {
        float total = 0f;

        foreach (TurretDef def in ValidTurrets(db))
        {
            int upgrades = Mathf.Max(1, def.MaxUpgrades);
            float interval = Mathf.Max(0.01f, def.FireInterval);

            float dmgAllIn = def.Damage * (1f + upgrades * def.DamageStep) / interval;
            float rateAllIn = def.Damage / (interval / (1f + upgrades * def.FireRateStep));

            float maxSingle = Mathf.Max(dmgAllIn, rateAllIn);
            float maxPower = maxSingle * GetAoeFactor(def, 1) * (def.Range / Mathf.Max(0.01f, db.RangeBaseline));

            total += maxPower * def.MaxCount;
        }

        return total;
    }

    /// <summary>한 번 발사로 때리는 기대 적 수. 이제 프리팹이 아니라 TurretDef 값에서 나온다.</summary>
    private static float GetAoeFactor(TurretDef def, int specialLevel)
    {
        if (def == null) return 1f;

        int targets = Mathf.Max(1, def.AoeTargets + specialLevel * Mathf.Max(1, def.SpecialAmount));
        float falloff = Mathf.Clamp(def.AoeFalloff, 0.01f, 0.999f);

        float sum = 0f;
        float weight = 1f;

        for (int i = 0; i < targets; i++)
        {
            sum += weight;
            weight *= falloff;
        }

        return sum;
    }


    /// <summary>포탑을 전부 깔고 전부 최대 강화했을 때의 전투력 합. waves 의 dpsRatio 기준값으로 쓴다.</summary>
    // ---------------------------------------------------------------- 웨이브

    private static int ImportWaves(GameDatabase db)
    {
        List<Row> rows = ReadRows(WavesFile);
        if (rows == null || db.Waves == null) return 0;

        Undo.RecordObject(db.Waves, "Import Wave Data");

        List<WaveTable.Wave> waves = new List<WaveTable.Wave>();

        foreach (Row row in rows)
        {
            WaveTable.Wave wave = new WaveTable.Wave
            {
                Label = row.Get("label", "wave"),
                Duration = row.Float("duration", 45f),
                BreakAfter = row.Float("breakAfter", 4f),
                SpawnInterval = row.Float("spawnInterval", 1.2f),
                BatchSize = row.Int("batchSize", 1),
                MaxAliveEnemies = row.Int("maxAlive", 80),
                HpMultiplier = row.Float("hpMultiplier", 1f),
                SpawnZones = row.IntList("spawnZones")
            };

            // w_<적id> 형태의 열을 전부 찾아 가중치로 넣는다.
            List<WaveTable.EnemyWeight> weights = new List<WaveTable.EnemyWeight>();

            foreach (string column in row.Columns)
            {
                if (!column.StartsWith("w_")) continue;

                string enemyId = column.Substring(2);
                EnemyDef def = db.FindEnemy(enemyId);

                if (def == null)
                {
                    Debug.LogWarning($"[GameDataCsv] {WavesFile}: 열 '{column}' 의 적 id '{enemyId}' 를 찾지 못했습니다.");
                    continue;
                }

                weights.Add(new WaveTable.EnemyWeight { Def = def, Weight = row.Float(column, 0f) });
            }

            wave.Enemies = weights.ToArray();
            waves.Add(wave);
        }

        db.Waves.Waves = waves.ToArray();
        EditorUtility.SetDirty(db.Waves);

        return waves.Count;
    }

    private static void ExportWaves(GameDatabase db)
    {
        // 데이터 A~I, 가중치 J부터, 밸런스 수식 O부터. (적이 6종을 넘으면 밸런스 열이 오른쪽으로 밀린다)
        List<EnemyDef> enemies = ValidEnemies(db);

        List<WaveTable.Wave> waves = new List<WaveTable.Wave>();
        if (db.Waves != null && db.Waves.Waves != null)
        {
            foreach (WaveTable.Wave wave in db.Waves.Waves)
            {
                if (wave != null) waves.Add(wave);
            }
        }

        const int FirstWeightCol = 10;                              // J
        int lastWeightCol = FirstWeightCol + Mathf.Max(1, enemies.Count) - 1;
        int balanceCol = Mathf.Max(15, lastWeightCol + 2);          // 최소 O

        int rowCount = waves.Count;
        int lastRow = rowCount + 1;
        int assumeRow = rowCount + 3;
        int hpRow = assumeRow + 2;
        int thRow = assumeRow + 3;
        int xpRow = assumeRow + 4;
        int teamRow = assumeRow + 5;

        string wFirst = ColumnName(FirstWeightCol);
        string wLast = ColumnName(lastWeightCol);

        // 밸런스 열 이름
        string cSpawnRate = ColumnName(balanceCol);
        string cTotalSpawns = ColumnName(balanceCol + 1);
        string cAvgHp = ColumnName(balanceCol + 2);
        string cAvgThreat = ColumnName(balanceCol + 3);
        string cRequired = ColumnName(balanceCol + 4);
        string cTotalPower = ColumnName(balanceCol + 5);
        string cPeakPower = ColumnName(balanceCol + 6);
        string cRatio = ColumnName(balanceCol + 7);
        string cWaveXp = ColumnName(balanceCol + 8);

        StringBuilder sb = new StringBuilder();

        List<string> header = new List<string>
        {
            "wave", "label", "duration", "breakAfter", "spawnInterval", "batchSize", "maxAlive",
            "hpMultiplier", "spawnZones"
        };
        foreach (EnemyDef def in enemies) header.Add("w_" + def.Id);
        while (header.Count < balanceCol - 1) header.Add("");

        header.AddRange(new string[]
        {
            "spawnPerSec", "totalSpawns", "avgHp", "avgThreat", "requiredDps",
            "totalPower", "peakPower", "dpsRatio", "waveXp"
        });

        sb.AppendLine(Join(header.ToArray()));

        for (int i = 0; i < waves.Count; i++)
        {
            WaveTable.Wave wave = waves[i];
            string r = (i + 2).ToString();

            List<string> cells = new List<string>
            {
                (i + 1).ToString(), wave.Label, N(wave.Duration), N(wave.BreakAfter),
                N(wave.SpawnInterval), wave.BatchSize.ToString(),
                wave.MaxAliveEnemies.ToString(), N(wave.HpMultiplier), FormatZones(wave.SpawnZones)
            };

            foreach (EnemyDef def in enemies) cells.Add(N(FindWeight(wave, def.Id)));
            while (cells.Count < balanceCol - 1) cells.Add("");

            // 가중치 합이 0이어도 0으로 나누지 않게 아주 작은 값을 더한다. (IF는 쉼표 때문에 못 씀)
            string weightSum = "(SUM(" + wFirst + r + ":" + wLast + r + ")+0.000001)";

            cells.Add("=F" + r + "/E" + r);
            cells.Add("=INT(C" + r + "/E" + r + ")*F" + r);
            cells.Add("=" + Dot(enemies.Count, r, FirstWeightCol, hpRow) + "/" + weightSum + "*H" + r);
            cells.Add("=" + Dot(enemies.Count, r, FirstWeightCol, thRow) + "/" + weightSum + "*H" + r);
            cells.Add("=" + cSpawnRate + r + "*" + cAvgHp + r);
            cells.Add("=" + cTotalSpawns + r + "*" + cAvgThreat + r);
            cells.Add("=G" + r + "*" + cAvgThreat + r);
            cells.Add("=" + cRequired + r + "/$B$" + teamRow);
            cells.Add("=" + Dot(enemies.Count, r, FirstWeightCol, xpRow) + "/" + weightSum + "*" + cTotalSpawns + r);

            sb.AppendLine(Join(cells.ToArray()));
        }

        // ---- 가정값 블록 ----
        sb.AppendLine();
        sb.AppendLine("#ASSUMPTIONS");
        sb.AppendLine(Join("#note", "아래 세 줄은 enemies.csv 에서 가져와 Export 때 갱신됨", ""));

        List<string> ids = new List<string> { "#enemy" };
        List<string> hp = new List<string> { "#maxHp" };
        List<string> th = new List<string> { "#threat" };
        List<string> xp = new List<string> { "#xpReward" };

        foreach (EnemyDef def in enemies)
        {
            ids.Add(def.Id);
            hp.Add(N(def.MaxHp));
            th.Add(N(Threat(def)));
            xp.Add(def.XpReward.ToString());
        }

        sb.AppendLine(Join(ids.ToArray()));
        sb.AppendLine(Join(hp.ToArray()));
        sb.AppendLine(Join(th.ToArray()));
        sb.AppendLine(Join(xp.ToArray()));
        sb.AppendLine(Join("#teamMaxPower", N(TeamMaxPower(db)), "turrets.csv 의 이론상 최대 전투력 · Export 때 갱신"));
        sb.AppendLine();
        sb.AppendLine("#TOTALS");
        sb.AppendLine(Join("#totalXpAvailable", "=SUM(" + cWaveXp + "2:" + cWaveXp + lastRow + ")",
                           "한 판에서 얻을 수 있는 XP 총합"));
        sb.AppendLine(Join("#dpsRatio", "1을 넘으면 이론상 최대 전투력으로도 못 버팀", ""));

        WriteFile(WavesFile, sb.ToString());
    }

    /// <summary>가중치 x 상수 를 적 수만큼 더한 식. 상수는 constRow 행의 B, C, D... 칸을 본다.</summary>
    private static string Dot(int enemyCount, string row, int firstWeightCol, int constRow)
    {
        StringBuilder sb = new StringBuilder("(");

        for (int k = 0; k < Mathf.Max(1, enemyCount); k++)
        {
            if (k > 0) sb.Append("+");

            sb.Append(ColumnName(firstWeightCol + k)).Append(row)
              .Append("*$").Append(ColumnName(2 + k)).Append("$").Append(constRow);
        }

        sb.Append(")");
        return sb.ToString();
    }

    /// <summary>1 -> A, 2 -> B, 27 -> AA</summary>
    private static string ColumnName(int index)
    {
        string name = "";

        while (index > 0)
        {
            int rem = (index - 1) % 26;
            name = (char)('A' + rem) + name;
            index = (index - 1) / 26;
        }

        return name;
    }


    /// <summary>
    /// 구역 번호를 파이프로 이어 쓴다. 공백을 쓰면 엑셀이 "1 5" 를 날짜(2026-01-05)로 바꿔버린다.
    /// 쉼표는 CSV 구분자라 쓸 수 없다. 비어 있으면 빈 칸(=전 구역).
    /// </summary>
    private static string FormatZones(int[] zones)
    {
        if (zones == null || zones.Length == 0) return "";

        string[] parts = new string[zones.Length];
        for (int i = 0; i < zones.Length; i++) parts[i] = zones[i].ToString();

        return string.Join("|", parts);
    }

    private static float FindWeight(WaveTable.Wave wave, string enemyId)
    {
        if (wave.Enemies == null) return 0f;

        foreach (WaveTable.EnemyWeight entry in wave.Enemies)
        {
            if (entry != null && entry.Def != null && entry.Def.Id == enemyId) return entry.Weight;
        }

        return 0f;
    }

    // ---------------------------------------------------------------- 레벨

    private static int ImportLevels(GameDatabase db)
    {
        List<Row> rows = ReadRows(LevelsFile);
        if (rows == null || db.Levels == null) return 0;

        Undo.RecordObject(db.Levels, "Import Level Data");

        List<int> values = new List<int>();
        foreach (Row row in rows) values.Add(Mathf.Max(1, row.Int("xpToNext", 1)));

        db.Levels.XpPerLevel = values.ToArray();
        EditorUtility.SetDirty(db.Levels);

        return values.Count;
    }

    private static void ExportLevels(GameDatabase db)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(Join("level", "xpToNext"));

        if (db.Levels != null && db.Levels.XpPerLevel != null)
        {
            for (int i = 0; i < db.Levels.XpPerLevel.Length; i++)
                sb.AppendLine(Join((i + 1).ToString(), db.Levels.XpPerLevel[i].ToString()));
        }

        WriteFile(LevelsFile, sb.ToString());
    }

    // ---------------------------------------------------------------- 밸런스 리포트

    // 리포트가 어떤 가정 위에 서 있는지. 숫자가 이상하면 먼저 여기를 의심한다.
    // Export 직전에 GameDatabase 값으로 채운다. 위협도 계산이 이 값을 쓴다.
    private static float speedBaseline = 2.5f;
    private const float WandererAggro = 0.5f;     // 플레이어를 안 쫓는 적의 위협 할인

    /// <summary>
    /// 읽기 전용 밸런스 리포트. Export 할 때마다 현재 데이터로 다시 계산된다.
    /// 이 파일을 고쳐도 게임에 반영되지 않는다. Import 대상이 아니다.
    /// </summary>
    /// <summary>한 번 발사로 때리는 기대 적 수. specialLevel 은 특수 강화 횟수.</summary>
    private static float ContactDps(EnemyDef def) =>
        def.ContactDamage / Mathf.Max(0.01f, def.ContactInterval);

    private static float SpeedFactor(EnemyDef def) => def.MoveSpeed / Mathf.Max(0.01f, speedBaseline);

    private static float Aggro(EnemyDef def) =>
        def.Prefab is WandererEnemy ? WandererAggro : 1f;

    /// <summary>한 마리가 주는 부담. 체력(죽이는 시간) x 접촉 화력 x 속도 x 추적 여부.</summary>
    private static float Threat(EnemyDef def) =>
        def.MaxHp * ContactDps(def) * SpeedFactor(def) * Aggro(def) / 10f;

    /// <summary>프리팹의 private 직렬화 필드를 읽는다. 에디터 전용이라 가능한 방법.</summary>
    // ---------------------------------------------------------------- 공통

    private static GameDatabase FindDatabase()
    {
        string[] guids = AssetDatabase.FindAssets("t:GameDatabase");

        if (guids.Length == 0)
        {
            Debug.LogError("[GameDataCsv] GameDatabase 에셋을 찾지 못했습니다. 먼저 만들어 주세요.");
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<GameDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    /// <summary>첫 줄을 헤더로 읽고 나머지를 행으로 만든다. 빈 줄과 # 로 시작하는 줄은 건너뛴다.</summary>
    private static List<Row> ReadRows(string fileName)
    {
        string path = Path.Combine(DataFolder, fileName);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[GameDataCsv] {path} 가 없습니다. 건너뜁니다.");
            return null;
        }

        // BOM을 보고 UTF-8 / UTF-16 을 자동 판별한다. 엑셀 "CSV UTF-8"은 BOM이 붙는다.
        string text;
        if (!TryReadAllText(path, out text)) return null;

        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        if (lines.Length < 2)
        {
            Debug.LogWarning($"[GameDataCsv] {fileName} 에 데이터 행이 없습니다.");
            return null;
        }

        string[] header = lines[0].Split(',');
        for (int i = 0; i < header.Length; i++) header[i] = header[i].Trim();

        List<Row> rows = new List<Row>();

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] values = line.Split(',');
            string firstCell = values.Length > 0 ? values[0].Trim() : "";

            // 첫 칸이 비었거나 #로 시작하면 데이터가 아니다.
            // 엑셀이 만드는 ",,,,," 빈 줄과 파일 아래쪽 가정값 블록이 여기서 걸러진다.
            if (firstCell.Length == 0 || firstCell.StartsWith("#")) continue;

            // 칸 수가 안 맞으면 셀 안에 쉼표가 들어갔다는 뜻이다. 조용히 밀리면 찾기 어려우니 크게 알린다.
            if (values.Length != header.Length)
            {
                Debug.LogError($"[GameDataCsv] {fileName} {i + 1}번째 줄: 칸이 {values.Length}개인데 헤더는 {header.Length}개입니다. " +
                               $"셀 안에 쉼표가 들어갔는지 확인하세요.\n{line}");
            }

            rows.Add(new Row(header, values));
        }

        return rows;
    }

    /// <summary>
    /// 엑셀이 CSV를 열어두면 파일을 잠근다. FileShare.ReadWrite로 최대한 비집고 들어가되,
    /// 그래도 막히면 스택트레이스 대신 무엇을 해야 하는지 알려준다.
    /// </summary>
    private static bool TryReadAllText(string path, out string text)
    {
        text = null;

        try
        {
            // BOM을 보고 UTF-8 / UTF-16 을 자동 판별한다. 엑셀 "CSV UTF-8"은 BOM이 붙는다.
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                text = reader.ReadToEnd();
            }

            return true;
        }
        catch (IOException)
        {
            Debug.LogError($"[GameDataCsv] {Path.GetFileName(path)} 를 열 수 없습니다. " +
                           "엑셀에서 이 파일을 열어두고 있으면 닫은 뒤 다시 시도하세요. (이 파일은 건너뜁니다)");
            return false;
        }
    }

    private static void WriteFile(string fileName, string contents)
    {
        string path = Path.Combine(DataFolder, fileName);

        try
        {
            // UTF-8 BOM으로 쓴다. 엑셀이 BOM을 보고 한글을 제대로 연다.
            File.WriteAllText(path, contents, new UTF8Encoding(true));
        }
        catch (IOException)
        {
            Debug.LogError($"[GameDataCsv] {fileName} 에 쓸 수 없습니다. " +
                           "엑셀에서 이 파일을 열어두고 있으면 닫은 뒤 다시 시도하세요.");
        }
    }

    /// <summary>셀을 쉼표로 잇는다. 값 안에 쉼표가 있으면 파일이 깨지므로 경고하고 공백으로 바꾼다.</summary>
    private static string Join(params string[] cells)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == null) { cells[i] = ""; continue; }

            if (cells[i].IndexOf(',') >= 0)
            {
                Debug.LogWarning($"[GameDataCsv] 값에 쉼표가 있어 공백으로 바꿔 내보냅니다: \"{cells[i]}\"");
                cells[i] = cells[i].Replace(',', ' ');
            }
        }

        return string.Join(",", cells);
    }

    private static string N(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private class Row
    {
        private readonly Dictionary<string, string> cells = new Dictionary<string, string>();

        public readonly List<string> Columns = new List<string>();

        public Row(string[] header, string[] values)
        {
            for (int i = 0; i < header.Length; i++)
            {
                if (string.IsNullOrEmpty(header[i])) continue;

                string value = i < values.Length ? values[i].Trim() : "";
                cells[header[i]] = value;
                Columns.Add(header[i]);
            }
        }

        public string Get(string column, string fallback = "")
        {
            string value;
            return cells.TryGetValue(column, out value) && value.Length > 0 ? value : fallback;
        }

        public float Float(string column, float fallback)
        {
            string value;
            if (!cells.TryGetValue(column, out value) || value.Length == 0) return fallback;

            float parsed;
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        public int Int(string column, int fallback)
        {
            string value;
            if (!cells.TryGetValue(column, out value) || value.Length == 0) return fallback;

            int parsed;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) return parsed;

            // 엑셀이 정수를 "5.0" 으로 내보내는 경우가 있다.
            float asFloat;
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out asFloat))
                return Mathf.RoundToInt(asFloat);

            return fallback;
        }

        /// <summary>"1 2 3" 같은 숫자 목록을 읽는다. 공백 / 파이프 / 슬래시를 구분자로 받는다.</summary>
        public int[] IntList(string column)
        {
            string value = Get(column);
            if (value.Length == 0) return new int[0];

            // 엑셀이 "1 5" 를 날짜로 바꿔버리면 "2026-01-05" 같은 값이 들어온다. 조용히 넘기면 찾기 어렵다.
            if (value.Length >= 8 && value.IndexOf('-') > 0 && value.IndexOf(' ') < 0)
            {
                Debug.LogError($"[GameDataCsv] '{column}' 열의 '{value}' 는 엑셀이 날짜로 바꾼 값으로 보입니다. " +
                               "구역은 1|2 처럼 파이프로 적고 셀 서식을 텍스트로 두세요.");
                return new int[0];
            }

            string[] parts = value.Split(new char[] { ' ', '|', '/', ';' }, System.StringSplitOptions.RemoveEmptyEntries);
            List<int> result = new List<int>(parts.Length);

            foreach (string part in parts)
            {
                int parsed;
                if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                    result.Add(parsed);
                else
                    Debug.LogWarning($"[GameDataCsv] '{column}' 열의 '{part}' 를 숫자로 읽지 못했습니다.");
            }

            return result.ToArray();
        }

        public Color Color(string column, Color fallback)
        {
            string value = Get(column);
            if (value.Length == 0) return fallback;

            Color parsed;
            return ColorUtility.TryParseHtmlString(value, out parsed) ? parsed : fallback;
        }
    }
}
