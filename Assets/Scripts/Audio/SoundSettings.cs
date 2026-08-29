// 볼륨 설정 한 곳. 설정 UI가 값을 바꾸고, SfxManager와 BgmPlayer가 읽어 쓴다.
// 씬을 다시 불러와도 유지돼야 해서 static 이고, 다음 실행에도 남도록 PlayerPrefs 에 적어둔다.

using System;
using UnityEngine;

public static class SoundSettings
{
    private const string MasterKey = "sound.master";
    private const string BgmKey = "sound.bgm";
    private const string SfxKey = "sound.sfx";

    private static bool loaded;

    private static float master = 1f;
    private static float bgm = 1f;
    private static float sfx = 1f;

    /// <summary>볼륨이 바뀔 때마다 불린다. 이미 울리고 있는 브금이 즉시 따라오려면 이걸 구독한다.</summary>
    public static event Action Changed;

    /// <summary>전체 볼륨. 브금과 효과음 모두에 곱해진다.</summary>
    public static float Master { get { Load(); return master; } }

    /// <summary>브금 전용 배율. 최종 볼륨은 Master * Bgm.</summary>
    public static float Bgm { get { Load(); return bgm; } }

    /// <summary>효과음 전용 배율. 최종 볼륨은 Master * Sfx.</summary>
    public static float Sfx { get { Load(); return sfx; } }

    /// <summary>브금에 실제로 곱할 값.</summary>
    public static float BgmVolume => Master * Bgm;

    /// <summary>효과음에 실제로 곱할 값.</summary>
    public static float SfxVolume => Master * Sfx;

    public static void SetMaster(float value) { Load(); master = Clamp(value); Save(MasterKey, master); }
    public static void SetBgm(float value) { Load(); bgm = Clamp(value); Save(BgmKey, bgm); }
    public static void SetSfx(float value) { Load(); sfx = Clamp(value); Save(SfxKey, sfx); }

    private static float Clamp(float value) => Mathf.Clamp01(value);

    private static void Save(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();

        if (Changed != null) Changed();
    }

    /// <summary>처음 읽을 때 한 번만 PlayerPrefs에서 꺼내온다.</summary>
    private static void Load()
    {
        if (loaded) return;
        loaded = true;

        master = PlayerPrefs.GetFloat(MasterKey, 1f);
        bgm = PlayerPrefs.GetFloat(BgmKey, 1f);
        sfx = PlayerPrefs.GetFloat(SfxKey, 1f);
    }
}
