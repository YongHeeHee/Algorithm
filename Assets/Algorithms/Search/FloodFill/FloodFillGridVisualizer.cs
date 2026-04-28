using System.Collections;
using System.Collections.Generic;
using Algorithms.Common;
using UnityEngine;

namespace Algorithms.Search
{
    /// <summary>
    /// =====================================================================
    ///  Flood Fill 그리드 시각화 데모 (Unity 용)
    /// =====================================================================
    ///
    /// ▶ 사용 방법
    ///   1) 빈 GameObject 를 만들고 이 컴포넌트를 붙인다.
    ///   2) (선택) Cell Prefab 슬롯에 Cube/Quad 같은 메시 프리팹을 넣는다.
    ///        - 비워두면 기본 Cube 가 자동 생성되어 바로 실행된다.
    ///   3) Play 버튼을 누르면 격자에 여러 색의 영역(Voronoi)이 생성되고,
    ///      시작 셀이 속한 영역이 한 스텝씩 fill 색으로 교체된다.
    ///
    /// ▶ 색상 의미
    ///     베이스 색 (영역별)  : 시작 셀이 속한 영역 = '채울 대상', 다른 영역 = '경계 = 채우지 않음'
    ///     노랑                : 큐에 들어감 (Frontier — 곧 채워질 예정)
    ///     분홍                : 채움 완료 (newValue 로 교체된 셀)
    ///     초록                : 시작 셀
    ///
    ///   ※ BFS / DFS 시각화와 색상 의미를 일관되게 유지했다.
    ///     단, Flood Fill 에는 '목표 셀(빨강)' 과 '벽(검정)' 이 없다 — 대신 *다른 색 영역* 이
    ///     자연스럽게 경계 역할을 한다. 같은 색 영역이 아니면 큐에 넣지 않는 것이 핵심.
    ///
    /// ▶ BFS 시각화와의 차이
    ///   - BFS  : 미리 만들어진 Graph&lt;Vector2Int&gt; 를 따라가며 *최단 거리* 를 구한다 (도달성 + 경로).
    ///   - Flood: 격자의 셀 *값(색)* 이 같은지를 즉석에서 검사하며 *연결된 같은-색 영역* 을 채운다.
    ///            그래프가 따로 없다. 4 방향 이웃 검사 + 값 일치 검사가 곧 '간선' 역할.
    ///   - 같은 시드(randomSeed)를 쓰면 BFS 데모와 동일한 격자 위에서, BFS 가 어떻게 영역 채우기
    ///     알고리즘으로 자연스럽게 변형되는지 비교해 볼 수 있다.
    ///
    /// ▶ 시각화 코드와 FloodFillAlgorithm.Fill 의 관계
    ///   이 코루틴 RunFloodFill 은 FloodFillAlgorithm.Fill 과 사실상 동일한 BFS 기반 로직이지만,
    ///   매 스텝마다 셀 색상을 바꾸고 WaitForSeconds 로 잠시 멈춰서 사람이 진행 과정을 볼 수 있게 한다.
    /// </summary>
    public class FloodFillGridVisualizer : MonoBehaviour, IAlgorithmDemo
    {
        // ─────────────────────────────────────────────────────────────
        // 인스펙터 노출 설정
        // ─────────────────────────────────────────────────────────────

        [Header("그리드 크기")]
        [SerializeField] private int width  = 16;
        [SerializeField] private int height = 16;

        [Header("타일 프리팹 / 셀 간격")]
        [Tooltip("비워두면 기본 Cube 가 자동 생성됩니다.")]
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private float cellSize = 1.0f;

        [Header("시작 좌표 (Flood Fill 의 출발점)")]
        [SerializeField] private Vector2Int startCoord = new Vector2Int(8, 8);

        [Header("영역 개수 (Voronoi 시드 수)")]
        [Tooltip("시드 수가 많을수록 영역이 작게 쪼개진다. 2~3 이면 큰 영역, 6 이상이면 작은 패치들.")]
        [Range(2, 8)]
        [SerializeField] private int regionCount = 4;
        [SerializeField] private int randomSeed = 12345;

