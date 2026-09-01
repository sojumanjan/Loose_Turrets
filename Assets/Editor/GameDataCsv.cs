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
        List<EnemyDef> defs = ValidEnemies(db);

        StringBuilder sb = new StringBuilder();

        sb.AppendLine(Join("id", "maxHp", "moveSpeed", "xpReward", "contactDamage", "contactRadius",
                           "contactInterval", "separationRadius", "separationStrength"));

        foreach (EnemyDef def in defs)
        {
            sb.AppendLine(Join(def.Id, N(def.MaxHp), N(def.MoveSpeed), def.XpReward.ToString(),
                               N(def.ContactDamage), N(def.ContactRadius), N(def.ContactInterval),
                               N(def.SeparationRadius), N(def.SeparationStrength)));
        }

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


            def.DamageStep = row.Float("damageStep", def.DamageStep);
            def.FireRateStep = row.Float("fireRateStep", def.FireRateStep);
            def.RangeStep = row.Float("rangeStep", def.RangeStep);

            def.MaxCount = row.Int("maxCount", def.MaxCount);
            def.MaxUpgrades = row.Int("maxUpgrades", def.MaxUpgrades);
            def.SpecialThreshold = row.Int("specialThreshold", def.SpecialThreshold);

            EditorUtility.SetDirty(def);
            changed++;
        }

        return changed;
    }

    private static void ExportTurrets(GameDatabase db)
    {
        List<TurretDef> defs = ValidTurrets(db);

        StringBuilder sb = new StringBuilder();

        sb.AppendLine(Join("id", "displayName", "description", "cardColor",
                           "range", "fireInterval", "damage",
                           "damageStep", "fireRateStep", "rangeStep",
                           "maxCount", "maxUpgrades", "specialThreshold"));

        foreach (TurretDef def in defs)
        {
            sb.AppendLine(Join(def.Id, def.DisplayName, def.Description,
                               "#" + ColorUtility.ToHtmlStringRGB(def.CardColor),
                               N(def.Range), N(def.FireInterval), N(def.Damage),
                               N(def.DamageStep), N(def.FireRateStep), N(def.RangeStep),
                               def.MaxCount.ToString(), def.MaxUpgrades.ToString(),
                               def.SpecialThreshold.ToString()));
        }

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
        List<EnemyDef> enemies = ValidEnemies(db);

        List<WaveTable.Wave> waves = new List<WaveTable.Wave>();
        if (db.Waves != null && db.Waves.Waves != null)
        {
            foreach (WaveTable.Wave wave in db.Waves.Waves)
            {
                if (wave != null) waves.Add(wave);
            }
        }

        StringBuilder sb = new StringBuilder();

        List<string> header = new List<string>
        {
            "wave", "label", "duration", "breakAfter", "spawnInterval", "batchSize", "maxAlive",
            "hpMultiplier", "spawnZones"
        };
        foreach (EnemyDef def in enemies) header.Add("w_" + def.Id);

        sb.AppendLine(Join(header.ToArray()));

        for (int i = 0; i < waves.Count; i++)
        {
            WaveTable.Wave wave = waves[i];

            List<string> cells = new List<string>
            {
                (i + 1).ToString(), wave.Label, N(wave.Duration), N(wave.BreakAfter),
                N(wave.SpawnInterval), wave.BatchSize.ToString(),
                wave.MaxAliveEnemies.ToString(), N(wave.HpMultiplier), FormatZones(wave.SpawnZones)
            };

            foreach (EnemyDef def in enemies) cells.Add(N(FindWeight(wave, def.Id)));

            sb.AppendLine(Join(cells.ToArray()));
        }

        WriteFile(WavesFile, sb.ToString());
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
