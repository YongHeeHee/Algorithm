using System.Collections;
using System.Collections.Generic;
using Algorithms.Common;
using UnityEngine;

namespace Algorithms.Search
{
    /// <summary>
    /// =====================================================================
    ///  BFS 그리드 시각화 데모 (Unity 용)
    /// =====================================================================
    ///
    /// ▶ 사용 방법
    ///   1) 빈 GameObject 를 만들고 이 컴포넌트를 붙인다.
    ///   2) (선택) Cell Prefab 슬롯에 Cube/Quad 같은 메시 프리팹을 넣는다.
    ///        - 비워두면 기본 Cube 가 자동 생성되어 바로 실행된다.
    ///   3) Play 버튼을 누르면 그리드가 만들어지고, 시작점에서 BFS 가 한 스텝씩 진행된다.
    ///
    /// ▶ 색상 의미
    ///     회색  : 미방문 칸
    ///     노랑  : 큐에 들어감 (곧 방문 예정, 일명 'Frontier')
    ///     파랑  : 방문 완료
    ///     초록  : 시작 정점
    ///     빨강  : 목표 정점
    ///     분홍  : 최단 경로 위에 있는 칸
    ///     검정  : 벽 (이동 불가)
    ///
    /// ▶ 시각화 코드와 BFSAlgorithm.Search 의 관계
    ///   이 코루틴 RunBFS 는 BFSAlgorithm.Search / FindPath 와 사실상 동일한 BFS 로직이지만,
    ///   매 스텝마다 셀 색상을 바꾸고 WaitForSeconds 로 잠시 멈춰서
    ///   사람이 진행 과정을 눈으로 볼 수 있게 한다.
    ///   학습 목적상 알고리즘 본체와 시각화 코드를 같이 두고 비교해 보면 이해가 빨라진다.
    /// </summary>
    public class BFSGridVisualizer : MonoBehaviour, IAlgorithmDemo
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

        // 좌표(Vector2Int) → 그 좌표에 놓인 셀 GameObject.
        // BFS 가 진행되며 색상을 바꿀 때 좌표만으로 빠르게 셀을 찾기 위함.
        private readonly Dictionary<Vector2Int, GameObject> _cellObjects = new();

        // 벽으로 막힌 칸 집합. 그래프 정점에서 제외 → 자연스럽게 '이동 불가' 처리.
        private readonly HashSet<Vector2Int> _walls = new();

        // BFS 가 동작할 인접 리스트 그래프.
        private Graph<Vector2Int> _graph;

        // 그리드 기반 BFS 의 4 방향 이웃 오프셋 (상하좌우).
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
            // [1] 진행 중인 BFS 코루틴이 있으면 중단.
            //     같은 시드로 재시작이지만, 도중에 누른 경우 이전 코루틴이 살아있으면
            //     색칠이 충돌하므로 반드시 멈춰야 한다.
            StopAllCoroutines();

            // [2] 이전에 만들어진 셀 GameObject 들을 모두 제거 + 내부 상태 정리.
            //     ※ Destroy 는 프레임 끝에 실행되지만, 새 셀은 곧바로 만들어진다.
            //       한 프레임 동안 시각적 겹침이 있을 수 있는데 데모 용도로는 무시 가능.
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            _cellObjects.Clear();
            _walls.Clear();

