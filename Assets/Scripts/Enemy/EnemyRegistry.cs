// 살아있는 적을 들고 있는 등록소이자, 거리 질의를 위한 공간 격자.
//
// 예전에는 질의 하나마다 살아있는 적 전체를 훑었고, 그 안쪽에서 enemy.transform.position 을 불렀다.
// transform.position 은 관리 코드에서 네이티브로 넘어가는 호출이라 루프 안에서 부르면 그 자체가 비용인데,
// 총알마다 / 적마다 도는 자리여서 적 160마리 기준 프레임당 1만 번을 넘겼고 적 수의 제곱으로 늘어났다.
//
// 그래서 두 가지를 바꿨다.
//   1. 위치는 프레임당 한 번만 읽어 배열에 받아둔다. 네이티브 왕복이 (적 수)번으로 줄어든다.
//   2. 그 위치로 격자를 채우고, 질의는 반경이 닿는 칸만 본다. 적이 늘어도 후보 수는 거의 그대로다.
//
// 격자는 첫 질의 때 알아서 다시 만들어지므로 스크립트 실행 순서를 맞출 필요가 없다.
// 같은 프레임 안에서 위치가 한 프레임 낡을 수 있지만, 60fps에서 적은 프레임당 0.05유닛도 못 움직인다.
// 명중 반경(0.55)이나 밀어내기 반경(0.85)에 비하면 무시할 수 있는 오차다.
//
// 격자는 적 참조를 직접 담는다. alive 리스트의 인덱스를 담으면, 프레임 도중 적이 죽어 리스트에서 빠질 때
// 뒤쪽 인덱스가 전부 밀려서 격자가 통째로 어긋난다.

using System;
using System.Collections.Generic;
using UnityEngine;

public static class EnemyRegistry
{
    private static readonly List<EnemyBase> alive = new List<EnemyBase>(256);

    public static IReadOnlyList<EnemyBase> Alive => alive;
    public static int Count => alive.Count;

    // ---------------- 격자 ----------------

    // 칸 하나의 크기(월드 단위). 자주 도는 질의의 반경(밀어내기 0.85, 명중 0.55)보다 크게 잡아
    // 그런 질의가 2x2 칸 안에서 끝나게 한다. 너무 키우면 칸당 후보가 늘어 격자의 의미가 없어진다.
    private const float CellSize = 1.5f;

    // 격자에 담긴 적과 그 위치. 프레임 시작 시점의 스냅샷이며 칸 순서로 정렬돼 있다.
    private static EnemyBase[] items = new EnemyBase[256];
    private static Vector3[] itemPositions = new Vector3[256];

    // 적마다의 몸 반경. 질의 반경에 이걸 더해서 비교하므로, 몸이 큰 적도 겉면에서 맞는다.
    private static float[] itemRadii = new float[256];

    // 정렬 전 임시 버퍼.
    private static EnemyBase[] srcItems = new EnemyBase[256];
    private static Vector3[] srcPositions = new Vector3[256];
    private static float[] srcRadii = new float[256];
    private static int[] srcCells = new int[256];

    // cellStart[c] ~ cellStart[c+1] 이 c번 칸에 들어있는 items 구간이다.
    private static int[] cellStart = new int[1];
    private static int[] cellCursor = new int[1];

    private static int cols;
    private static int rows;
    private static float originX;
    private static float originZ;

    private static int itemCount;
    private static int builtFrame = -1;

    // 이번 프레임에 등록된 적 중 가장 큰 몸 반경. 격자에서 훑을 칸 범위를 이만큼 넓혀야
    // 멀찍이 떨어진 칸에 있는 큰 적을 놓치지 않는다.
    private static float maxBodyRadius;

    public static void Register(EnemyBase enemy)
    {
        if (enemy != null && !alive.Contains(enemy)) alive.Add(enemy);
    }

    public static void Unregister(EnemyBase enemy)
    {
        alive.Remove(enemy);
    }

    /// <summary>static은 씬을 다시 로드해도 살아남으므로 게임 재시작 시 반드시 비워야 한다.</summary>
    public static void Clear()
    {
        alive.Clear();

        // 격자에 옛 씬의 적 참조가 남으면 파괴된 오브젝트를 계속 만지게 된다.
        Array.Clear(items, 0, items.Length);
        itemCount = 0;
        maxBodyRadius = 0f;
        builtFrame = -1;
    }

