using System.Collections;
using System.Collections.Generic;
using Algorithms.Common;
using UnityEngine;

namespace Algorithms.Search
{
    /// <summary>
    /// =====================================================================
    ///  DFS 그리드 시각화 데모 (Unity 용)
    /// =====================================================================
    ///
    /// ▶ 사용 방법
    ///   1) 빈 GameObject 를 만들고 이 컴포넌트를 붙인다.
    ///   2) (선택) Cell Prefab 슬롯에 Cube/Quad 같은 메시 프리팹을 넣는다.
    ///        - 비워두면 기본 Cube 가 자동 생성되어 바로 실행된다.
    ///   3) Play 버튼을 누르면 그리드가 만들어지고, 시작점에서 DFS 가 한 스텝씩 진행된다.
    ///
    /// ▶ 색상 의미 (BFS 시각화와 동일하게 유지하여 비교를 쉽게 함)
    ///     회색  : 미방문 칸
    ///     노랑  : 스택에 들어감 (곧 방문 예정, Frontier)
    ///     파랑  : 방문 완료
    ///     초록  : 시작 정점
    ///     빨강  : 목표 정점
    ///     분홍  : 발견된 경로 위에 있는 칸 (※ DFS 는 최단 경로 보장 X)
    ///     검정  : 벽 (이동 불가)
    ///
    /// ▶ BFS 시각화와의 차이
    ///   - 자료구조만 Queue → Stack 으로 바뀌었다 (BFSGridVisualizer 코드와 비교해 보면 한눈에 보임).
    ///   - 시각적으로는 BFS 가 동심원처럼 퍼져 나가는 데 비해,
    ///     DFS 는 *뱀처럼* 한 방향으로 깊게 들어갔다가 막히면 되돌아온다.
    ///   - 같은 미로에서 두 알고리즘을 비교해 보면 DFS 가 찾은 경로가 BFS 가 찾은 경로보다
    ///     훨씬 길게 휘어진 모양인 경우가 많다 — 이게 "DFS 는 최단을 보장하지 않는다" 는 사실의
    ///     시각적 증거다.
    /// </summary>
    public class DFSGridVisualizer : MonoBehaviour, IAlgorithmDemo
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
        [SerializeField] private float wallRatio = 0.25f;
        [SerializeField] private int randomSeed = 12345;

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
        private readonly HashSet<Vector2Int> _walls = new();
        private Graph<Vector2Int> _graph;

        // 그리드 기반 탐색의 4 방향 이웃 오프셋 (상하좌우).
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
            // [1] 진행 중인 DFS 코루틴이 있으면 중단.
            StopAllCoroutines();

            // [2] 이전 셀 GameObject + 내부 상태 정리.
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            _cellObjects.Clear();
            _walls.Clear();

