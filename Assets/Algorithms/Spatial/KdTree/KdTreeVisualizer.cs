using System.Collections;
using System.Collections.Generic;
using Algorithms.Common;
using Algorithms.Physics; // AABB — 노드 cell 영역 표현 (KdTree 알고리즘과 공유)
using UnityEngine;
using UnityEngine.InputSystem;

namespace Algorithms.Spatial
{
    /// <summary>
    /// =====================================================================
    ///  K-d tree 시각화 데모 (Unity 용) — kNN Query 전용
    /// =====================================================================
    ///
    /// ▶ 이 데모는 두 단계로 진행된다.
    ///
    ///   [Phase 1] Build 애니메이션 — *Mondrian 패턴* 으로 분할선이 한 개씩 등장
    ///       · 모든 점이 화면에 회색으로 등장
    ///       · BFS 순서 (루트 → 자식 → 손자) 로 노드의 분할선이 한 개씩 페이드인
    ///       · 깊이 짝수 (axis=X) → *세로선*, 홀수 (axis=Y) → *가로선*
    ///       · 분할선은 *부모 cell 안에서만* 그려져 직사각형 영역들로 채워진다 (= Mondrian 그림)
    ///
    ///   [Phase 2] kNN Query — 마우스 주변 가장 가까운 k 개 점
    ///       · 마우스 위치 = 쿼리 점
    ///       · 매 프레임 KNearest(k) 실행 → 결과 + 통계
    ///       · 마우스 주변에 *현재 best-k 의 worst 거리* 만큼 노란 원 (반경)
    ///         → 점이 모인 곳에 마우스를 가져가면 원이 *작아진다*
    ///         → 외딴 곳에 가져가면 원이 *커진다*
    ///       · 점 색칠:
    ///           - 분홍 = 현재 top-k 안에 든 점
    ///           - 주황 = 검사는 했지만 top-k 에 못 든 점 (visited but not in result)
    ///           - 회색 = pruned (가지치기로 검사 자체 안 됨)
    ///       · 분할선 색칠:
    ///           - 주황 = 알고리즘이 *실제 들어간* 노드의 분할선
    ///           - 회색 = 가지치기된 서브트리의 분할선 (반투명)
    ///       · OnGUI: 방문 / 가지치기 노드 + 결과 거리 + Brute Force 비교
    ///
    /// ▶ 색상 의미
    ///     분할선 (X 축)  : 옅은 파랑 (idle)        / 주황 (visited) / 회색 (pruned)
    ///     분할선 (Y 축)  : 옅은 분홍 (idle)        / 주황 (visited) / 회색 (pruned)
    ///         → 두 축 색을 다르게 둬 *번갈아 가르는* 패턴이 한눈에 보이게 함
    ///     점             : 회색 (pruned/untested) / 주황 (visited)  / 분홍 (top-k)
    ///     반경 원        : 노랑 (현재 best-k 의 worst 거리)
    ///     쿼리 점        : 파랑 (마우스 위치)
    ///
    /// ▶ BVH 데모와의 연결
    ///   같은 Spatial 카테고리의 4 형제 (Quadtree / Spatial Hashing / BVH / K-d tree) 가
    ///   각자 다른 *킬러 쿼리* 를 보여준다:
    ///     Quadtree         → 영역 쿼리 (사각 영역 내 점)
    ///     Spatial Hashing  → 영역 쿼리 (동적 객체 + 균등 격자)
    ///     BVH              → 광선 쿼리 (레이트레이싱)
    ///     **K-d tree**     → **kNN 쿼리** (가장 가까운 k 개)
    ///
    /// ▶ 사용 방법
    ///   1) 빈 GameObject 를 만들고 이 컴포넌트를 붙인다.
    ///   2) Camera 위에서 내려다보는 각도 (Position (0, 25, 0), Rotation (90, 0, 0),
    ///      Projection Orthographic, Size 12 — Quadtree / BVH 와 동일).
    ///   3) Play → Phase 1 (분할선 BFS 등장) 후 Phase 2 (마우스 kNN) 자동 진입.
    /// </summary>
    public class KdTreeVisualizer : MonoBehaviour, IAlgorithmDemo
    {
        // ─────────────────────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────────────────────