            // [3] 그리드/그래프 재구성 + BFS 코루틴 재시작.
            BuildGrid();
            BuildGraph();
            StartCoroutine(RunBFS());
        }

        // ─────────────────────────────────────────────────────────────
        // 1. 그리드 / 벽 생성
        // ─────────────────────────────────────────────────────────────

        private void BuildGrid()
        {
            // 동일한 시드로 동일한 미로가 재현되도록 Random 시드를 고정.
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

                    // 셀 GameObject 생성. 프리팹이 비어 있으면 기본 Cube 로 대체 → 셋업 없이 바로 실행 가능.
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

                    // 초기 색 칠하기. 우선순위: 벽 > 시작 > 목표 > 미방문.
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
        // 2. 그래프 구성
        // ─────────────────────────────────────────────────────────────

        private void BuildGraph()
        {
            _graph = new Graph<Vector2Int>();

            // 벽이 아닌 셀들만 정점으로 등록한다.
            // → 벽인 칸은 그래프에 존재하지 않으므로 BFS 가 자연스럽게 그곳을 피해 간다.
            foreach (var kv in _cellObjects)
            {
                var coord = kv.Key;
                if (_walls.Contains(coord)) continue;
                _graph.AddNode(coord);
            }

            // 각 정점의 4 방향 이웃을 검사해 (그리드 안 + 벽 아님)이면 간선으로 연결한다.
            foreach (var node in _graph.Nodes)
            {
                foreach (var dir in FourDirections)
                {
                    var next = node + dir;

                    // 이웃이 그래프에 등록돼 있지 않다 = 그리드 밖이거나 벽.
                    if (!_graph.Contains(next)) continue;

                    // bidirectional: false 로 추가하지만,
                    // 양쪽 정점에서 이 루프가 한 번씩 도므로 결과적으로 양방향 간선이 된다.
                    // (양쪽에서 true 로 추가하면 같은 간선이 두 번 등록되어 중복이 발생)
                    _graph.AddEdge(node, next, bidirectional: false);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 3. BFS 실행 (시각화 버전)
        // ─────────────────────────────────────────────────────────────
        //
        // 이 코루틴의 BFS 본체는 BFSAlgorithm.Search / FindPath 와 동일한 흐름이다.
        // 다른 점은 두 가지뿐:
        //   - 매 스텝 셀 색상을 바꾼다.
        //   - WaitForSeconds 로 잠시 멈춰 사람이 진행을 볼 수 있게 한다.
        //
        private IEnumerator RunBFS()
        {
            // 시작점이 벽이거나 그래프 밖이면 실행 불가.
            if (!_graph.Contains(startCoord))
            {
                Debug.LogWarning("[BFS] 시작 좌표가 그래프에 없습니다(벽이거나 그리드 밖).");
                yield break;
            }

            var visited = new HashSet<Vector2Int>();
            var queue   = new Queue<Vector2Int>();
            var parent  = new Dictionary<Vector2Int, Vector2Int>();

            // [5] 시작점을 큐에 넣고 방문 표시.
            queue.Enqueue(startCoord);
            visited.Add(startCoord);

            bool goalFound = false;

            // [6] 큐가 빌 때까지 반복.
            while (queue.Count > 0)
            {
                // [6-1] 큐 앞에서 정점 하나 꺼내기.
                var current = queue.Dequeue();

                // [6-2] '방문 완료' 색으로 칠하기.
                //       단, 시작/목표 칸은 자기 색을 유지(가독성을 위해).
                if (current != startCoord && current != goalCoord)
                {
                    PaintCell(current, colorVisited);
                }

                // 목표에 도달하면 BFS 즉시 중단 → 최단 경로 보장.
                if (current == goalCoord)
                {
                    goalFound = true;
                    break;
                }

                // [6-3] 4 방향 이웃 검사.
                foreach (var dir in FourDirections)
                {
                    var next = current + dir;

                    // 그래프에 없는 좌표(밖/벽) 또는 이미 방문된 좌표는 건너뛴다.
                    if (!_graph.Contains(next)) continue;
                    if (!visited.Add(next))      continue;

                    // 부모 기록 (경로 복원용).
                    parent[next] = current;

                    // 큐에 새로 들어가는 셀 = Frontier. 노란색으로 표시.
                    // 단, 목표 셀은 빨간색을 유지해야 한눈에 보이므로 색을 바꾸지 않는다.
                    if (next != goalCoord)
                    {
                        PaintCell(next, colorFrontier);
                    }

                    queue.Enqueue(next);
                }

                // 한 정점 처리가 끝났으니 잠시 대기 → 사람이 한 스텝씩 볼 수 있다.
                yield return new WaitForSeconds(stepDelay);
            }

            // ─────────────────────────────────────────────────────────
            // 4. 최단 경로 복원 + 색칠
            //    parent 맵을 goal → start 방향으로 거슬러 올라가며 경로 셀에 분홍색을 칠한다.
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
                Debug.Log("[BFS] 목표 도달! 분홍색 = 최단 경로.");
            }
            else
            {
                Debug.Log("[BFS] 목표에 도달할 수 없습니다 (서로 다른 컴포넌트).");
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
