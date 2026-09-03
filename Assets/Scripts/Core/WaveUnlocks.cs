// 판을 넘어 남는 해금 기록. 보스를 한 번이라도 잡았으면 그 다음 웨이브에서 시작할 수 있다.
// PlayerPrefs에 웨이브 번호별로 따로 저장하므로, 보스를 더 추가해도 코드를 안 고쳐도 된다.

using UnityEngine;

public static class WaveUnlocks
{
    private const string KeyPrefix = "boss_cleared_";

    private static string KeyFor(int bossWave) => KeyPrefix + bossWave;

    /// <summary>이 웨이브의 보스를 한 번이라도 잡았는가.</summary>
    public static bool IsBossCleared(int bossWave)
    {
        return PlayerPrefs.GetInt(KeyFor(bossWave), 0) != 0;
    }

    /// <summary>보스를 잡은 순간 부른다. 이미 기록돼 있으면 아무것도 하지 않는다.</summary>
    public static void MarkBossCleared(int bossWave)
    {
        if (bossWave <= 0 || IsBossCleared(bossWave)) return;

        PlayerPrefs.SetInt(KeyFor(bossWave), 1);
        PlayerPrefs.Save();
    }

    /// <summary>해금 초기화. 최고 기록을 지울 때 같이 지운다.</summary>
    public static void Clear(int maxBossWave = 200)
    {
        for (int wave = 1; wave <= maxBossWave; wave++) PlayerPrefs.DeleteKey(KeyFor(wave));

        PlayerPrefs.Save();
    }
}
