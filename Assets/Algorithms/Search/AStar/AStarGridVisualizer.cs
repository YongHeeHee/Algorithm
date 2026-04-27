using System.Collections;
using System.Collections.Generic;
using Algorithms.Common;
using UnityEngine;

namespace Algorithms.Search
{
    /// <summary>
    /// =====================================================================
    ///  A* 그리드 시각화 데모 (Unity 용)
    /// =====================================================================
    ///
    /// ▶ 사용 방법
    ///   1) 빈 GameObject 를 만들고 이 컴포넌트를 붙인다.
    ///   2) (선택) Cell Prefab 슬롯에 Cube 프리팹.
    ///   3) Play → 무작위 가중치 그리드가 만들어지고 A* 가 실행됨.
    ///
    /// ▶ Dijkstra 시각화와의 가장 큰 시각적 차이
    ///   Dijkstra 는 시작점에서 *동심원처럼* 노란 Frontier 가 퍼져 나가다 결국 목표에 도달.
    ///   A* 는 휴리스틱 덕분에 *목표 방향으로 길쭉하게* Frontier 가 뻗어 나간다.
    ///   탐색되는 셀 수도 훨씬 적다 — 같은 미로에서 분홍 경로는 동일하지만, 파란/노란 셀 수가
    ///   Dijkstra 보다 눈에 띄게 적다.
    ///   → "이론적 worst case 는 같지만 실측은 훨씬 빠르다" 는 A* 의 가치를 직접 확인 가능.
    ///
    /// ▶ 휴리스틱
    ///   이 데모는 4 방향 그리드이므로 **Manhattan distance** 를 사용한다 (admissible & consistent).
    ///   `|dx| + |dy|` 로 매우 단순. 셀의 weight 가 1 이상이라 가정하면 항상 underestimate.
    ///   weight 가 1 보다 작아질 수 있는 환경에서는 휴리스틱을 가중치 최소값에 맞게 스케일해야 한다.
    ///
    /// ▶ 색상 의미 (BFS / DFS / Dijkstra 와 동일)
    ///     회색  : 미방문 칸
    ///     노랑  : 우선순위 큐(PQ)에 들어감 또는 g 가 갱신됨 (Frontier)
    ///     파랑  : settled — 최단 거리 확정
    ///     초록  : 시작 정점
    ///     빨강  : 목표 정점
    ///     분홍  : 최단 경로 위에 있는 칸 (가중치 합 기준)
    ///     검정  : 벽 (이동 불가)
    ///
    /// ▶ 셀 높이 = 가중치
    ///   Dijkstra 데모와 동일. 가중치만큼 셀이 위로 솟아 "비싼 칸" 이 시각적으로 드러난다.
    ///   같은 randomSeed 로 Dijkstra 와 A* 데모를 돌리면 같은 미로 + 같은 가중치 + 같은 분홍 경로 가
    ///   나오면서, 노란/파란 셀 수만 다르다 — 이게 휴리스틱의 효과.
    /// </summary>
    public class AStarGridVisualizer : MonoBehaviour, IAlgorithmDemo
    {
        // ─────────────────────────────────────────────────────────────
        // 인스펙터 노출 설정 (Dijkstra 와 동일)
        // ─────────────────────────────────────────────────────────────

        [Header("그리드 크기")]
        [SerializeField] private int width  = 12;
        [SerializeField] private int height = 12;

        [Header("타일 프리팹 / 셀 간격")]
        [Tooltip("비워두면 기본 Cube 가 자동 생성됩니다.")]
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private float cellSize = 1.0f;

        [Header("시작 / 목표 좌표")]
        [SerializeField] private Vector2Int startCoord = new Vector2Int(0, 0);
        [SerializeField] private Vector2Int goalCoord  = new Vector2Int(11, 11);

        [Header("벽 비율 (0 ~ 0.5)")]
        [Range(0f, 0.5f)]
        [SerializeField] private float wallRatio = 0.2f;
        [SerializeField] private int randomSeed = 12345;

        [Header("가중치 (셀 이동 비용)")]
        [SerializeField] private int minWeight = 1;
        [SerializeField] private int maxWeight = 5;

