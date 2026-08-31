// 역대 최고 기록. PlayerPrefs에 저장하므로 게임을 껐다 켜도 남는다.
// 항목마다 따로 비교해서 더 좋은 쪽만 남긴다. 한 판에서 전부 갱신되지 않아도 된다.

using UnityEngine;

public static class BestRecords
{
    private const string SecondsKey = "best_seconds";
    private const string WaveKey = "best_wave";
    private const string KillsKey = "best_kills";
    private const string DamageKey = "best_damage";

    public static float BestSeconds => PlayerPrefs.GetFloat(SecondsKey, 0f);
    public static int BestWave => PlayerPrefs.GetInt(WaveKey, 0);
    public static int BestKills => PlayerPrefs.GetInt(KillsKey, 0);
    public static float BestDamage => PlayerPrefs.GetFloat(DamageKey, 0f);

    /// <summary>한 판이 끝날 때 부른다. 항목별로 최고값만 갱신된다.</summary>
    public static void Submit(float seconds, int wave, int kills, float damage)
    {
        bool changed = false;

        if (seconds > BestSeconds)
        {
            PlayerPrefs.SetFloat(SecondsKey, seconds);
            changed = true;
        }

        if (wave > BestWave)
        {
            PlayerPrefs.SetInt(WaveKey, wave);
            changed = true;
        }

        if (kills > BestKills)
        {
            PlayerPrefs.SetInt(KillsKey, kills);
            changed = true;
        }

        if (damage > BestDamage)
        {
            PlayerPrefs.SetFloat(DamageKey, damage);
            changed = true;
        }

        if (changed) PlayerPrefs.Save();
    }

    /// <summary>기록 초기화. 설정창에 버튼을 달고 싶으면 이걸 부르면 된다.</summary>
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(SecondsKey);
        PlayerPrefs.DeleteKey(WaveKey);
        PlayerPrefs.DeleteKey(KillsKey);
        PlayerPrefs.DeleteKey(DamageKey);
        PlayerPrefs.Save();
    }

    /// <summary>초를 mm:ss 로. 한 시간을 넘으면 h:mm:ss 가 된다.</summary>
    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));

        int hours = total / 3600;
        int minutes = total % 3600 / 60;
        int secs = total % 60;

        if (hours > 0) return string.Format("{0}:{1:00}:{2:00}", hours, minutes, secs);
        return string.Format("{0:00}:{1:00}", minutes, secs);
    }

    /// <summary>큰 수를 천 단위로 끊어서. 누적 피해는 금방 수십만이 된다.</summary>
    public static string FormatNumber(float value)
    {
        return Mathf.Max(0f, value).ToString("N0");
    }
}
