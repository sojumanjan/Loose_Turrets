// 메인 메뉴가 떠 있는 동안 인게임 표시를 감춘다.
// 배치해 둔 포탑과 경계선만 남겨서 메뉴 뒤가 살아있는 배경처럼 보이게 하는 것이 목적이다.
// 무엇을 숨길지는 씬에서 직접 고른다. 코드를 고치지 않고 목록만 바꾸면 된다.

using UnityEngine;

public class MenuSceneView : MonoBehaviour
{
    [Tooltip("메인 메뉴 동안 숨길 오브젝트. 게임이 시작되면 다시 켜진다. " +
             "플레이어처럼 스크립트가 살아 있어야 하는 것은 오브젝트 자체 대신 모델 자식만 넣는다.")]
    [SerializeField] private GameObject[] hideDuringMenu;

    // 아직 한 번도 적용하지 않은 상태를 구분하려고 bool 대신 null 가능한 값을 쓴다.
    private bool applied;
    private bool hiddenNow;

    private void Start()
    {
        // 다른 UI들의 Awake가 끝난 뒤에 맞춘다.
        Apply(IsMenu());
    }

    private void Update()
    {
        bool menu = IsMenu();
        if (!applied || menu != hiddenNow) Apply(menu);
    }

    private static bool IsMenu()
    {
        GameManager game = GameManager.Instance;
        return game == null || game.State == GameManager.GameState.Menu;
    }

    private void Apply(bool menu)
    {
        applied = true;
        hiddenNow = menu;

        if (hideDuringMenu == null) return;

        for (int i = 0; i < hideDuringMenu.Length; i++)
        {
            if (hideDuringMenu[i] != null) hideDuringMenu[i].SetActive(!menu);
        }
    }
}