        [Header("스텝 간 딜레이(초)")]
        [SerializeField] private float stepDelay = 0.05f;

        [Header("색상")]
        [SerializeField] private Color colorUnvisited = new Color(0.85f, 0.85f, 0.85f);
        [SerializeField] private Color colorFrontier  = new Color(1.00f, 0.90f, 0.20f);
        [SerializeField] private Color colorVisited   = new Color(0.30f, 0.55f, 1.00f);
        [SerializeField] private Color colorStart     = new Color(0.20f, 0.85f, 0.30f);
        [SerializeField] private Color colorGoal      = new Color(1.00f, 0.20f, 0.20f);
        [SerializeField] private Color colorPath      = new Color(1.00f, 0.40f, 0.80f);
        [SerializeField] private Color colorWall      = new Color(0.10f, 0.10f, 0.10f);

        // ─────────────────────────────────────────────────────────────
        // 내부 상태
        // ─────────────────────────────────────────────────────────────

        private readonly Dictionary<Vector2Int, GameObject> _cellObjects = new();
        private readonly Dictionary<Vector2Int, int> _weights = new();
        private readonly HashSet<Vector2Int> _walls = new();
        private WeightedGraph<Vector2Int> _graph;

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
            Restart();
        }

        public void Restart()
        {
            StopAllCoroutines();

            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            _cellObjects.Clear();
            _weights.Clear();
            _walls.Clear();

            BuildGrid();
            BuildGraph();
            StartCoroutine(RunAStar());
        }

        // ─────────────────────────────────────────────────────────────
        // 1. 그리드 / 가중치 / 벽 생성 (Dijkstra 와 동일)
        // ─────────────────────────────────────────────────────────────