        [Header("스텝 간 딜레이(초)")]
        [SerializeField] private float stepDelay = 0.04f;

        [Header("색상 — 알고리즘 상태")]
        [SerializeField] private Color colorFrontier = new Color(1.00f, 0.90f, 0.20f);
        [SerializeField] private Color colorFilled   = new Color(1.00f, 0.40f, 0.80f);
        [SerializeField] private Color colorStart    = new Color(0.20f, 0.85f, 0.30f);

        [Header("색상 — 영역 베이스 팔레트")]
        [Tooltip("regionCount 개수만큼만 사용된다. 부족하면 자동으로 순환.")]
        [SerializeField] private Color[] regionPalette =
        {
            new Color(0.85f, 0.85f, 0.85f), // 옅은 회색
            new Color(0.55f, 0.75f, 0.95f), // 옅은 파랑
            new Color(0.95f, 0.80f, 0.55f), // 옅은 주황
            new Color(0.75f, 0.95f, 0.65f), // 옅은 연두
            new Color(0.85f, 0.65f, 0.95f), // 옅은 보라
            new Color(0.95f, 0.65f, 0.65f), // 옅은 분홍-빨강
            new Color(0.65f, 0.95f, 0.95f), // 옅은 청록
            new Color(0.95f, 0.95f, 0.55f), // 옅은 노랑(약함)
        };

        // ─────────────────────────────────────────────────────────────
        // 내부 상태
        // ─────────────────────────────────────────────────────────────

        // 좌표(Vector2Int) → 그 좌표에 놓인 셀 GameObject.
        // Flood Fill 이 진행되며 색상을 바꿀 때 좌표만으로 빠르게 셀을 찾기 위함.
        private readonly Dictionary<Vector2Int, GameObject> _cellObjects = new();

        // 격자 셀의 '영역 ID' (= 어느 Voronoi 시드에 속하는가).
        // FloodFillAlgorithm 의 일반 버전이 받는 'T[,] grid' 와 같은 역할.
        // 여기선 int 영역 인덱스를 셀 값으로 사용한다.
        private int[,] _regionId;

        // 그리드 기반 Flood Fill 의 4 방향 이웃 오프셋 (상하좌우).
        // 8 방향(대각선 포함)으로 확장하려면 (1,1), (1,-1), (-1,1), (-1,-1) 을 추가하면 된다.
        private static readonly Vector2Int[] FourDirections =
        {
            new Vector2Int( 1,  0),
            new Vector2Int(-1,  0),
            new Vector2Int( 0,  1),
            new Vector2Int( 0, -1),
        };

        // ─────────────────────────────────────────────────────────────
        // Unity 라이프사이클
        // ─────────────────────────────────────────────────────────────

        private void Start()
        {
            // 처음 시작 = '재시작' 과 동일한 흐름이므로 Restart() 한 번 호출로 통합.
            Restart();
        }

        /// <summary>
        /// 알고리즘을 처음 상태로 되돌리고 다시 실행한다.
        /// AlgorithmDemoUI 의 Restart 버튼이 이 메서드를 호출한다.
        /// </summary>
        public void Restart()
        {
            // [1] 진행 중인 코루틴이 있으면 중단.
            //     Restart 를 도중에 누른 경우 이전 코루틴이 살아있으면 색칠이 충돌하므로 반드시 멈춰야 한다.
            StopAllCoroutines();

            // [2] 이전에 만들어진 셀 GameObject 들을 모두 제거 + 내부 상태 정리.
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            _cellObjects.Clear();

            // [3] 그리드(Voronoi 영역) 재구성 + 코루틴 재시작.
            BuildGrid();
            StartCoroutine(RunFloodFill());
        }