            // [3] 그리드/그래프 재구성 + DFS 코루틴 재시작.
            BuildGrid();
            BuildGraph();
            StartCoroutine(RunDFS());
        }

        // ─────────────────────────────────────────────────────────────
        // 1. 그리드 / 벽 생성 (BFSGridVisualizer 와 동일한 로직)
        //    같은 시드를 쓰면 BFS 데모와 정확히 같은 미로가 만들어지므로
        //    두 알고리즘의 탐색 패턴을 직접 비교할 수 있다.
        // ─────────────────────────────────────────────────────────────

        private void BuildGrid()
        {
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
                    cell.name = $"Cell ({x},{y})";

                    Color initialColor =
                        isWall              ? colorWall      :
                        coord == startCoord ? colorStart     :
                        coord == goalCoord  ? colorGoal      :
                                              colorUnvisited;
                    SetCellColor(cell, initialColor);

                    _cellObjects[coord] = cell;
                    if (isWall) _walls.Add(coord);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 2. 그래프 구성 (BFSGridVisualizer 와 동일)
        // ─────────────────────────────────────────────────────────────

        private void BuildGraph()
        {
            _graph = new Graph<Vector2Int>();

            // 벽이 아닌 셀들만 정점으로 등록한다.
            foreach (var kv in _cellObjects)
            {
                var coord = kv.Key;
                if (_walls.Contains(coord)) continue;
                _graph.AddNode(coord);
            }

            // 4 방향 이웃을 검사해 (그리드 안 + 벽 아님)이면 간선으로 연결.
            foreach (var node in _graph.Nodes)
            {
                foreach (var dir in FourDirections)
                {
                    var next = node + dir;
                    if (!_graph.Contains(next)) continue;
                    _graph.AddEdge(node, next, bidirectional: false);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 3. DFS 실행 (시각화 버전)
        // ─────────────────────────────────────────────────────────────
        //
        // 이 코루틴의 DFS 본체는 DFSAlgorithm.Search / FindPath 와 동일한 흐름이다.
        // BFSGridVisualizer.RunBFS 와 비교해 보면 거의 모든 코드가 같고,
        // 단 한 가지만 바뀐다: Queue<Vector2Int> → Stack<Vector2Int>.
        //
        private IEnumerator RunDFS()
        {
            if (!_graph.Contains(startCoord))
            {
                Debug.LogWarning("[DFS] 시작 좌표가 그래프에 없습니다(벽이거나 그리드 밖).");
                yield break;
            }

            var visited = new HashSet<Vector2Int>();
            var stack   = new Stack<Vector2Int>();
            var parent  = new Dictionary<Vector2Int, Vector2Int>();

            // [5] 시작점을 스택에 올리고 방문 표시.
            stack.Push(startCoord);
            visited.Add(startCoord);

            bool goalFound = false;

            // [6] 스택이 빌 때까지 반복.
            while (stack.Count > 0)
            {
                // [6-1] 스택 위에서 정점 하나 꺼내기.
                var current = stack.Pop();

                // [6-2] '방문 완료' 색으로 칠하기.
                //       단, 시작/목표 칸은 자기 색을 유지(가독성을 위해).
                if (current != startCoord && current != goalCoord)
                {
                    PaintCell(current, colorVisited);
                }

                // 목표에 도달하면 DFS 즉시 중단.
                // ※ DFS 의 경우 이 시점의 경로가 *최단인 보장은 없다*.
                //   하지만 "어떤 경로든 하나" 가 필요한 시나리오엔 충분하다.
                if (current == goalCoord)
                {
                    goalFound = true;
                    break;
                }

                // [6-3] 4 방향 이웃 검사.
                foreach (var dir in FourDirections)
                {
                    var next = current + dir;

                    if (!_graph.Contains(next)) continue;
                    if (!visited.Add(next))      continue;

                    // 부모 기록 (경로 복원용).
                    parent[next] = current;

                    // 스택에 새로 들어가는 셀 = Frontier. 노란색으로 표시.
                    // 단, 목표 셀은 빨간색을 유지해야 한눈에 보이므로 색을 바꾸지 않는다.
                    if (next != goalCoord)
                    {
                        PaintCell(next, colorFrontier);
                    }

                    stack.Push(next);
                }

                yield return new WaitForSeconds(stepDelay);
            }

            // ─────────────────────────────────────────────────────────
            // 4. 발견된 경로 복원 + 색칠
            //    parent 맵을 goal → start 방향으로 거슬러 올라가며 분홍색으로 표시.
            //    ※ BFS 와 같은 코드지만 "최단 경로" 가 아니라 "발견된 경로" 임에 주의.
            // ─────────────────────────────────────────────────────────
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
                Debug.Log("[DFS] 목표 도달! 분홍색 = 발견된 경로 (최단 보장 X).");
            }
            else
            {
                Debug.Log("[DFS] 목표에 도달할 수 없습니다 (서로 다른 컴포넌트).");
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 헬퍼 (BFSGridVisualizer 와 동일)
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