        private void BuildGrid()
        {
            // 같은 시드면 BFS / DFS / Dijkstra / A* 데모가 모두 같은 미로 + 같은 가중치 패턴.
            Random.InitState(randomSeed);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var coord = new Vector2Int(x, y);

                    bool isWall =
                        coord != startCoord &&
                        coord != goalCoord  &&
                        Random.value < wallRatio;

                    int weight = isWall ? 1 : Mathf.Max(1, Random.Range(minWeight, maxWeight + 1));

                    GameObject cell;
                    if (cellPrefab != null)
                    {
                        cell = Instantiate(cellPrefab, transform);
                    }
                    else
                    {
                        cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cell.transform.SetParent(transform);
                    }
                    cell.name = $"Cell ({x},{y}) w={weight}";

                    if (isWall)
                    {
                        cell.transform.localScale = new Vector3(1, 0.2f, 1);
                        cell.transform.position   = new Vector3(x * cellSize, 0.1f, y * cellSize);
                    }
                    else
                    {
                        cell.transform.localScale = new Vector3(1, weight, 1);
                        cell.transform.position   = new Vector3(x * cellSize, weight * 0.5f, y * cellSize);
                    }

                    Color initialColor =
                        isWall              ? colorWall      :
                        coord == startCoord ? colorStart     :
                        coord == goalCoord  ? colorGoal      :
                                              colorUnvisited;
                    SetCellColor(cell, initialColor);

                    _cellObjects[coord] = cell;
                    if (isWall) _walls.Add(coord);
                    else        _weights[coord] = weight;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 2. 가중치 그래프 구성 (Dijkstra 와 동일)
        // ─────────────────────────────────────────────────────────────

        private void BuildGraph()
        {
            _graph = new WeightedGraph<Vector2Int>();

            foreach (var kv in _weights)
            {
                _graph.AddNode(kv.Key);
            }

            // 간선 가중치는 *목적지 셀의 weight* — Dijkstra 데모와 동일 모델.
            foreach (var node in _graph.Nodes)
            {
                foreach (var dir in FourDirections)
                {
                    var next = node + dir;
                    if (!_graph.Contains(next)) continue;
                    _graph.AddEdge(node, next, _weights[next], bidirectional: false);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 3. A* 실행 (시각화 버전)
        // ─────────────────────────────────────────────────────────────
        //
        // DijkstraGridVisualizer.RunDijkstra 와 비교해 보면 코드 차이가 *단 한 줄* 이다:
        //   Dijkstra : pq.Enqueue(neighbor, newDist);
        //   A*       : pq.Enqueue(neighbor, newDist + ManhattanDistance(neighbor, goalCoord));
        //
        // 이 한 줄이 시각적으로는 "동심원 → 화살표 모양" 으로 탐색 패턴을 바꾼다.
        //
        private IEnumerator RunAStar()
        {
            if (!_graph.Contains(startCoord))
            {
                Debug.LogWarning("[A*] 시작 좌표가 그래프에 없습니다(벽이거나 그리드 밖).");
                yield break;
            }
            if (!_graph.Contains(goalCoord))
            {
                Debug.LogWarning("[A*] 목표 좌표가 그래프에 없습니다(벽이거나 그리드 밖).");
                yield break;
            }

            var gScore  = new Dictionary<Vector2Int, float>();
            var settled = new HashSet<Vector2Int>();
            var parent  = new Dictionary<Vector2Int, Vector2Int>();
            var pq      = new MinPriorityQueue<Vector2Int>();

            gScore[startCoord] = 0f;
            // priority = f(start) = g(start) + h(start) = 0 + h(start, goal)
            pq.Enqueue(startCoord, ManhattanDistance(startCoord, goalCoord));

            bool goalFound = false;

            while (pq.Count > 0)
            {
                var current = pq.Dequeue();

                if (!settled.Add(current)) continue;

                if (current != startCoord && current != goalCoord)
                {
                    PaintCell(current, colorVisited);
                }

                if (current == goalCoord)
                {
                    goalFound = true;
                    break;
                }

                foreach (var (neighbor, weight) in _graph.GetNeighbors(current))
                {
                    if (settled.Contains(neighbor)) continue;

                    float tentativeG = gScore[current] + weight;

                    if (!gScore.TryGetValue(neighbor, out float oldG) || tentativeG < oldG)
                    {
                        gScore[neighbor] = tentativeG;
                        parent[neighbor] = current;

                        // ★ Dijkstra 와 유일한 코드 차이 ★
                        // priority = f = g + h.   Dijkstra 는 priority = g.
                        float f = tentativeG + ManhattanDistance(neighbor, goalCoord);
                        pq.Enqueue(neighbor, f);

                        if (neighbor != goalCoord)
                        {
                            PaintCell(neighbor, colorFrontier);
                        }
                    }
                }

                yield return new WaitForSeconds(stepDelay);
            }

            // ─────────────────────────────────────────────
            // 4. 최단 경로 복원 + 색칠
            // ─────────────────────────────────────────────
            if (goalFound)
            {
                var node = goalCoord;
                while (node != startCoord)
                {
                    if (node != goalCoord)
                    {
                        PaintCell(node, colorPath);
                    }
                    node = parent[node];
                    yield return new WaitForSeconds(stepDelay);
                }

                Debug.Log($"[A*] 목표 도달! 분홍 = 최단 경로 (가중치 합 = {gScore[goalCoord]}). " +
                          $"Dijkstra 와 같은 미로에서 돌려보고 *settled 셀 수의 차이* 를 비교해 보자.");
            }
            else
            {
                Debug.Log("[A*] 목표에 도달할 수 없습니다 (서로 다른 컴포넌트).");
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 휴리스틱 — Manhattan distance
        // ─────────────────────────────────────────────────────────────
        //
        // 4 방향 그리드에서 admissible (실제 비용을 절대 과대평가하지 않음) & consistent.
        // 셀 weight 가 1 이상이라는 가정 하에 |dx|+|dy| ≤ 실제 비용 이 항상 성립.
        //
        // ※ 만약 셀 weight 가 1 보다 작아질 수 있다면 (예: 일부 셀 weight=0.5),
        //   admissibility 가 깨져 최단 보장이 무너진다 — 휴리스틱을 minWeight 로 스케일해야 한다.
        //
        private static float ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        // ─────────────────────────────────────────────────────────────
        // 헬퍼
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
            if (cell.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = color;
            }
        }
    }
}
