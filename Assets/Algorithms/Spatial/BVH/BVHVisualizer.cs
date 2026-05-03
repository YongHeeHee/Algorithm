using System.Collections;
using System.Collections.Generic;
using Algorithms.Common;
using Algorithms.Physics; // AABB — 노드 / 객체 박스 + Slab Raycast 재사용
using UnityEngine;
using UnityEngine.InputSystem;

namespace Algorithms.Spatial
{
    /// <summary>
    /// =====================================================================
    ///  BVH 시각화 데모 (Unity 용) — Ray Query 전용
    /// =====================================================================
    ///
    /// ▶ 이 데모는 두 단계로 진행된다.
    ///
    ///   [Phase 1] Build 애니메이션 — top-down 으로 트리가 한 노드씩 등장
    ///       · 모든 객체 (작은 사각형) 가 화면에 회색으로 등장
    ///       · BFS 순서 (루트 → 자식 → 손자) 로 노드 AABB 외곽선이 한 개씩 페이드인
    ///       · 깊이별로 외곽선 색이 달라져 트리 구조가 한눈에 보임
    ///       · ※ Quadtree 와 결정적 차이 — 형제 AABB 가 *겹친다*. 객체를 따라 박스가 그려지므로
    ///         자연스럽게 overlap 발생. 이게 BVH 정체성의 시각적 증거.
    ///
    ///   [Phase 2] Ray Query 모드 — 마우스를 향해 광선 발사
    ///       · 광선 origin 은 화면 좌하단 *고정*, 끝점은 마우스 위치
    ///       · 트리를 내려가며 각 노드 AABB 에 광선 검사:
    ///           - 안 맞는 노드 → 서브트리 *통째 가지치기* (외곽선 흐려짐)
    ///           - 맞는 노드 → 들어가서 자식 재귀 (외곽선 주황)
    ///       · leaf 안의 객체 색칠:
    ///           - 회색  : pruned (서브트리 자체 스킵 — 광선 검사도 안 됨)
    ///           - 주황  : 검사는 했지만 ray miss
    ///           - 분홍  : ray hit (광선이 실제 통과)
    ///       · OnGUI 카운터: 방문/스킵 노드 + 검사 객체 vs Brute Force 전체 N + 결과 hit 수
    ///
    /// ▶ 색상 의미
    ///     외곽선 흰색  : idle (Phase 1 등장 직후)
    ///     외곽선 주황  : 광선이 통과 = 알고리즘이 *실제로 들어간* 노드
    ///     외곽선 회색  : 가지치기 = 서브트리 통째 스킵
    ///     광선 노랑    : 활성 광선
    ///     객체 회색    : pruned (검사 자체 안 됨)
    ///     객체 주황    : 검사했지만 miss
    ///     객체 분홍    : ray hit
    ///     원점 파랑 점 : 광선 발사 위치 (고정)
    ///
    /// ▶ AABB 데모와의 연결
    ///   광선 ↔ 노드 외곽선 검사는 `AABB.Raycast` (Slab method) 를 *그대로* 호출한다.
    ///   AABB 데모의 3 번째 패널 (Slab Raycast) 에서 본 그 함수가, BVH 에서는 트리 모든 노드에서
    ///   호출되는 *부품* 으로 등장한다. "원자 → 분자" 학습 스토리의 시각적 완성.
    ///
    /// ▶ 사용 방법
    ///   1) 빈 GameObject 를 만들고 이 컴포넌트를 붙인다.
    ///   2) Camera 는 위에서 내려다보는 각도. 예) Position (0, 25, 0), Rotation (90, 0, 0),
    ///      Projection Orthographic, Size 12 (Quadtree 와 동일).
    ///   3) Play → Phase 1 (트리 구축 애니메이션) 후 Phase 2 (마우스 광선) 자동 진입.
    /// </summary>
    public class BVHVisualizer : MonoBehaviour, IAlgorithmDemo
    {
        // ─────────────────────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────────────────────

