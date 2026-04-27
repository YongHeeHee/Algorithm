using System.Collections;
using System.Collections.Generic;
using Algorithms.Common;
using UnityEngine;

namespace Algorithms.Search
{
    /// <summary>
    /// =====================================================================
    ///  Dijkstra 그리드 시각화 데모 (Unity 용)
    /// =====================================================================
    ///
    /// ▶ 사용 방법
    ///   1) 빈 GameObject 를 만들고 이 컴포넌트를 붙인다.
    ///   2) (선택) Cell Prefab 슬롯에 Cube/Quad 같은 메시 프리팹.
    ///        - 비워두면 기본 Cube 가 자동 생성된다.
    ///   3) Play → 무작위 가중치 그리드가 만들어지고, 시작점에서 Dijkstra 가 한 스텝씩 진행된다.
    ///
    /// ▶ 가중치의 시각화 — 셀 *높이*
    ///   비-벽 셀은 무작위 가중치(minWeight ~ maxWeight) 를 가진다.
    ///   가중치를 셀의 Y 스케일로 매핑해 *높을수록 비싸다* 는 직관을 그대로 보여 준다.
    ///   - weight 1 (싸다)  : 높이 1 (일반 큐브)
    ///   - weight 5 (비싸다): 높이 5 (긴 기둥 모양)
    ///   벽은 짧고 검은 큐브 (높이 0.2) — "지나갈 수 없는 trench" 같은 느낌.
    ///
    /// ▶ 색상 의미 (BFS / DFS 와 동일하게 유지하여 비교를 쉽게 함)
    ///     회색  : 미방문 칸
    ///     노랑  : 우선순위 큐(PQ)에 들어감 또는 거리가 갱신됨 (Frontier)
    ///     파랑  : settled — 최단 거리 *확정*
    ///     초록  : 시작 정점
    ///     빨강  : 목표 정점
    ///     분홍  : 최단 경로 위에 있는 칸 (가중치 합 기준)
    ///     검정  : 벽 (이동 불가)
    ///
    /// ▶ BFS / DFS 시각화와의 가장 큰 차이
    ///   BFS / DFS 는 "어떤 칸을 *몇 번째*로 방문했는가" 만 본다 (가중치 무시).
    ///   Dijkstra 는 "어떤 칸까지의 *누적 비용* 이 얼마인가" 를 본다.
    ///   같은 시드라도 가중치가 다양하면 BFS 의 분홍 경로와 Dijkstra 의 분홍 경로가 달라진다.
    ///   → 비싼 칸을 우회하는 모습이 시각적으로 드러나는 게 핵심.
    /// </summary>
    public class DijkstraGridVisualizer : MonoBehaviour, IAlgorithmDemo
    {
        // ─────────────────────────────────────────────────────────────
        // 인스펙터 노출 설정
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
        [Tooltip("비-벽 셀의 최소 가중치 (1 이상 권장)")]
        [SerializeField] private int minWeight = 1;

        [Tooltip("비-벽 셀의 최대 가중치. minWeight 이상.")]
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

        // 좌표 → 그 좌표에 놓인 셀 GameObject.
        private readonly Dictionary<Vector2Int, GameObject> _cellObjects = new();

        // 좌표 → 그 셀의 가중치 (벽이 아닌 경우만 등록).
        // BuildGraph 가 간선 가중치를 결정할 때 참고한다.
        private readonly Dictionary<Vector2Int, int> _weights = new();

        // 벽 좌표 집합. 그래프 정점에서 제외 → 자연스럽게 '이동 불가'.
        private readonly HashSet<Vector2Int> _walls = new();

        // Dijkstra 가 동작할 가중치 그래프.
        private WeightedGraph<Vector2Int> _graph;

        // 그리드 4 방향 이웃 오프셋.
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
            // [1] 진행 중인 코루틴 중단.
            StopAllCoroutines();

            // [2] 이전 셀 GameObject + 내부 상태 정리.
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            _cellObjects.Clear();
            _weights.Clear();
            _walls.Clear();

            // [3] 그리드/그래프 재구성 + Dijkstra 코루틴 재시작.
            BuildGrid();
            BuildGraph();
            StartCoroutine(RunDijkstra());
        }

        // ─────────────────────────────────────────────────────────────
        // 1. 그리드 / 가중치 / 벽 생성
        // ─────────────────────────────────────────────────────────────

