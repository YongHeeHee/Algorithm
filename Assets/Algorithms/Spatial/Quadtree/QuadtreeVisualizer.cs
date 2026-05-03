using System.Collections;
using System.Collections.Generic;
using Algorithms.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Algorithms.Spatial
{
    /// <summary>
    /// =====================================================================
    ///  Quadtree 시각화 데모 (Unity 용)
    /// =====================================================================
    ///
    /// ▶ 이 데모는 두 단계로 진행된다.
    ///
    ///   [Phase 1] 삽입 애니메이션 — 한 개씩 코루틴으로 점을 삽입
    ///       · 새 점은 노란 플래시 → 회색으로 안정
    ///       · capacity 초과한 leaf 가 4 분할되면 자식 노드의 사각형 라인이 등장
    ///       · BFS 의 frontier 한 칸씩 확장과 같은 리듬
    ///
    ///   [Phase 2] 쿼리 모드 — 마우스를 따라다니는 빨간 사각 영역
    ///       · 쿼리 영역과 *교차하는* 노드의 라인만 노란색 (= 방문)
    ///       · 통째로 스킵된 노드는 회색 그대로 (= 가속의 핵심)
    ///       · 쿼리 결과 점은 분홍색
    ///       · 화면 좌상단 OnGUI 패널에 "방문/스킵 노드 + 검사한 점" 카운터
    ///
    /// ▶ 사용 방법
    ///   1) 빈 GameObject 를 만들고 이 컴포넌트를 붙인다.
    ///   2) Camera 는 위에서 내려다보는 각도로 둔다 (Y 축 기준).
    ///        예) Position (0, 25, 0), Rotation (90, 0, 0), Projection Orthographic, Size 12.
    ///   3) Play → 점들이 한 개씩 삽입되며 분할 라인이 생기고,
    ///      삽입이 끝나면 마우스를 움직여 쿼리 가속 효과를 확인.
    ///
    /// ▶ 색상 의미
    ///     라인 회색 : 미방문 노드 (또는 쿼리와 교차하지 않는 = 스킵)
    ///     라인 노랑 : 방문 노드 (쿼리와 교차)
    ///     라인 빨강 : 마우스 쿼리 영역
    ///     점 회색   : 일반 객체
    ///     점 노랑   : 방금 삽입됨 (Phase 1 의 플래시)
    ///     점 분홍   : 쿼리 결과
    ///
    /// ▶ 시각화 코드와 Quadtree.Query 의 관계
    ///   Phase 2 의 노드 색칠은 트리를 별도로 walk 해서 `Bounds.Intersects(query)` 를
    ///   직접 검사한다 — 이는 알고리즘의 *pruning 조건* 과 정확히 같은 검사이므로
    ///   "노란 노드 = 알고리즘이 실제로 들어간 노드" 가 일치한다.
    /// </summary>
    public class QuadtreeVisualizer : MonoBehaviour, IAlgorithmDemo
    {
        // ─────────────────────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────────────────────

        [Header("월드 영역 (정사각형, -size/2 ~ +size/2)")]
        [SerializeField] private float worldSize = 20f;

        [Header("Quadtree 파라미터")]
        [Tooltip("한 leaf 가 담을 수 있는 최대 점 수. 초과하면 4 분할.")]
        [SerializeField] private int capacity = 4;
        [Tooltip("분할 가능한 최대 깊이. 점이 한 점에 몰릴 때 무한 분할 방지용 안전장치.")]
        [SerializeField] private int maxDepth = 6;

        [Header("점 생성")]
        [SerializeField] private int pointCount = 200;
        [SerializeField] private int randomSeed = 12345;
        [SerializeField] private float pointMarkerSize = 0.25f;

        [Header("Phase 1 — 삽입 애니메이션")]
        [Tooltip("점 한 개 삽입 사이의 대기 시간(초). 0 으로 두면 즉시 완료.")]
        [SerializeField] private float insertStepDelay = 0.03f;
        [SerializeField] private float insertFlashDuration = 0.25f;

        [Header("Phase 2 — 쿼리 모드")]
        [Tooltip("마우스 위치를 중심으로 하는 정사각형 쿼리의 반-변 길이.")]
        [SerializeField] private float queryHalfSize = 2.0f;

        [Header("시각 옵션")]
        [SerializeField] private float lineWidth = 0.05f;
        [SerializeField] private float lineHeightOffset = 0.05f;

        [Header("색상")]
        [SerializeField] private Color colorNodeIdle    = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] private Color colorNodeVisited = new Color(1.00f, 0.90f, 0.20f);
        [SerializeField] private Color colorQueryArea   = new Color(1.00f, 0.20f, 0.20f);
        [SerializeField] private Color colorPointIdle   = new Color(0.70f, 0.70f, 0.70f);
        [SerializeField] private Color colorPointFlash  = new Color(1.00f, 0.90f, 0.20f);
        [SerializeField] private Color colorPointResult = new Color(1.00f, 0.40f, 0.80f);

        // ─────────────────────────────────────────────────────────────
        // Internal state
        // ─────────────────────────────────────────────────────────────

        private Quadtree<int> _tree;

        // 점 데이터와 시각화 GameObject 를 같은 인덱스로 매핑.
        // QuadtreePoint.Data 를 인덱스로 쓰므로 i 번째 점 ↔ i 번째 marker.
        private readonly List<QuadtreePoint<int>> _allPoints = new();
        private readonly List<GameObject> _pointMarkers = new();

        // 노드 → LineRenderer 매핑. 분할이 일어나면 새 라인이 추가된다.
        private readonly Dictionary<Quadtree<int>, LineRenderer> _nodeLines = new();

        // Phase 2 쿼리 영역 라인 (마우스 따라다님).
        private LineRenderer _queryLine;

        private bool _phase2Active;

        // OnGUI 통계 표시용 (Update 에서 갱신).
        private int _lastNodesVisited;
        private int _lastNodesPruned;
        private int _lastPointsChecked;
        private int _lastResultCount;

        // 라인 머티리얼은 한 번만 만들어 모든 LineRenderer 가 공유.
        private static Material _sharedLineMaterial;
        private static Material LineMaterial
        {
            get
            {
                if (_sharedLineMaterial == null)
                {
                    // Sprites/Default 셰이더는 vertex color 를 지원해서
                    // LineRenderer.startColor/endColor 만으로 색을 바꿀 수 있다.
                    var shader = Shader.Find("Sprites/Default");
                    _sharedLineMaterial = new Material(shader);
                }
                return _sharedLineMaterial;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Unity 라이프사이클
        // ─────────────────────────────────────────────────────────────

        private void Start()
        {
            // 첫 실행 = 재시작 흐름과 동일.
            Restart();
        }

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
            _nodeLines.Clear();
            _queryLine = null;
            _phase2Active = false;
            _lastNodesVisited = _lastNodesPruned = _lastPointsChecked = _lastResultCount = 0;

            // [2] 트리 재생성. 월드 영역 = (0,0) 중심의 정사각형.
            var rootBounds = new QuadtreeBounds(0f, 0f, worldSize * 0.5f, worldSize * 0.5f);
            _tree = new Quadtree<int>(rootBounds, capacity, maxDepth);
            EnsureNodeLine(_tree); // 루트 라인 즉시 생성

            // [3] Phase 1 시작.
            StartCoroutine(Phase1Insert());
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 1: 한 개씩 삽입 애니메이션
        // ─────────────────────────────────────────────────────────────

        private IEnumerator Phase1Insert()
        {
            // 동일 시드 = 동일 점 분포 (재현성).
            Random.InitState(randomSeed);
            float halfWorld = worldSize * 0.5f;

            for (int i = 0; i < pointCount; i++)
            {
                // [1] 무작위 점 생성. Data 에 인덱스를 박아 두면
                //     쿼리 결과로 받은 점 → marker GameObject 매핑이 O(1) 이 된다.
                float x = Random.Range(-halfWorld, halfWorld);
                float y = Random.Range(-halfWorld, halfWorld);
                var point = new QuadtreePoint<int>(x, y, i);

                // [2] 트리 삽입. 내부에서 capacity 초과 → Subdivide 가 일어날 수 있다.
                _tree.Insert(point);
                _allPoints.Add(point);

                // [3] 점 마커 생성 + 노란 플래시 → 회색.
                var marker = CreatePointMarker(x, y);
                _pointMarkers.Add(marker);
                StartCoroutine(FlashMarker(marker, colorPointFlash, colorPointIdle, insertFlashDuration));

                // [4] 새로 생긴 자식 노드들의 라인을 만든다 (= 분할 시각화).
                //     이미 만들어진 노드는 EnsureNodeLine 안에서 스킵.
                WalkAndEnsureLines(_tree);

                if (insertStepDelay > 0f)
                {
                    yield return new WaitForSeconds(insertStepDelay);
                }
            }

            // [5] Phase 1 완료 → Phase 2 진입 준비.
            _queryLine = CreateLineRenderer("QueryArea", colorQueryArea, yOffset: lineHeightOffset + 0.5f);
            UpdateRectLine(_queryLine, new QuadtreeBounds(0f, 0f, queryHalfSize, queryHalfSize), lineHeightOffset + 0.5f);
            _phase2Active = true;

            Debug.Log($"[Quadtree] 삽입 완료. 총 {_allPoints.Count} 점, {_nodeLines.Count} 노드. 마우스를 움직여 쿼리해보세요.");
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 2: 마우스 쿼리 (Update 마다)
        // ─────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_phase2Active || _queryLine == null) return;

            // [1] 마우스 위치를 XZ 평면(Y=0) 위 좌표로 변환.
            if (!TryGetMouseOnPlane(out Vector3 mouseWorld)) return;

            // [2] 쿼리 영역 갱신 (마우스를 중심으로 하는 정사각형).
            //     알고리즘의 (X, Y) 가 시각화의 (worldX, worldZ) 이므로
            //     QuadtreeBounds 의 두 번째 축은 mouseWorld.z 가 된다.
            var queryBounds = new QuadtreeBounds(mouseWorld.x, mouseWorld.z, queryHalfSize, queryHalfSize);
            UpdateRectLine(_queryLine, queryBounds, lineHeightOffset + 0.5f);

            // [3] 트리 쿼리 실행 (통계 포함).
            var result = _tree.Query(in queryBounds, out var stats);
            _lastNodesVisited = stats.NodesVisited;
            _lastNodesPruned  = stats.NodesPruned;
            _lastPointsChecked = stats.PointsChecked;
            _lastResultCount  = result.Count;

            // [4] 노드 라인 색칠.
            //     알고리즘과 동일한 검사(Bounds.Intersects)로 판정 → 노랑 = 알고리즘이 실제 들어간 노드.
            foreach (var kv in _nodeLines)
            {
                bool intersects = kv.Key.Bounds.Intersects(in queryBounds);
                SetLineColor(kv.Value, intersects ? colorNodeVisited : colorNodeIdle);
            }

            // [5] 점 마커 색칠 — 일단 모두 회색, 결과 점만 분홍.
            for (int i = 0; i < _pointMarkers.Count; i++)
            {
                SetMarkerColor(_pointMarkers[i], colorPointIdle);
            }
            for (int i = 0; i < result.Count; i++)
            {
                int idx = result[i].Data;
                if (idx >= 0 && idx < _pointMarkers.Count)
                {
                    SetMarkerColor(_pointMarkers[idx], colorPointResult);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // OnGUI: Brute Force vs Quadtree 카운터
        // ─────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_phase2Active) return;

            const int W = 320, H = 130;
            GUI.Box(new Rect(10, 10, W, H), "");

            int total = _allPoints.Count;
            string text =
                "<b>Quadtree Range Query</b>\n" +
                $"전체 점: {total}\n" +
                $"방문 노드: {_lastNodesVisited}    스킵 노드: {_lastNodesPruned}\n" +
                $"검사한 점: {_lastPointsChecked}    (Brute Force = {total})\n" +
                $"결과 점: {_lastResultCount}";

            var style = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13 };
            GUI.Label(new Rect(20, 18, W - 20, H - 20), text, style);
        }

        // ─────────────────────────────────────────────────────────────
        // 트리 walk → LineRenderer 보장 (없는 노드만 새로 생성)
        // ─────────────────────────────────────────────────────────────

        private void WalkAndEnsureLines(Quadtree<int> node)
        {
            EnsureNodeLine(node);
            if (node.IsSubdivided)
            {
                // node.Children 은 [NW, NE, SW, SE] 4 개.
                for (int i = 0; i < node.Children.Count; i++)
                {
                    WalkAndEnsureLines(node.Children[i]);
                }
            }
        }

        private void EnsureNodeLine(Quadtree<int> node)
        {
            if (_nodeLines.ContainsKey(node)) return;

            // 깊은 노드일수록 살짝 위에 그려서 z-fighting / 가려짐 방지.
            float y = lineHeightOffset + node.Depth * 0.002f;
            var line = CreateLineRenderer($"Node_d{node.Depth}", colorNodeIdle, y);
            UpdateRectLine(line, node.Bounds, y);
            _nodeLines[node] = line;
        }

        // ─────────────────────────────────────────────────────────────
        // LineRenderer 헬퍼
        // ─────────────────────────────────────────────────────────────

        private LineRenderer CreateLineRenderer(string name, Color color, float yOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.material      = LineMaterial;
            line.startWidth    = lineWidth;
            line.endWidth      = lineWidth;
            line.positionCount = 5; // 사각형 = 4 모서리 + 시작점으로 닫기
            line.startColor    = color;
            line.endColor      = color;
            // yOffset 인자는 명시적 표기용 — 위치 자체는 UpdateRectLine 에서 세팅.
            _ = yOffset;
            return line;
        }

        private static void UpdateRectLine(LineRenderer line, in QuadtreeBounds b, float y)
        {
            // 알고리즘 좌표 (X, Y) → 월드 (worldX, worldZ). Y 는 평면 높이.
            line.SetPosition(0, new Vector3(b.MinX, y, b.MinY));
            line.SetPosition(1, new Vector3(b.MaxX, y, b.MinY));
            line.SetPosition(2, new Vector3(b.MaxX, y, b.MaxY));
            line.SetPosition(3, new Vector3(b.MinX, y, b.MaxY));
            line.SetPosition(4, new Vector3(b.MinX, y, b.MinY)); // 닫기
        }

        private static void SetLineColor(LineRenderer line, Color c)
        {
            // LineRenderer 는 vertex color 를 사용하므로 머티리얼 인스턴싱이 발생하지 않는다.
            line.startColor = c;
            line.endColor   = c;
        }

        // ─────────────────────────────────────────────────────────────
        // 점 마커
        // ─────────────────────────────────────────────────────────────

        private GameObject CreatePointMarker(float x, float y)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.position = new Vector3(x, 0f, y);
            go.transform.localScale = Vector3.one * pointMarkerSize;

            // collider 는 마우스 입력 평면 계산에 불필요 + 약간의 비용이라 제거.
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            SetMarkerColor(go, colorPointIdle);
            return go;
        }

        private static void SetMarkerColor(GameObject go, Color c)
        {
            // Unity 의 == 오버로드는 Destroy 된 오브젝트도 null 로 판정한다.
            if (go == null) return;
            if (go.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = c;
            }
        }

        private IEnumerator FlashMarker(GameObject go, Color flash, Color rest, float duration)
        {
            SetMarkerColor(go, flash);
            yield return new WaitForSeconds(duration);
            // Phase 2 가 시작되면 Update 가 매 프레임 색을 다시 칠하므로 재설정 불필요.
            // Phase 1 도중이라면 안전하게 회색으로 복귀.
            if (!_phase2Active) SetMarkerColor(go, rest);
        }

        // ─────────────────────────────────────────────────────────────
        // 마우스 → 월드 (XZ 평면 Y=0)
        //   Physics.Raycast 가 아닌 수학적 평면 교차로 계산 → collider 영향 없음.
        //   ※ 신규 Input System 사용 (Player Settings 의 Active Input Handling 이
        //     "Input System Package (New)" 또는 "Both" 여야 함).
        //     MCTS / TicTacToe Visualizer 와 같은 패턴.
        // ─────────────────────────────────────────────────────────────

        private static bool TryGetMouseOnPlane(out Vector3 worldPos)
        {
            worldPos = default;
            var cam = Camera.main;
            if (cam == null) return false;

            // Mouse.current 가 null 이면 마우스 디바이스가 없거나 Input System 이 비활성.
            var mouse = Mouse.current;
            if (mouse == null) return false;

            Vector2 screenPos = mouse.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(screenPos);

            // ray.direction.y 가 거의 0 이면 평면과 거의 평행 → 교차 없음.
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;

            // 평면 Y=0 위의 점:  origin.y + t * dir.y = 0  →  t = -origin.y / dir.y
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) return false; // 카메라 뒤쪽 평면 — 무시

            worldPos = ray.origin + ray.direction * t;
            return true;
        }
    }
}