        // ─────────────────────────────────────────────────────────────
        // 1. 그리드 / 영역(Voronoi) 생성
        //    - 무작위 시드 좌표 N 개를 뽑은 뒤, 각 셀을 가장 가까운 시드의 영역에 할당.
        //    - 이렇게 하면 자연스럽게 *연결된* 색 패치들이 만들어져 Flood Fill 의 동작이 잘 보인다.
        // ─────────────────────────────────────────────────────────────

        private void BuildGrid()
        {
            // 동일한 시드로 동일한 격자가 재현되도록 Random 시드를 고정.
            // BFS / DFS 데모와 같은 시드를 쓰면 미로의 *형태* 는 다르지만,
            // 같은 좌표계에서 알고리즘이 어떻게 다른지 직접 비교해 볼 수 있다.
            Random.InitState(randomSeed);

            _regionId = new int[width, height];

            // [1] Voronoi 시드 좌표 결정. regionCount 개의 무작위 셀.
            //     시드 좌표가 곧 각 영역의 '중심점' 역할을 한다.
            var seedCoords = new Vector2Int[regionCount];
            for (int i = 0; i < regionCount; i++)
            {
                seedCoords[i] = new Vector2Int(
                    Random.Range(0, width),
                    Random.Range(0, height));
            }

            // [2] 각 셀을 '가장 가까운 시드' 의 영역으로 할당 + 셀 GameObject 생성.
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // [2-a] 거리 비교: 제곱 거리로 비교하면 sqrt 를 피할 수 있어 빠르다.
                    int bestIdx  = 0;
                    int bestDist = int.MaxValue;
                    for (int i = 0; i < regionCount; i++)
                    {
                        int dx = x - seedCoords[i].x;
                        int dy = y - seedCoords[i].y;
                        int d  = dx * dx + dy * dy;
                        if (d < bestDist)
                        {
                            bestDist = d;
                            bestIdx  = i;
                        }
                    }
                    _regionId[x, y] = bestIdx;

                    // [2-b] 셀 GameObject 생성. 프리팹이 비어 있으면 기본 Cube → 셋업 없이 바로 실행 가능.
                    GameObject cell;
                    if (cellPrefab != null)
                    {
                        cell = Instantiate(
                            cellPrefab,
                            new Vector3(x * cellSize, 0, y * cellSize),
                            Quaternion.identity,
                            transform);
                    }
                    else
                    {
                        cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cell.transform.SetParent(transform);
                        cell.transform.position = new Vector3(x * cellSize, 0, y * cellSize);
                    }
                    cell.name = $"Cell ({x},{y}) R{bestIdx}";

                    // [2-c] 초기 색 칠하기. 시작 셀은 초록, 그 외는 영역 베이스 색.
                    var coord = new Vector2Int(x, y);
                    Color initialColor = (coord == startCoord)
                        ? colorStart
                        : GetRegionColor(bestIdx);
                    SetCellColor(cell, initialColor);

                    _cellObjects[coord] = cell;
                }
            }
        }

        private Color GetRegionColor(int regionIdx)
        {
            // 팔레트보다 영역 수가 많으면 순환해서 사용. (regionCount 가 8 이하면 그대로 인덱싱.)
            if (regionPalette == null || regionPalette.Length == 0)
            {
                return Color.gray;
            }
            return regionPalette[regionIdx % regionPalette.Length];
        }

        // ─────────────────────────────────────────────────────────────
        // 2. Flood Fill 실행 (시각화 버전)
        // ─────────────────────────────────────────────────────────────
        //
        // 이 코루틴의 본체는 FloodFillAlgorithm.Fill 과 동일한 흐름이다.
        // 다른 점은 두 가지뿐:
        //   - 매 스텝 셀 색상을 바꾼다 (Frontier 노랑 / Filled 분홍).
        //   - WaitForSeconds 로 잠시 멈춰 사람이 진행을 볼 수 있게 한다.
        //
        private IEnumerator RunFloodFill()
        {
            // 시작 좌표 검증.
            if (startCoord.x < 0 || startCoord.x >= width ||
                startCoord.y < 0 || startCoord.y >= height)
            {
                Debug.LogWarning("[FloodFill] 시작 좌표가 격자 밖입니다.");
                yield break;
            }

            // [4] 시작 셀의 영역 ID = targetRegion. 이 ID 와 같은 셀들만 채울 대상이 된다.
            //     일반화된 FloodFillAlgorithm 에서는 'targetValue' 에 해당.
            int targetRegion = _regionId[startCoord.x, startCoord.y];

            // [6] Flood Fill 의 심장: 큐(FIFO).
            //     ※ HashSet&lt;Vector2Int&gt; 를 별도로 두지 않는 이유:
            //       큐에 넣는 순간 _regionId[x,y] 를 음수(-1) 로 바꿔 '방문 표시' 를 겸한다 …
            //       …는 일반 알고리즘 버전의 트릭. 시각화 버전에서는 색을 바꿔 표시하므로,
            //       대신 visited 를 별도로 둬도 OK. 여기서는 _regionId 를 변경하지 않고
            //       명시적인 visited 집합을 써서 코드를 더 직관적으로 보이게 한다.
            //       (학습 목적상 알고리즘 본체 vs 시각화 버전을 비교해 보면 흥미롭다.)
            var visited = new HashSet<Vector2Int>();
            var queue   = new Queue<Vector2Int>();

            // [7] 시작 셀을 큐에 넣고 방문 표시.
            queue.Enqueue(startCoord);
            visited.Add(startCoord);

            int filledCount = 0;

            // [8] 큐가 빌 때까지 반복 = 시작 셀과 같은 영역에 속한 모든 셀이 처리될 때까지.
            while (queue.Count > 0)
            {
                // [8-1] 큐의 가장 앞 셀을 꺼낸다.
                var current = queue.Dequeue();

                // [8-2] '채움 완료' 색으로 칠하기. 단, 시작 셀은 초록을 유지(가독성).
                if (current != startCoord)
                {
                    PaintCell(current, colorFilled);
                }
                filledCount++;

                // [8-3] 4 방향 이웃 검사.
                foreach (var dir in FourDirections)
                {
                    var next = current + dir;

                    // 격자 밖이면 건너뛴다.
                    if (next.x < 0 || next.x >= width ||
                        next.y < 0 || next.y >= height) continue;

                    // 이미 큐에 들어간 적이 있으면 건너뛴다 (중복 방지).
                    if (!visited.Add(next)) continue;

                    // *Flood Fill 의 핵심*: 같은 영역(= 같은 베이스 색) 이 아니면 채우지 않는다.
                    // 이게 BFS 와의 결정적 차이 — '간선' 이 사전에 정의되지 않고,
                    // '값(색) 일치' 라는 조건이 즉석에서 간선 역할을 한다.
                    if (_regionId[next.x, next.y] != targetRegion) continue;

                    // 큐에 새로 들어가는 셀 = Frontier. 노란색으로 표시.
                    PaintCell(next, colorFrontier);
                    queue.Enqueue(next);
                }

                // 한 셀 처리가 끝났으니 잠시 대기 → 사람이 한 스텝씩 볼 수 있다.
                yield return new WaitForSeconds(stepDelay);
            }

            Debug.Log($"[FloodFill] 영역 채우기 완료. 채워진 셀 수: {filledCount} / 전체 {width * height}");
        }

        // ─────────────────────────────────────────────────────────────
        // 헬퍼 (BFS / DFS Visualizer 와 동일한 패턴)
        // ─────────────────────────────────────────────────────────────

        private void PaintCell(Vector2Int coord, Color color)
        {
            if (_cellObjects.TryGetValue(coord, out var cell))
            {
                SetCellColor(cell, color);
            }
        }

        private static void SetCellColor(GameObject cell, Color color)
        {
            // Renderer.material 은 호출 시 인스턴스 머티리얼을 자동 생성하므로
            // 셀마다 독립된 색상을 가질 수 있다.
            // sharedMaterial 을 직접 수정하면 같은 머티리얼을 쓰는 모든 셀이 동시에 바뀌어 버린다.
            if (cell.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = color;
            }
        }
    }
}