    // ---------------- 질의 ----------------

    /// <summary>
    /// from 기준 range 안에서 가장 가까운 살아있는 적. 없으면 null.
    /// 높이(y)는 무시하고 XZ 평면 거리로만 잰다. 들어올린 포탑이나 공중을 나는 총알도
    /// 지상의 적을 정상적으로 맞히게 하기 위한 것이며, 탑뷰라 y 차이는 게임적으로 의미가 없다.
    /// </summary>
    public static EnemyBase FindNearest(Vector3 from, float range)
    {
        return FindNearestExcluding(from, range, null);
    }

    /// <summary>
    /// FindNearest와 같지만 exclude에 들어있는 적은 건너뛴다. 연쇄 공격이 같은 적을 다시 때리지 않게 하는 용도.
    /// </summary>
    public static EnemyBase FindNearestExcluding(Vector3 from, float range, List<EnemyBase> exclude)
    {
        EnsureFresh();

        EnemyBase nearest = null;

        // 몸 겉면까지의 거리로 비교한다. 가장 가까운 겉면을 고르므로 큰 적이 부당하게 밀리지 않는다.
        float bestSurfaceSqr = range * range;

        int minCol, maxCol, minRow, maxRow;
        if (!CellRange(from, range + maxBodyRadius, out minCol, out maxCol, out minRow, out maxRow)) return null;

        for (int row = minRow; row <= maxRow; row++)
        {
            int rowBase = row * cols;

            for (int col = minCol; col <= maxCol; col++)
            {
                int cell = rowBase + col;

                for (int k = cellStart[cell]; k < cellStart[cell + 1]; k++)
                {
                    EnemyBase enemy = items[k];
                    if (enemy == null || !enemy.IsAlive) continue;
                    if (exclude != null && exclude.Contains(enemy)) continue;

                    float dx = itemPositions[k].x - from.x;
                    float dz = itemPositions[k].z - from.z;

                    // 중심까지의 거리에서 몸 반경을 뺀 값이 겉면까지의 거리다. 음수면 이미 몸 안이다.
                    float surface = Mathf.Sqrt(dx * dx + dz * dz) - itemRadii[k];
                    float surfaceSqr = surface <= 0f ? 0f : surface * surface;

                    if (surfaceSqr > bestSurfaceSqr) continue;

                    bestSurfaceSqr = surfaceSqr;
                    nearest = enemy;
                }
            }
        }

        return nearest;
    }

    /// <summary>range 안의 살아있는 적을 전부 results에 담는다. 관통탄과 광역 판정용.</summary>
    public static void FindAllInRange(Vector3 from, float range, List<EnemyBase> results)
    {
        results.Clear();
        EnsureFresh();

        int minCol, maxCol, minRow, maxRow;
        if (!CellRange(from, range + maxBodyRadius, out minCol, out maxCol, out minRow, out maxRow)) return;

        for (int row = minRow; row <= maxRow; row++)
        {
            int rowBase = row * cols;

            for (int col = minCol; col <= maxCol; col++)
            {
                int cell = rowBase + col;

                for (int k = cellStart[cell]; k < cellStart[cell + 1]; k++)
                {
                    EnemyBase enemy = items[k];
                    if (enemy == null || !enemy.IsAlive) continue;

                    float dx = itemPositions[k].x - from.x;
                    float dz = itemPositions[k].z - from.z;

                    float reach = range + itemRadii[k];
                    if (dx * dx + dz * dz <= reach * reach) results.Add(enemy);
                }
            }
        }
    }

    // ---------------- 격자 만들기 ----------------

    private static void EnsureFresh()
    {
        if (builtFrame == Time.frameCount) return;
        builtFrame = Time.frameCount;

        Rebuild();
    }