        private void BuildGrid()
        {
            // 같은 시드로 같은 미로 + 같은 가중치 패턴이 재현되도록 시드 고정.
            // BFS / DFS 데모와 같은 시드를 쓰면 동일한 미로에서 세 알고리즘을 비교할 수 있다.
            Random.InitState(randomSeed);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var coord = new Vector2Int(x, y);

                    // 시작 / 목표 칸은 절대 벽이 되지 않도록 보호.
                    bool isWall =
                        coord != startCoord &&
                        coord != goalCoord  &&
                        Random.value < wallRatio;

                    // 비-벽 셀은 [minWeight, maxWeight] 범위 무작위 가중치.
                    // 시작/목표도 가중치를 받지만 색은 초록/빨강을 유지한다.
                    int weight = isWall ? 1 : Mathf.Max(1, Random.Range(minWeight, maxWeight + 1));

                    // 셀 GameObject 생성.
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

                    // 가중치 = Y 스케일 → 셀의 *높이* 가 곧 비용.
                    // 베이스(밑면)를 y=0 에 맞추기 위해 position.y = scale.y / 2.
                    if (isWall)
                    {
                        // 벽은 짧고 검은 trench 처럼 표현 → 시각적으로 walkable 셀과 구분.
                        cell.transform.localScale = new Vector3(1, 0.2f, 1);
                        cell.transform.position   = new Vector3(x * cellSize, 0.1f, y * cellSize);
                    }
                    else
                    {
                        cell.transform.localScale = new Vector3(1, weight, 1);
                        cell.transform.position   = new Vector3(x * cellSize, weight * 0.5f, y * cellSize);
                    }

                    // 초기 색. 우선순위: 벽 > 시작 > 목표 > 미방문.
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
        // 2. 가중치 그래프 구성
        // ─────────────────────────────────────────────────────────────

        private void BuildGraph()
        {
            _graph = new WeightedGraph<Vector2Int>();

            // 비-벽 셀들만 정점으로 등록.
            foreach (var kv in _weights)
            {
                _graph.AddNode(kv.Key);
            }

            // 4 방향 이웃에 대해 간선을 추가.
            //
            // 간선 가중치 모델 :
            //   "어떤 셀에 *들어가는* 비용 = 그 셀의 weight"
            //   → A → B 간선의 가중치는 B 의 weight.
            //
            // 결과적으로 양방향 간선이지만 *방향에 따라 비용이 다르다*:
            //   A → B = B의 weight,   B → A = A의 weight.
            // 단방향 추가를 양쪽 정점에서 한 번씩 돌리면 자동으로 위 결과가 나온다.
            foreach (var node in _graph.Nodes)
            {
                foreach (var dir in FourDirections)
                {
                    var next = node + dir;

                    // 이웃이 그래프에 없다 = 그리드 밖 또는 벽.
                    if (!_graph.Contains(next)) continue;

                    // node → next, 비용 = next 셀의 weight.
                    _graph.AddEdge(node, next, _weights[next], bidirectional: false);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 3. Dijkstra 실행 (시각화 버전)
        // ─────────────────────────────────────────────────────────────
        //
        // 이 코루틴의 Dijkstra 본체는 DijkstraAlgorithm.Search / FindPath 와 동일 흐름.
        // 다른 점은 두 가지:
        //   - 매 스텝 셀 색상을 바꾼다 (PaintCell).
        //   - WaitForSeconds 로 잠시 멈춰 사람이 진행을 볼 수 있게 한다.
        //
        private IEnumerator RunDijkstra()
        {
            if (!_graph.Contains(startCoord))
            {
                Debug.LogWarning("[Dijkstra] 시작 좌표가 그래프에 없습니다(벽이거나 그리드 밖).");
                yield break;
            }

            var distances = new Dictionary<Vector2Int, float>();
            var settled   = new HashSet<Vector2Int>();
            var parent    = new Dictionary<Vector2Int, Vector2Int>();
            var pq        = new MinPriorityQueue<Vector2Int>();

            // [5] 시작점 초기화.
            distances[startCoord] = 0f;
            pq.Enqueue(startCoord, 0f);

            bool goalFound = false;

            // [6] 큐가 빌 때까지 반복.
            while (pq.Count > 0)
            {
                // [6-1] 현재 알려진 거리가 가장 짧은 정점을 꺼낸다.
                var current = pq.Dequeue();

                // [6-2] lazy deletion.
                if (!settled.Add(current)) continue;

                // [6-3] settled 처리 → 파란색.
                if (current != startCoord && current != goalCoord)
                {
                    PaintCell(current, colorVisited);
                }

                // 목표 도달 — settled 시점이 곧 최단 거리.
                if (current == goalCoord)
                {
                    goalFound = true;
                    break;
                }

                // [6-4] 이웃 'relax'.
                foreach (var (neighbor, weight) in _graph.GetNeighbors(current))
                {
                    if (settled.Contains(neighbor)) continue;

                    float newDist = distances[current] + weight;

                    if (!distances.TryGetValue(neighbor, out float oldDist) || newDist < oldDist)
                    {
                        distances[neighbor] = newDist;
                        parent[neighbor] = current;
                        pq.Enqueue(neighbor, newDist);

                        // PQ 에 새로 들어가거나 거리 갱신된 셀 = Frontier (노란색).
                        // 단, 목표 셀은 빨간색 유지.
                        if (neighbor != goalCoord)
                        {
                            PaintCell(neighbor, colorFrontier);
                        }
                    }
                }

                // 한 정점 처리 끝 — 잠시 대기.
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

                Debug.Log($"[Dijkstra] 목표 도달! 분홍 = 최단 경로 (가중치 합 = {distances[goalCoord]}).");
            }
            else
            {
                Debug.Log("[Dijkstra] 목표에 도달할 수 없습니다 (서로 다른 컴포넌트).");
            }
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
            // Renderer.material 은 호출 시 인스턴스 머티리얼을 자동 생성.
            // sharedMaterial 을 직접 수정하면 모든 셀이 함께 바뀌어 버린다.
            if (cell.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = color;
            }
        }
    }
}