        [Header("월드 영역 (정사각형, -size/2 ~ +size/2)")]
        [SerializeField] private float worldSize = 20f;

        [Header("BVH 파라미터")]
        [Tooltip("leaf 한 개가 담을 수 있는 최대 객체 수. 작을수록 트리가 깊고 광선 가속도 ↑.")]
        [SerializeField] private int maxLeafSize = 2;

        [Header("객체 생성 (작은 AABB 들)")]
        [SerializeField] private int objectCount = 30;
        [SerializeField] private int randomSeed = 12345;
        [Tooltip("객체 AABB 의 반-크기 범위. min~max 사이에서 무작위.")]
        [SerializeField] private float objectHalfSizeMin = 0.25f;
        [SerializeField] private float objectHalfSizeMax = 0.7f;

        [Header("Phase 1 — Build 애니메이션")]
        [Tooltip("BFS 노드 등장 사이의 대기 시간(초). 0 으로 두면 즉시 완료.")]
        [SerializeField] private float buildStepDelay = 0.08f;
        [Tooltip("객체 cube 가 한 개씩 등장하는 시작 단계의 지연 (초). 0 이면 한 번에 등장.")]
        [SerializeField] private float objectSpawnDelay = 0.0f;

        [Header("Phase 2 — Ray Query")]
        [Tooltip("광선 발사 원점. 알고리즘 좌표계. 보통 화면 좌하단.")]
        [SerializeField] private Vector2 rayOrigin = new Vector2(-9f, -9f);
        [Tooltip("마우스가 origin 보다 가까울 때 광선 최소 길이 (보기용).")]
        [SerializeField] private float rayMinLength = 0.5f;

        [Header("시각 옵션")]
        [SerializeField] private float lineWidth = 0.04f;
        [SerializeField] private float rayLineWidth = 0.08f;
        [SerializeField] private float lineHeightOffset = 0.05f;
        [Tooltip("객체 cube 의 두께(Y 축). 거의 0 으로 둬서 평면처럼 보이게 함.")]
        [SerializeField] private float objectThickness = 0.05f;

        [Header("색상 — 외곽선")]
        [SerializeField] private Color colorNodeIdle    = new Color(0.85f, 0.85f, 0.85f);
        [SerializeField] private Color colorNodeVisited = new Color(1.00f, 0.60f, 0.10f);
        [SerializeField] private Color colorNodePruned  = new Color(0.30f, 0.30f, 0.30f, 0.6f);

        [Header("색상 — 객체")]
        [SerializeField] private Color colorObjectIdle    = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] private Color colorObjectVisited = new Color(1.00f, 0.65f, 0.20f);
        [SerializeField] private Color colorObjectHit     = new Color(1.00f, 0.40f, 0.80f);

        [Header("색상 — 광선 / 원점")]
        [SerializeField] private Color colorRay      = new Color(1.00f, 0.95f, 0.30f);
        [SerializeField] private Color colorOrigin   = new Color(0.30f, 0.60f, 1.00f);

        // ─────────────────────────────────────────────────────────────
        // Internal state
        // ─────────────────────────────────────────────────────────────

        private BVH<int> _tree;
        private readonly List<BVHObject<int>> _allObjects = new();
        private readonly List<GameObject> _objectMarkers = new();

        // 노드 → LineRenderer 매핑. Phase 1 에서 BFS 순서로 채워진다.
        private readonly Dictionary<BVHNode, LineRenderer> _nodeLines = new();

        // Phase 2 광선.
        private LineRenderer _rayLine;
        private GameObject _originMarker;

        private bool _phase2Active;

        // OnGUI 통계.
        private int _lastNodesVisited;
        private int _lastNodesPruned;
        private int _lastObjectsTested;
        private int _lastHitCount;

        // 라인 머티리얼 — Quadtree 와 같은 패턴 (vertex color 지원 셰이더).
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
            _allObjects.Clear();
            _objectMarkers.Clear();
            _nodeLines.Clear();
            _rayLine = null;
            _originMarker = null;
            _phase2Active = false;
            _lastNodesVisited = _lastNodesPruned = _lastObjectsTested = _lastHitCount = 0;