    private static void Rebuild()
    {
        // 킬 경계는 인스펙터에서 바뀔 수 있으므로 매번 확인한다. 크기가 그대로면 배열을 다시 만들지 않는다.
        Vector2 half = ArenaBounds.KillHalfSize;

        int wantCols = Mathf.Max(1, Mathf.CeilToInt(half.x * 2f / CellSize));
        int wantRows = Mathf.Max(1, Mathf.CeilToInt(half.y * 2f / CellSize));

        if (wantCols != cols || wantRows != rows)
        {
            cols = wantCols;
            rows = wantRows;
            cellStart = new int[cols * rows + 1];
            cellCursor = new int[cols * rows + 1];
        }

        originX = -half.x;
        originZ = -half.y;

        // 죽었거나 파괴된 적은 여기서 한 번에 걸러낸다. 질의마다 리스트를 고치던 예전 방식보다 안전하다.
        int n = 0;
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            EnemyBase enemy = alive[i];

            if (enemy == null)
            {
                alive.RemoveAt(i);
                continue;
            }

            n++;
        }

        EnsureCapacity(n);

        // 1) 위치를 한 번만 읽는다. 네이티브 왕복은 여기가 전부다.
        int w = 0;
        float biggest = 0f;
        for (int i = 0; i < alive.Count; i++)
        {
            EnemyBase enemy = alive[i];
            if (enemy == null) continue;

            srcItems[w] = enemy;
            srcPositions[w] = enemy.transform.position;
            srcRadii[w] = Mathf.Max(0f, enemy.BodyRadius);
            srcCells[w] = CellIndex(srcPositions[w]);

            if (srcRadii[w] > biggest) biggest = srcRadii[w];
            w++;
        }

        itemCount = w;
        maxBodyRadius = biggest;

        // 2) 칸마다 몇 개인지 센다.
        Array.Clear(cellStart, 0, cellStart.Length);
        for (int i = 0; i < itemCount; i++) cellStart[srcCells[i] + 1]++;

        // 3) 누적합을 내면 각 칸의 시작 위치가 된다.
        for (int c = 0; c < cols * rows; c++) cellStart[c + 1] += cellStart[c];

        // 4) 칸 순서대로 늘어놓는다. cellStart를 커서로 쓰면 값이 망가지므로 복사본을 쓴다.
        Array.Copy(cellStart, cellCursor, cellStart.Length);

        for (int i = 0; i < itemCount; i++)
        {
            int slot = cellCursor[srcCells[i]]++;
            items[slot] = srcItems[i];
            itemPositions[slot] = srcPositions[i];
            itemRadii[slot] = srcRadii[i];
        }

        // 남은 칸에 옛 프레임의 참조가 남아있으면 파괴된 오브젝트를 붙들게 된다.
        if (itemCount < items.Length) Array.Clear(items, itemCount, items.Length - itemCount);
    }

    private static void EnsureCapacity(int n)
    {
        if (items.Length >= n) return;

        int size = Mathf.NextPowerOfTwo(Mathf.Max(256, n));

        items = new EnemyBase[size];
        itemPositions = new Vector3[size];
        itemRadii = new float[size];
        srcItems = new EnemyBase[size];
        srcPositions = new Vector3[size];
        srcRadii = new float[size];
        srcCells = new int[size];
    }

    private static int CellIndex(Vector3 position)
    {
        int col = Mathf.Clamp((int)((position.x - originX) / CellSize), 0, cols - 1);
        int row = Mathf.Clamp((int)((position.z - originZ) / CellSize), 0, rows - 1);

        return row * cols + col;
    }

    /// <summary>반경이 닿는 칸 범위. 격자가 비어 있으면 false.</summary>
    private static bool CellRange(Vector3 from, float range,
                                  out int minCol, out int maxCol, out int minRow, out int maxRow)
    {
        minCol = maxCol = minRow = maxRow = 0;
        if (itemCount == 0) return false;

        minCol = Mathf.Clamp(Mathf.FloorToInt((from.x - range - originX) / CellSize), 0, cols - 1);
        maxCol = Mathf.Clamp(Mathf.FloorToInt((from.x + range - originX) / CellSize), 0, cols - 1);
        minRow = Mathf.Clamp(Mathf.FloorToInt((from.z - range - originZ) / CellSize), 0, rows - 1);
        maxRow = Mathf.Clamp(Mathf.FloorToInt((from.z + range - originZ) / CellSize), 0, rows - 1);

        return true;
    }
}