        [Header("월드 영역 (정사각형, -size/2 ~ +size/2)")]
        [SerializeField] private float worldSize = 20f;

        [Header("점 생성")]
        [SerializeField] private int pointCount = 50;
        [SerializeField] private int randomSeed = 12345;
        [SerializeField] private float pointMarkerSize = 0.35f;

        [Header("kNN 파라미터")]
        [Tooltip("가장 가까운 몇 개의 점을 찾을지. 1 ~ 20 권장.")]
        [Range(1, 20)]
        [SerializeField] private int k = 5;

        [Header("Phase 1 — Build 애니메이션")]
        [Tooltip("BFS 노드 등장 사이의 대기 시간(초). 0 으로 두면 즉시 완료.")]
        [SerializeField] private float buildStepDelay = 0.07f;

        [Header("시각 옵션")]
        [SerializeField] private float splitLineWidth = 0.05f;
        [SerializeField] private float radiusCircleWidth = 0.08f;
        [Tooltip("반경 원의 분할 세그먼트 수. 64 면 부드러운 원.")]
        [SerializeField] private int radiusCircleSegments = 64;
        [SerializeField] private float lineHeightOffset = 0.05f;

        [Header("색상 — 분할선 (idle 시 축별 색 구분)")]
        [SerializeField] private Color colorSplitXAxisIdle = new Color(0.45f, 0.65f, 0.95f);
        [SerializeField] private Color colorSplitYAxisIdle = new Color(0.95f, 0.55f, 0.75f);
        [SerializeField] private Color colorSplitVisited   = new Color(1.00f, 0.65f, 0.10f);
        [SerializeField] private Color colorSplitPruned    = new Color(0.30f, 0.30f, 0.30f, 0.5f);

        [Header("색상 — 점")]
        [SerializeField] private Color colorPointIdle    = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] private Color colorPointVisited = new Color(1.00f, 0.65f, 0.20f);
        [SerializeField] private Color colorPointTopK    = new Color(1.00f, 0.40f, 0.80f);

        [Header("색상 — 쿼리 / 반경")]
        [SerializeField] private Color colorRadius = new Color(1.00f, 0.95f, 0.30f);
        [SerializeField] private Color colorQuery  = new Color(0.30f, 0.60f, 1.00f);

        // ─────────────────────────────────────────────────────────────
        // Internal state
        // ─────────────────────────────────────────────────────────────

        private KdTree<int> _tree;
        private readonly List<KdPoint<int>> _allPoints = new();
        private readonly List<GameObject> _pointMarkers = new();

        // 노드 → 분할선 LineRenderer 매핑.
        private readonly Dictionary<KdNode<int>, LineRenderer> _splitLines = new();

        // Phase 2 — 반경 원 + 쿼리 점.
        private LineRenderer _radiusCircle;
        private GameObject _queryMarker;

        private bool _phase2Active;

        // OnGUI 통계.
        private int _lastNodesVisited;
        private int _lastNodesPruned;
        private int _lastResultCount;
        private float _lastWorstDist; // 현재 best-k 의 worst 거리 (= 반경)