            // [2] 객체 생성 + 트리 빌드 (알고리즘 자체는 동기, 시각화만 코루틴).
            GenerateObjects();
            _tree = new BVH<int>(_allObjects, maxLeafSize);

            // [3] Phase 1 시작 — 노드 외곽선을 BFS 순서로 등장.
            StartCoroutine(Phase1Build());
        }

        // ─────────────────────────────────────────────────────────────
        // 객체 생성 — 무작위 위치 + 무작위 크기의 AABB
        // ─────────────────────────────────────────────────────────────

        private void GenerateObjects()
        {
            Random.InitState(randomSeed);
            float halfWorld = worldSize * 0.5f;

            for (int i = 0; i < objectCount; i++)
            {
                float hw = Random.Range(objectHalfSizeMin, objectHalfSizeMax);
                float hh = Random.Range(objectHalfSizeMin, objectHalfSizeMax);

                // 박스가 월드 경계 밖으로 삐져나가지 않도록 중심 좌표를 [-halfWorld+hw, halfWorld-hw] 로 클램프.
                float cx = Random.Range(-halfWorld + hw, halfWorld - hw);
                float cy = Random.Range(-halfWorld + hh, halfWorld - hh);

                var bounds = new AABB(cx, cy, hw, hh);
                _allObjects.Add(new BVHObject<int>(in bounds, i));

                // 객체 마커 (작은 평평한 cube). Phase 1 첫 단계에서 회색으로 등장.
                var marker = CreateObjectMarker(bounds);
                marker.SetActive(objectSpawnDelay <= 0f); // 0 이면 즉시 노출, 아니면 코루틴이 한 개씩 켬
                _objectMarkers.Add(marker);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 1: 트리 빌드 애니메이션 (BFS 순서)
        // ─────────────────────────────────────────────────────────────

        private IEnumerator Phase1Build()
        {
            // [A] 객체 마커가 즉시 노출 모드가 아니면 한 개씩 등장.
            if (objectSpawnDelay > 0f)
            {
                for (int i = 0; i < _objectMarkers.Count; i++)
                {
                    _objectMarkers[i].SetActive(true);
                    yield return new WaitForSeconds(objectSpawnDelay);
                }
            }

            // [B] 트리는 이미 동기로 빌드됨. BFS 순회하며 노드 외곽선을 한 개씩 등장.
            //     루트 → 자식 → 손자 순서라 "큰 박스가 점점 작은 박스들로 쪼개지는" 구조가 한눈에 들어온다.
            foreach (var node in _tree.WalkBFS())
            {
                EnsureNodeLine(node);
                if (buildStepDelay > 0f)
                {
                    yield return new WaitForSeconds(buildStepDelay);
                }
            }

            // [C] Phase 2 진입 준비 — 광선 + 원점 마커 생성.
            _originMarker = CreateOriginMarker(rayOrigin);
            _rayLine = CreateLineRenderer("Ray", colorRay, lineHeightOffset + 0.6f, width: rayLineWidth);
            _phase2Active = true;

            Debug.Log($"[BVH] Build 완료. {_tree.NodeCount} 노드, max depth {_tree.MaxDepth}, {_allObjects.Count} 객체. " +
                      "마우스를 움직여 광선 검사를 확인하세요.");
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 2: 마우스 광선 (Update 마다)
        // ─────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_phase2Active || _rayLine == null) return;

            // [1] 마우스 위치 → 알고리즘 좌표 (X, Y) = (worldX, worldZ).
            if (!TryGetMouseOnPlane(out Vector3 mouseWorld)) return;

            float mx = mouseWorld.x;
            float my = mouseWorld.z;

            // [2] 광선 방향 = mouse - origin (정규화 안 해도 알고리즘이 t 스케일만 영향받음 → 정규화).
            float dx = mx - rayOrigin.x;
            float dy = my - rayOrigin.y;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 1e-4f) return;
            float ndx = dx / len;
            float ndy = dy / len;
            float drawLen = Mathf.Max(len, rayMinLength);

            // [3] 광선 라인 갱신.
            UpdateRayLine(_rayLine, rayOrigin, ndx, ndy, drawLen, lineHeightOffset + 0.6f);

            // [4] BVH Raycast 실행 — 알고리즘 본체.
            var hits = _tree.Raycast(rayOrigin.x, rayOrigin.y, ndx, ndy, out var stats);
            _lastNodesVisited  = stats.NodesVisited;
            _lastNodesPruned   = stats.NodesPruned;
            _lastObjectsTested = stats.ObjectsTested;
            _lastHitCount      = hits.Count;

            // [5] 노드 외곽선 색칠 — 알고리즘과 *동일한* 검사를 다시 walk 해서 visited/pruned 판정.
            //     "주황 외곽선 = 알고리즘이 실제 들어간 노드" 가 정확히 일치한다.
            //     (별도로 visited 노드 집합을 만들어 알고리즘에서 받아오는 방법도 있지만, walk 가 더 직접적.)
            //     동시에 객체별 상태 (idle / visited / hit) 도 트리 walk 도중 결정한다.
            var hitObjectIndices = new HashSet<int>();
            for (int i = 0; i < hits.Count; i++) hitObjectIndices.Add(hits[i].ObjectIndex);

            // 모든 객체를 일단 idle (= pruned 가정) 로 초기화. visited / hit 는 walk 도중 덮어씀.
            for (int i = 0; i < _objectMarkers.Count; i++)
            {
                SetObjectColor(_objectMarkers[i], colorObjectIdle);
            }

            WalkAndColor(_tree.Root, rayOrigin.x, rayOrigin.y, ndx, ndy, hitObjectIndices);
        }

        /// <summary>
        /// 트리를 walk 하며 노드 외곽선과 객체 색을 알고리즘과 동일한 검사로 칠한다.
        /// 알고리즘의 RaycastRecursive 와 같은 가지치기 조건을 사용 → 시각화 ≡ 알고리즘 동작.
        /// </summary>
        private void WalkAndColor(BVHNode node, float ox, float oy, float dx, float dy, HashSet<int> hitObjectIndices)
        {
            if (node == null) return;

            var nodeHit = node.Bounds.Raycast(ox, oy, dx, dy);

            if (!nodeHit.Hit)
            {
                // 가지치기 — 이 노드 + 자손 모든 외곽선을 회색으로.
                SetSubtreeLineColor(node, colorNodePruned);
                // 객체는 이미 idle (회색) 로 초기화돼 있으므로 추가 작업 없음.
                return;
            }

            // 방문된 노드 — 외곽선 주황.
            SetNodeLineColor(node, colorNodeVisited);

            if (node.IsLeaf)
            {
                // leaf 의 객체들: hit 면 분홍, 아니면 visited (검사는 됐는데 miss) 주황.
                for (int i = 0; i < node.ObjectIndices.Count; i++)
                {
                    int objIdx = node.ObjectIndices[i];
                    Color c = hitObjectIndices.Contains(objIdx) ? colorObjectHit : colorObjectVisited;
                    SetObjectColor(_objectMarkers[objIdx], c);
                }
                return;
            }

            // internal — 양쪽 자식 재귀 (알고리즘과 같은 순서).
            WalkAndColor(node.Left,  ox, oy, dx, dy, hitObjectIndices);
            WalkAndColor(node.Right, ox, oy, dx, dy, hitObjectIndices);
        }

        /// <summary>이 노드와 모든 자손의 외곽선을 한 색으로 칠한다 (가지치기 시각화).</summary>
        private void SetSubtreeLineColor(BVHNode node, Color c)
        {
            if (node == null) return;
            SetNodeLineColor(node, c);
            if (!node.IsLeaf)
            {
                SetSubtreeLineColor(node.Left,  c);
                SetSubtreeLineColor(node.Right, c);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // OnGUI: Brute Force vs BVH 카운터
        // ─────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_phase2Active) return;

            const int W = 360, H = 130;
            GUI.Box(new Rect(10, 10, W, H), "");

            int totalObjects = _allObjects.Count;
            string text =
                "<b>BVH Ray Query</b>\n" +
                $"전체 객체: {totalObjects}    노드: {(_tree?.NodeCount ?? 0)}    max depth: {(_tree?.MaxDepth ?? 0)}\n" +
                $"방문 노드: {_lastNodesVisited}    스킵 노드: {_lastNodesPruned}\n" +
                $"검사한 객체: {_lastObjectsTested}    (Brute Force = {totalObjects})\n" +
                $"광선 hit 객체: {_lastHitCount}";

            var style = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13 };
            GUI.Label(new Rect(20, 18, W - 20, H - 20), text, style);
        }

        // ─────────────────────────────────────────────────────────────
        // 노드 외곽선 (LineRenderer) 헬퍼
        // ─────────────────────────────────────────────────────────────

        private void EnsureNodeLine(BVHNode node)
        {
            if (_nodeLines.ContainsKey(node)) return;

            // 깊을수록 살짝 위에 그려서 z-fighting 방지.
            float y = lineHeightOffset + node.Depth * 0.003f;
            var line = CreateLineRenderer($"Node_d{node.Depth}", colorNodeIdle, y);
            UpdateRectLine(line, node.Bounds, y);
            _nodeLines[node] = line;
        }

        private void SetNodeLineColor(BVHNode node, Color c)
        {
            if (_nodeLines.TryGetValue(node, out var line))
            {
                line.startColor = c;
                line.endColor   = c;
            }
        }

        private LineRenderer CreateLineRenderer(string name, Color color, float yOffset, float width = -1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.material      = LineMaterial;
            float w = width > 0f ? width : lineWidth;
            line.startWidth    = w;
            line.endWidth      = w;
            line.positionCount = 5; // 사각형 기본 (Ray 는 따로 2 로 갱신)
            line.startColor    = color;
            line.endColor      = color;
            _ = yOffset;
            return line;
        }

        private static void UpdateRectLine(LineRenderer line, in AABB b, float y)
        {
            // 알고리즘 좌표 (X, Y) → 월드 (worldX, worldZ).
            line.positionCount = 5;
            line.SetPosition(0, new Vector3(b.MinX, y, b.MinY));
            line.SetPosition(1, new Vector3(b.MaxX, y, b.MinY));
            line.SetPosition(2, new Vector3(b.MaxX, y, b.MaxY));
            line.SetPosition(3, new Vector3(b.MinX, y, b.MaxY));
            line.SetPosition(4, new Vector3(b.MinX, y, b.MinY));
        }

        private static void UpdateRayLine(LineRenderer line, Vector2 origin, float dx, float dy, float length, float y)
        {
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(origin.x, y, origin.y));
            line.SetPosition(1, new Vector3(origin.x + dx * length, y, origin.y + dy * length));
        }

        // ─────────────────────────────────────────────────────────────
        // 객체 마커 (작은 평평한 cube)
        // ─────────────────────────────────────────────────────────────

        private GameObject CreateObjectMarker(in AABB bounds)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.position = new Vector3(bounds.CenterX, 0f, bounds.CenterY);
            go.transform.localScale = new Vector3(bounds.HalfWidth * 2f, objectThickness, bounds.HalfHeight * 2f);

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            SetObjectColor(go, colorObjectIdle);
            return go;
        }

        private GameObject CreateOriginMarker(Vector2 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.position = new Vector3(pos.x, lineHeightOffset + 0.5f, pos.y);
            go.transform.localScale = Vector3.one * 0.4f;

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            SetObjectColor(go, colorOrigin);
            return go;
        }

        private static void SetObjectColor(GameObject go, Color c)
        {
            if (go == null) return;
            if (go.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = c;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 마우스 → 월드 (XZ 평면 Y=0). Quadtree / Spatial Hashing 과 같은 패턴.
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
