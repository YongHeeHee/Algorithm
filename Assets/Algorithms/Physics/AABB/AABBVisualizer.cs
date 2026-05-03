using System.Collections.Generic;
using Algorithms.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Algorithms.Physics
{
    /// <summary>
    /// =====================================================================
    ///  AABB 시각화 데모 (Unity 용) — 한 화면에 3 데모 가로 배치
    /// =====================================================================
    ///
    /// ▶ 데모 구성 (왼쪽 → 오른쪽)
    ///
    ///   [1. Contains]  점 ↔ 박스
    ///       · 정적 박스 + 마우스 따라다니는 점
    ///       · 점이 박스 안 → 박스 초록 / 점 분홍
    ///       · 워밍업 — Contains 검사가 단순 4 비교임을 확인
    ///
    ///   [2. Overlap]  박스 ↔ 박스
    ///       · 정적 박스 + 마우스 중심 작은 박스 (probe)
    ///       · 겹침 → 두 박스 모두 노랑
    ///       · 분리 축 (X 또는 Y) 표시 → SAT 의 \"분리 축\" 개념 미리보기
    ///
    ///   [3. Slab Raycast]  광선 ↔ 박스 (이게 메인)
    ///       · 정적 박스 + 고정 원점에서 마우스 방향으로 광선
    ///       · 4 개 t 값 (X 슬랩 진입/이탈, Y 슬랩 진입/이탈) 을 광선 위 *작은 점* 으로 표시
    ///       · 박스 진입 (= 두 슬랩 *모두* 안인 첫 시각) 을 *분홍 점* 으로 강조
    ///       · OnGUI 에 4 개 t 값 + 부등식 결과
    ///       · → \"각 슬랩 진입/이탈 구간의 *교집합* 이 박스 통과 구간\" 통찰이 시각으로 박힘
    ///
    /// ▶ 사용 방법
    ///   1) 빈 GameObject + 이 컴포넌트.
    ///   2) Camera Position (0, 25, 0) / Rotation (90, 0, 0) / Orthographic Size 11.
    ///   3) Play → 마우스 움직이며 세 데모 동시 관찰.
    ///
    /// ▶ 다른 시각화와의 차이
    ///   Quadtree / Spatial Hashing 은 *2 페이즈* (삽입 → 쿼리) 였지만 AABB 는 *연산이 자체* 라
    ///   삽입 단계가 없다. 단일 페이즈로 매 프레임 마우스 → 결과를 즉시 보여준다.
    /// </summary>
    public class AABBVisualizer : MonoBehaviour, IAlgorithmDemo
    {
        // ─────────────────────────────────────────────────────────────
        // Inspector — 레이아웃 (X 좌표가 화면 가로, Z 좌표가 화면 세로)
        // ─────────────────────────────────────────────────────────────

        [Header("데모 1 — Contains")]
        [SerializeField] private Vector2 demo1Center = new Vector2(-12f, 0f);
        [SerializeField] private float demo1BoxHalfSize = 3f;

        [Header("데모 2 — Overlap")]
        [SerializeField] private Vector2 demo2Center = new Vector2(0f, 0f);
        [SerializeField] private float demo2StaticHalfSize = 3f;
        [SerializeField] private float demo2ProbeHalfSize = 1.2f;

        [Header("데모 3 — Slab Raycast")]
        [SerializeField] private Vector2 demo3Center = new Vector2(12f, 0f);
        [SerializeField] private float demo3BoxHalfSize = 3f;
        [Tooltip("광선 발사 원점 (월드 좌표). 보통 박스에서 멀리.")]
        [SerializeField] private Vector2 demo3RayOrigin = new Vector2(8f, -7f);
        [Tooltip("광선의 시각화 길이 — origin 에서 이만큼 뻗는다.")]
        [SerializeField] private float demo3RayMaxLength = 18f;

        [Header("시각 옵션")]
        [SerializeField] private float lineWidth = 0.06f;
        [SerializeField] private float lineHeightOffset = 0.05f;
        [SerializeField] private float dotMarkerSize = 0.35f;

        [Header("색상")]
        [SerializeField] private Color colorBoxIdle      = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] private Color colorBoxContains  = new Color(0.20f, 0.85f, 0.30f);
        [SerializeField] private Color colorBoxOverlap   = new Color(1.00f, 0.90f, 0.20f);
        [SerializeField] private Color colorRayMiss      = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] private Color colorRayHit       = new Color(1.00f, 0.90f, 0.20f);
        [SerializeField] private Color colorOriginDot    = new Color(0.50f, 0.80f, 1.00f);
        [SerializeField] private Color colorPointIdle    = new Color(0.70f, 0.70f, 0.70f);
        [SerializeField] private Color colorPointResult  = new Color(1.00f, 0.40f, 0.80f);
        [SerializeField] private Color colorSlabXDot     = new Color(0.40f, 0.70f, 1.00f); // X 슬랩 = 파랑
        [SerializeField] private Color colorSlabYDot     = new Color(0.40f, 1.00f, 0.55f); // Y 슬랩 = 초록

        // ─────────────────────────────────────────────────────────────
        // Internal state
        // ─────────────────────────────────────────────────────────────

        // 정적 박스 + 라인.
        private AABB _box1, _box2Static, _box3;
        private LineRenderer _box1Line, _box2StaticLine, _box2ProbeLine, _box3Line;

        // Demo 1 점 마커.
        private GameObject _probePoint1;

        // Demo 3 광선 + 슬랩 점들 + hit 점.
        private LineRenderer _rayLine;
        private GameObject _originDot;
        private GameObject _slabDotXEnter, _slabDotXExit, _slabDotYEnter, _slabDotYExit;
        private GameObject _hitDotEnter, _hitDotExit;

        // OnGUI 표시용 캐시.
        private bool _demo1Inside;
        private Vector2 _demo1MousePos;
        private bool _demo2Overlap;
        private int _demo2SeparatingAxis = -1;
        private AABBRaycastResult _demo3Result;

        // 라인 머티리얼 공유.
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
            // [1] 자식 정리.
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            // [2] 박스 자료 + 라인 만들기.
            _box1       = new AABB(demo1Center.x, demo1Center.y, demo1BoxHalfSize, demo1BoxHalfSize);
            _box2Static = new AABB(demo2Center.x, demo2Center.y, demo2StaticHalfSize, demo2StaticHalfSize);
            _box3       = new AABB(demo3Center.x, demo3Center.y, demo3BoxHalfSize, demo3BoxHalfSize);

            _box1Line       = CreateLineRenderer("Box1_Static",  colorBoxIdle, closedRect: true);
            _box2StaticLine = CreateLineRenderer("Box2_Static",  colorBoxIdle, closedRect: true);
            _box2ProbeLine  = CreateLineRenderer("Box2_Probe",   colorBoxIdle, closedRect: true);
            _box3Line       = CreateLineRenderer("Box3_Static",  colorBoxIdle, closedRect: true);

            UpdateRectLine(_box1Line, _box1, lineHeightOffset);
            UpdateRectLine(_box2StaticLine, _box2Static, lineHeightOffset);
            UpdateRectLine(_box3Line, _box3, lineHeightOffset);
            // probe 박스는 매 프레임 갱신.

            // Demo 1 점 마커.
            _probePoint1 = CreateDot("Probe1", colorPointIdle);

            // Demo 3 광선 + 점들.
            _rayLine = CreateLineRenderer("Ray", colorRayMiss, closedRect: false);
            _originDot      = CreateDot("Origin", colorOriginDot);
            _originDot.transform.position = new Vector3(demo3RayOrigin.x, 0f, demo3RayOrigin.y);
            _slabDotXEnter = CreateDot("SlabX_Enter", colorSlabXDot);
            _slabDotXExit  = CreateDot("SlabX_Exit",  colorSlabXDot);
            _slabDotYEnter = CreateDot("SlabY_Enter", colorSlabYDot);
            _slabDotYExit  = CreateDot("SlabY_Exit",  colorSlabYDot);
            _hitDotEnter   = CreateDot("Hit_Enter",   colorPointResult);
            _hitDotExit    = CreateDot("Hit_Exit",    colorPointResult);
        }

        // ─────────────────────────────────────────────────────────────
        // Update — 매 프레임 세 데모 동시 갱신
        // ─────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!TryGetMouseOnPlane(out Vector3 mouseWorld)) return;
            // 알고리즘 좌표 (X, Y) 가 시각화의 (worldX, worldZ) 라 두 번째 축은 mouseWorld.z.
            Vector2 mouse = new Vector2(mouseWorld.x, mouseWorld.z);

            UpdateDemo1(mouse);
            UpdateDemo2(mouse);
            UpdateDemo3(mouse);
        }

        // ─────────────────────────────────────────────────────────────
        // Demo 1 — Contains
        // ─────────────────────────────────────────────────────────────

        private void UpdateDemo1(Vector2 mouse)
        {
            bool inside = _box1.Contains(mouse.x, mouse.y);

            // 점 마커 위치 = 마우스 위치.
            _probePoint1.transform.position = new Vector3(mouse.x, 0f, mouse.y);
            SetDotColor(_probePoint1, inside ? colorPointResult : colorPointIdle);

            // 박스 색.
            SetLineColor(_box1Line, inside ? colorBoxContains : colorBoxIdle);

            _demo1Inside = inside;
            _demo1MousePos = mouse;
        }

        // ─────────────────────────────────────────────────────────────
        // Demo 2 — Overlap
        // ─────────────────────────────────────────────────────────────

        private void UpdateDemo2(Vector2 mouse)
        {
            // probe 박스: 마우스 중심.
            var probeBox = new AABB(mouse.x, mouse.y, demo2ProbeHalfSize, demo2ProbeHalfSize);
            UpdateRectLine(_box2ProbeLine, probeBox, lineHeightOffset);

            bool overlap = _box2Static.Overlaps(in probeBox);
            int sepAxis = _box2Static.GetSeparatingAxis(in probeBox);

            Color c = overlap ? colorBoxOverlap : colorBoxIdle;
            SetLineColor(_box2StaticLine, c);
            SetLineColor(_box2ProbeLine,  c);

            _demo2Overlap = overlap;
            _demo2SeparatingAxis = sepAxis;
        }

        // ─────────────────────────────────────────────────────────────
        // Demo 3 — Slab Raycast
        // ─────────────────────────────────────────────────────────────

        private void UpdateDemo3(Vector2 mouse)
        {
            Vector2 origin = demo3RayOrigin;
            Vector2 toMouse = mouse - origin;
            float distToMouse = toMouse.magnitude;
            if (distToMouse < 1e-4f)
            {
                // 마우스가 origin 위 → 방향 정의 불가, 광선 시각화만 정리.
                _rayLine.SetPosition(0, new Vector3(origin.x, lineHeightOffset, origin.y));
                _rayLine.SetPosition(1, new Vector3(origin.x, lineHeightOffset, origin.y));
                return;
            }
            Vector2 dir = toMouse / distToMouse; // 정규화 → t 가 \"월드 거리\" 의미.

            // 정규화된 방향으로 raycast.
            var result = _box3.Raycast(origin.x, origin.y, dir.x, dir.y);

            // 광선 시각화 — origin 에서 dir 방향으로 max 길이.
            Vector2 rayEnd = origin + dir * demo3RayMaxLength;
            _rayLine.SetPosition(0, new Vector3(origin.x,  lineHeightOffset, origin.y));
            _rayLine.SetPosition(1, new Vector3(rayEnd.x,  lineHeightOffset, rayEnd.y));
            SetLineColor(_rayLine, result.Hit ? colorRayHit : colorRayMiss);

            // 박스 색.
            SetLineColor(_box3Line, result.Hit ? colorRayHit : colorBoxIdle);

            // 4 개 슬랩 점 위치 = origin + dir * t. t 가 음수 / 너무 큼 / 무한대 일 수 있으므로 clamp.
            PositionDotOnRay(_slabDotXEnter, origin, dir, result.TEnterX);
            PositionDotOnRay(_slabDotXExit,  origin, dir, result.TExitX);
            PositionDotOnRay(_slabDotYEnter, origin, dir, result.TEnterY);
            PositionDotOnRay(_slabDotYExit,  origin, dir, result.TExitY);

            // hit 분홍 점 — Hit 일 때만 표시.
            if (result.Hit)
            {
                _hitDotEnter.SetActive(true);
                _hitDotExit.SetActive(true);
                PositionDotOnRay(_hitDotEnter, origin, dir, result.TEnter);
                PositionDotOnRay(_hitDotExit,  origin, dir, result.TExit);
            }
            else
            {
                _hitDotEnter.SetActive(false);
                _hitDotExit.SetActive(false);
            }

            _demo3Result = result;
        }

        // ─────────────────────────────────────────────────────────────
        // OnGUI — 각 데모의 결과 텍스트
        // ─────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            const int W = 420, H = 230;
            GUI.Box(new Rect(10, 10, W, H), "");

            string sepStr =
                _demo2SeparatingAxis == 0 ? "X 축에서 분리" :
                _demo2SeparatingAxis == 1 ? "Y 축에서 분리" :
                                            "분리 축 없음 (겹침)";

            // 슬랩 t 값 — 무한대일 수 있어서 그대로 출력하지 않고 정리.
            string xRange = FormatTRange(_demo3Result.TEnterX, _demo3Result.TExitX);
            string yRange = FormatTRange(_demo3Result.TEnterY, _demo3Result.TExitY);
            string enterStr = float.IsFinite(_demo3Result.TEnter) ? _demo3Result.TEnter.ToString("0.00") : "∞";
            string exitStr  = float.IsFinite(_demo3Result.TExit)  ? _demo3Result.TExit.ToString("0.00")  : "∞";

            string text =
                "<b>AABB — 3 데모</b>\n\n" +
                "<b>1. Contains (점 ↔ 박스)</b>\n" +
                $"  마우스: ({_demo1MousePos.x:0.0}, {_demo1MousePos.y:0.0})    " +
                $"안에 있음: {(_demo1Inside ? "<color=#ee5599>YES</color>" : "no")}\n\n" +
                "<b>2. Overlap (박스 ↔ 박스)</b>\n" +
                $"  겹침: {(_demo2Overlap ? "<color=#ddcc11>YES</color>" : "no")}    " +
                $"분리: <color=#88aaff>{sepStr}</color>\n\n" +
                "<b>3. Slab Raycast (광선 ↔ 박스)</b>\n" +
                $"  X 슬랩: {xRange}    Y 슬랩: {yRange}\n" +
                $"  tEnter = max(...) = <b>{enterStr}</b>    tExit = min(...) = <b>{exitStr}</b>\n" +
                $"  HIT: {(_demo3Result.Hit ? "<color=#ddcc11>YES</color>" : "no")}    " +
                $"(tEnter ≤ tExit ∧ tExit ≥ 0)";

            var style = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13 };
            GUI.Label(new Rect(20, 18, W - 20, H - 20), text, style);
        }

        private static string FormatTRange(float a, float b)
        {
            string fmt(float v) => float.IsPositiveInfinity(v) ? "+∞" :
                                   float.IsNegativeInfinity(v) ? "-∞" :
                                                                 v.ToString("0.00");
            return $"[{fmt(a)}, {fmt(b)}]";
        }

        // ─────────────────────────────────────────────────────────────
        // 헬퍼
        // ─────────────────────────────────────────────────────────────

        private LineRenderer CreateLineRenderer(string name, Color color, bool closedRect)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.material      = LineMaterial;
            line.startWidth    = lineWidth;
            line.endWidth      = lineWidth;
            line.positionCount = closedRect ? 5 : 2;
            line.startColor    = color;
            line.endColor      = color;
            return line;
        }

        private static void UpdateRectLine(LineRenderer line, in AABB b, float y)
        {
            line.SetPosition(0, new Vector3(b.MinX, y, b.MinY));
            line.SetPosition(1, new Vector3(b.MaxX, y, b.MinY));
            line.SetPosition(2, new Vector3(b.MaxX, y, b.MaxY));
            line.SetPosition(3, new Vector3(b.MinX, y, b.MaxY));
            line.SetPosition(4, new Vector3(b.MinX, y, b.MinY));
        }

        private static void SetLineColor(LineRenderer line, Color c)
        {
            line.startColor = c;
            line.endColor   = c;
        }

        private GameObject CreateDot(string name, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localScale = Vector3.one * dotMarkerSize;

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            SetDotColor(go, color);
            return go;
        }

        private static void SetDotColor(GameObject go, Color c)
        {
            if (go == null) return;
            if (go.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = c;
            }
        }

        /// <summary>
        /// 광선 위 점 위치 = origin + dir * t. t 가 무한대면 보이지 않게 멀리 보내고 비활성화.
        /// </summary>
        private void PositionDotOnRay(GameObject dot, Vector2 origin, Vector2 dir, float t)
        {
            if (!float.IsFinite(t))
            {
                dot.SetActive(false);
                return;
            }
            // 광선 시각화 길이 밖이면 끝점에 클램핑 (안 보이게 하지 않고 *경계에* 두는 게 학습에 더 좋음).
            // 음수 t 도 표시 (광선 *뒤쪽* 의 슬랩 진입 — \"왜 hit 가 아닌지\" 가 보임).
            dot.SetActive(true);
            float clampedT = Mathf.Clamp(t, -demo3RayMaxLength, demo3RayMaxLength * 1.5f);
            Vector2 pos = origin + dir * clampedT;
            dot.transform.position = new Vector3(pos.x, 0f, pos.y);
        }

        // ─────────────────────────────────────────────────────────────
        // 마우스 → 월드 (XZ 평면 Y=0) — Quadtree / SpatialHash 와 동일 패턴
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