        // 라인 머티리얼 — Quadtree / BVH 와 같은 패턴.
        private static Material _sharedLineMaterial;
        private static Material LineMaterial
        {
            get
            {
                if (_sharedLineMaterial == null)
                {
                    var shader = Shader.Find("Sprites/Default");
                    _sharedLineMaterial = new Material(shader);
                }
                return _sharedLineMaterial;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Unity 라이프사이클
        // ─────────────────────────────────────────────────────────────

        private void Start() => Restart();

        public void Restart()
        {
            // [1] 진행 중 코루틴 / 자식 GameObject 정리.
            StopAllCoroutines();
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            _allPoints.Clear();
            _pointMarkers.Clear();
            _splitLines.Clear();
            _radiusCircle = null;
            _queryMarker = null;
            _phase2Active = false;
            _lastNodesVisited = _lastNodesPruned = _lastResultCount = 0;
            _lastWorstDist = 0f;

            // [2] 점 생성 + 트리 빌드 (알고리즘 본체는 동기, 시각화만 코루틴).
            GeneratePoints();
            float halfWorld = worldSize * 0.5f;
            var worldBounds = new AABB(0f, 0f, halfWorld, halfWorld);
            _tree = new KdTree<int>(_allPoints, in worldBounds);

            // [3] Phase 1 시작 — 분할선을 BFS 순서로 등장.
            StartCoroutine(Phase1Build());
        }

        // ─────────────────────────────────────────────────────────────
        // 점 생성 — Quadtree 와 같은 패턴
        // ─────────────────────────────────────────────────────────────

        private void GeneratePoints()
        {
            Random.InitState(randomSeed);
            float halfWorld = worldSize * 0.5f;

            for (int i = 0; i < pointCount; i++)
            {
                float x = Random.Range(-halfWorld, halfWorld);
                float y = Random.Range(-halfWorld, halfWorld);
                _allPoints.Add(new KdPoint<int>(x, y, i));

                var marker = CreatePointMarker(x, y);
                _pointMarkers.Add(marker);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 1: 분할선 BFS 등장
        // ─────────────────────────────────────────────────────────────

        private IEnumerator Phase1Build()
        {
            // 트리는 이미 빌드됨. BFS 순회하며 분할선을 한 개씩 등장.
            // 루트의 세로선이 화면을 둘로 가르고 → 자식들의 가로선이 그 안을 또 가르고 →
            // 깊이가 깊어질수록 격자가 촘촘해지는 *Mondrian* 패턴이 완성된다.
            foreach (var node in _tree.WalkBFS())
            {
                EnsureSplitLine(node);
                if (buildStepDelay > 0f)
                {
                    yield return new WaitForSeconds(buildStepDelay);
                }
            }

            // Phase 2 진입 준비 — 쿼리 마커 + 반경 원.
            _queryMarker = CreateQueryMarker();
            _radiusCircle = CreateCircleLineRenderer("RadiusCircle", colorRadius, lineHeightOffset + 0.6f);
            _phase2Active = true;

            Debug.Log($"[K-d tree] Build 완료. {_tree.NodeCount} 노드, max depth {_tree.MaxDepth}, " +
                      $"{_allPoints.Count} 점. 마우스를 움직여 kNN 검색을 확인하세요. (k={k})");
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 2: kNN 쿼리 (Update 마다)
        // ─────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_phase2Active || _radiusCircle == null) return;

            // [1] 마우스 위치 → 쿼리 점.
            if (!TryGetMouseOnPlane(out Vector3 mouseWorld)) return;
            float qx = mouseWorld.x;
            float qy = mouseWorld.z;

            // 쿼리 마커 위치 갱신.
            if (_queryMarker != null)
            {
                _queryMarker.transform.position = new Vector3(qx, lineHeightOffset + 0.5f, qy);
            }

            // [2] kNN 실행 — 알고리즘 본체.
            var results = _tree.KNearest(qx, qy, k, out var stats);
            _lastNodesVisited = stats.NodesVisited;
            _lastNodesPruned  = stats.NodesPruned;
            _lastResultCount  = results.Count;
            _lastWorstDist    = results.Count > 0 ? Mathf.Sqrt(results[results.Count - 1].DistSq) : 0f;

            // [3] 반경 원 갱신 — 현재 best-k 의 worst 거리 만큼.
            UpdateCircle(_radiusCircle, qx, qy, _lastWorstDist, lineHeightOffset + 0.6f);

            // [4] 점 색칠 — 일단 모두 idle (= pruned 가정), walk 도중 visited / topK 로 덮어씀.
            for (int i = 0; i < _pointMarkers.Count; i++)
            {
                SetPointColor(_pointMarkers[i], colorPointIdle);
            }

            // top-k 점 인덱스 집합 (분홍 칠하기 용).
            var topKIndices = new HashSet<int>();
            for (int i = 0; i < results.Count; i++) topKIndices.Add(results[i].Point.Data);

            // 분할선 + 점을 알고리즘과 같은 검사 (near/far + pruning) 로 walk 하며 색칠.
            WalkAndColor(_tree.Root, qx, qy, results, topKIndices);
        }

        /// <summary>
        /// 알고리즘의 KnnRecursive 와 *동일한* near/far 결정 + 가지치기 검사를 사용해
        /// 분할선과 점을 색칠한다. → 시각화의 "주황 = 알고리즘이 실제 들어간 노드" 가 1:1 일치.
        /// </summary>
        private void WalkAndColor(KdNode<int> node, float qx, float qy, List<KnnResult<int>> results, HashSet<int> topKIndices)
        {
            if (node == null) return;

            // 이 노드는 방문됨 — 분할선 주황, 점은 topK 면 분홍, 아니면 visited 주황.
            SetSplitLineColor(node, colorSplitVisited);
            int idx = node.Point.Data;
            if (idx >= 0 && idx < _pointMarkers.Count)
            {
                Color c = topKIndices.Contains(idx) ? colorPointTopK : colorPointVisited;
                SetPointColor(_pointMarkers[idx], c);
            }

            // near / far 결정.
            int axis = node.SplitAxis;
            float queryAxisVal = axis == 0 ? qx : qy;
            float nodeAxisVal = axis == 0 ? node.Point.X : node.Point.Y;
            float axisDelta = queryAxisVal - nodeAxisVal;

            KdNode<int> near = axisDelta < 0f ? node.Left : node.Right;
            KdNode<int> far  = axisDelta < 0f ? node.Right : node.Left;

            // near 항상 재귀.
            WalkAndColor(near, qx, qy, results, topKIndices);

            // far 가지치기 검사 — 알고리즘과 같은 조건.
            float worstDistSq = results.Count < k ? float.PositiveInfinity : results[results.Count - 1].DistSq;
            float planeDistSq = axisDelta * axisDelta;

            if (results.Count < k || planeDistSq < worstDistSq)
            {
                WalkAndColor(far, qx, qy, results, topKIndices);
            }
            else
            {
                // 가지치기 — far 서브트리 전체의 분할선을 회색으로.
                ColorSubtreeAsPruned(far);
            }
        }

        /// <summary>이 노드와 모든 자손의 분할선을 회색으로 (= 가지치기 시각화).</summary>
        private void ColorSubtreeAsPruned(KdNode<int> node)
        {
            if (node == null) return;
            SetSplitLineColor(node, colorSplitPruned);
            // 점은 이미 idle (회색) 로 초기화돼 있어 추가 작업 불필요.
            ColorSubtreeAsPruned(node.Left);
            ColorSubtreeAsPruned(node.Right);
        }

        // ─────────────────────────────────────────────────────────────
        // OnGUI: Brute Force vs K-d tree 카운터
        // ─────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_phase2Active) return;

            const int W = 360, H = 145;
            GUI.Box(new Rect(10, 10, W, H), "");

            int total = _allPoints.Count;
            string text =
                "<b>K-d tree kNN Query</b>\n" +
                $"전체 점: {total}    노드: {(_tree?.NodeCount ?? 0)}    max depth: {(_tree?.MaxDepth ?? 0)}\n" +
                $"k = {k}    결과 점: {_lastResultCount}    k 번째 거리: {_lastWorstDist:F2}\n" +
                $"방문 노드: {_lastNodesVisited}    가지치기 분기: {_lastNodesPruned}\n" +
                $"(Brute Force = {total} 점 모두 거리 계산)";

            var style = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13 };
            GUI.Label(new Rect(20, 18, W - 20, H - 20), text, style);
        }

        // ─────────────────────────────────────────────────────────────
        // 분할선 (LineRenderer) 헬퍼
        // ─────────────────────────────────────────────────────────────

        private void EnsureSplitLine(KdNode<int> node)
        {
            if (_splitLines.ContainsKey(node)) return;

            // 깊을수록 살짝 위에 그려서 z-fighting 방지.
            float y = lineHeightOffset + node.Depth * 0.003f;
            Color idleColor = node.SplitAxis == 0 ? colorSplitXAxisIdle : colorSplitYAxisIdle;

            var line = CreateLineRenderer($"Split_d{node.Depth}_{(node.SplitAxis == 0 ? "X" : "Y")}", idleColor, y, splitLineWidth);
            UpdateSplitLineGeometry(line, node, y);
            _splitLines[node] = line;
        }

        /// <summary>
        /// 노드의 분할선 좌표 갱신.
        ///   X 축 분할 (axis=0) → 세로선: x = node.Point.X, y 는 cell 의 [MinY, MaxY]
        ///   Y 축 분할 (axis=1) → 가로선: y = node.Point.Y, x 는 cell 의 [MinX, MaxX]
        /// 부모 cell 안에서만 그려지므로 *Mondrian* 패턴이 완성된다.
        /// </summary>
        private static void UpdateSplitLineGeometry(LineRenderer line, KdNode<int> node, float y)
        {
            line.positionCount = 2;
            var cell = node.CellBounds;
            if (node.SplitAxis == 0)
            {
                // 세로선
                float x = node.Point.X;
                line.SetPosition(0, new Vector3(x, y, cell.MinY));
                line.SetPosition(1, new Vector3(x, y, cell.MaxY));
            }
            else
            {
                // 가로선
                float yLine = node.Point.Y;
                line.SetPosition(0, new Vector3(cell.MinX, y, yLine));
                line.SetPosition(1, new Vector3(cell.MaxX, y, yLine));
            }
        }

        private void SetSplitLineColor(KdNode<int> node, Color c)
        {
            if (_splitLines.TryGetValue(node, out var line))
            {
                line.startColor = c;
                line.endColor = c;
            }
        }

        private LineRenderer CreateLineRenderer(string name, Color color, float yOffset, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.material = LineMaterial;
            line.startWidth = width;
            line.endWidth = width;
            line.positionCount = 2;
            line.startColor = color;
            line.endColor = color;
            _ = yOffset;
            return line;
        }

        // ─────────────────────────────────────────────────────────────
        // 반경 원 (LineRenderer 로 N 분할 다각형 → 시각적 원)
        // ─────────────────────────────────────────────────────────────

        private LineRenderer CreateCircleLineRenderer(string name, Color color, float yOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.material = LineMaterial;
            line.startWidth = radiusCircleWidth;
            line.endWidth = radiusCircleWidth;
            line.loop = true; // 닫힌 원
            line.positionCount = Mathf.Max(8, radiusCircleSegments);
            line.startColor = color;
            line.endColor = color;
            _ = yOffset;
            return line;
        }

        private void UpdateCircle(LineRenderer line, float cx, float cy, float radius, float y)
        {
            int n = line.positionCount;
            for (int i = 0; i < n; i++)
            {
                float t = (i / (float)n) * Mathf.PI * 2f;
                float x = cx + Mathf.Cos(t) * radius;
                float z = cy + Mathf.Sin(t) * radius;
                line.SetPosition(i, new Vector3(x, y, z));
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 점 / 쿼리 마커
        // ─────────────────────────────────────────────────────────────

        private GameObject CreatePointMarker(float x, float y)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.position = new Vector3(x, 0f, y);
            go.transform.localScale = Vector3.one * pointMarkerSize;

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            SetPointColor(go, colorPointIdle);
            return go;
        }

        private GameObject CreateQueryMarker()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localScale = Vector3.one * (pointMarkerSize * 1.5f);

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            SetPointColor(go, colorQuery);
            return go;
        }

        private static void SetPointColor(GameObject go, Color c)
        {
            if (go == null) return;
            if (go.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = c;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 마우스 → 월드 (XZ 평면 Y=0). Quadtree / BVH 와 같은 패턴.
        // ─────────────────────────────────────────────────────────────

        private static bool TryGetMouseOnPlane(out Vector3 worldPos)
        {
            worldPos = default;
            var cam = Camera.main;
            if (cam == null) return false;

            var mouse = Mouse.current;
            if (mouse == null) return false;

            Vector2 screenPos = mouse.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(screenPos);

            if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;

            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) return false;

            worldPos = ray.origin + ray.direction * t;
            return true;
        }
    }
}
