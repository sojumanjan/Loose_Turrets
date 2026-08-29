// 레벨업 선택지의 종류와, 카드 한 장에 표시할 내용.

using UnityEngine;

public enum UpgradeType
{
    // 모든 포탑에 적용 (약하지만 넓게)
    AllDamage,
    AllFireRate,
    AllRange,

    // 특정 포탑 종류에만 적용 (강하지만 좁게)
    TypeDamage,
    TypeFireRate,
    TypeRange,

    // 특정 포탑 종류의 고유 특수 강화. 해당 포탑 강화를 일정 횟수 쌓아야 등장한다.
    TypeSpecial,

    // 포탑 추가
    NewTurret,

    // 플레이어
    PlayerSpeed
}

public struct UpgradeOption
{
    public UpgradeType Type;
    public string Title;
    public string Description;

    /// <summary>포탑 관련 선택지일 때 GameManager의 turretChoices 배열에서 몇 번째인지. 그 외에는 -1.</summary>
    public int TurretIndex;

    /// <summary>카드를 칠할 색. 포탑 관련이면 그 포탑 색, 아니면 중립 회색.</summary>
    public Color Accent;

    /// <summary>카드 좌측 상단 아이콘. 포탑 관련 카드만 채우고, 그 외에는 null이라 아이콘을 숨긴다.</summary>
    public Sprite Icon;

    /// <summary>특수 강화까지 채운 칸 수. 0 미만이면 별을 표시하지 않는다.</summary>
    public int StarsFilled;

    /// <summary>특수 강화에 필요한 총 칸 수.</summary>
    public int StarsTotal;

    /// <summary>이 포탑이 지금까지 쓴 일반 강화 횟수. 0 미만이면 표시하지 않는다.</summary>
    public int UpgradesUsed;

    /// <summary>이 포탑이 쓸 수 있는 일반 강화 최대 횟수. 특수 강화는 여기 포함되지 않는다.</summary>
    public int UpgradesMax;

    public UpgradeOption(UpgradeType type, string title, string description, Color accent,
                         int turretIndex = -1, int starsFilled = -1, int starsTotal = 0,
                         int upgradesUsed = -1, int upgradesMax = 0, Sprite icon = null)
    {
        Type = type;
        Title = title;
        Description = description;
        Accent = accent;
        Icon = icon;
        TurretIndex = turretIndex;
        StarsFilled = starsFilled;
        StarsTotal = starsTotal;
        UpgradesUsed = upgradesUsed;
        UpgradesMax = upgradesMax;
    }
}
