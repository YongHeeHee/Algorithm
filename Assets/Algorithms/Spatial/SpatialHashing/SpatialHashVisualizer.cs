using System.Collections;
using System.Collections.Generic;
using Algorithms.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Algorithms.Spatial
{
    /// <summary>
    /// =====================================================================
    ///  Spatial Hashing 시각화 데모 (Unity 용)
    /// =====================================================================
    ///
    /// ▶ 이 데모는 두 단계로 진행된다.
    ///
    ///   [Phase 1] 삽입 애니메이션 — 한 개씩 코루틴으로 점을 삽입
    ///       · 새 점은 노란 플래시 → 회색으로 안정
    ///       · 점이 들어간 셀 라인이 잠깐 노랗게 깜빡 (= 그 셀에 등록되었음을 표시)
    ///       · Quadtree 와 달리 *분할 자체가 없다* — 격자가 처음부터 고정
    ///
    ///   [Phase 2] 쿼리 모드 — 마우스를 따라다니는 빨간 사각 영역
    ///       · 쿼리 영역이 *덮는* 셀의 라인만 노란색 (= broad-phase 후보)
    ///       · 그 셀 안의 점 중에서:
    ///           - 쿼리 영역 *안* (narrow-phase 통과) → **분홍 (result)**
    ///           - 쿼리 영역 *밖* (narrow-phase 탈락) → **주황 (candidate)**
    ///       · 그 외 점들 (덮이지 않은 셀) → 회색 그대로
    ///       · 화면 좌상단 OnGUI 에 broad/narrow 카운터
    ///
    /// ▶ 색상 의미
    ///     라인 회색  : 미방문 셀 (또는 쿼리와 교차하지 않는 = 통째로 스킵)
    ///     라인 노랑  : 방문 셀 (쿼리가 덮음) — broad-phase 후보 셀
    ///     라인 빨강  : 마우스 쿼리 영역
    ///     점 회색    : 일반 객체 (덮이지 않은 셀에 있음)
    ///     점 노랑    : 방금 삽입됨 (Phase 1 의 플래시)
    ///     점 주황    : 후보 (broad 통과, narrow 탈락) — 셀은 덮였지만 정밀 검사에서 제외
    ///     점 분홍    : 결과 (narrow 통과)
    ///
    /// ▶ Quadtree 데모와의 차이
    ///   - 격자가 *고정* — 분할 애니메이션이 없다. 대신 셀 크기를 인스펙터에서 직접 조정.
    ///   - 점 색상이 *3 종* (Quadtree 는 2 종). 주황(candidate) 이 broad↔narrow 분리를 보여준다.
    ///   - OnGUI 카운터는 "덮은 셀 / 검사한 점 / 결과" 3 단계 — 가속이 두 단계로 일어남이 드러난다.
    /// </summary>
    public class SpatialHashVisualizer : MonoBehaviour, IAlgorithmDemo
    {
        // ─────────────────────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────────────────────

        [Header("월드 영역 (정사각형, -size/2 ~ +size/2)")]
        [SerializeField] private float worldSize = 20f;

        [Header("Spatial Hashing 파라미터")]
        [Tooltip("격자 셀 한 변의 길이. 이 알고리즘의 *유일하지만 결정적인* 튜닝 포인트.")]
        [SerializeField] private float cellSize = 2.0f;

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
        [SerializeField] private Color colorCellIdle      = new Color(0.45f, 0.45f, 0.45f);
        [SerializeField] private Color colorCellCovered   = new Color(1.00f, 0.90f, 0.20f);
        [SerializeField] private Color colorQueryArea     = new Color(1.00f, 0.20f, 0.20f);
        [SerializeField] private Color colorPointIdle     = new Color(0.70f, 0.70f, 0.70f);
        [SerializeField] private Color colorPointFlash    = new Color(1.00f, 0.90f, 0.20f);
        [SerializeField] private Color colorPointCandidate = new Color(1.00f, 0.55f, 0.10f);
        [SerializeField] private Color colorPointResult   = new Color(1.00f, 0.40f, 0.80f);

        // ─────────────────────────────────────────────────────────────
        // Internal state
        // ─────────────────────────────────────────────────────────────

        private SpatialHash<int> _hash;

        // 점 데이터와 시각화 GameObject 를 같은 인덱스로 매핑.
        // SpatialHashPoint.Data 를 인덱스로 쓰므로 i 번째 점 ↔ i 번째 marker.
        private readonly List<SpatialHashPoint<int>> _allPoints = new();
        private readonly List<GameObject> _pointMarkers = new();

        // 셀 좌표 → LineRenderer 매핑. 월드 영역 안의 모든 셀을 미리 만들어 둔다.
        private readonly Dictionary<(int cellX, int cellY), LineRenderer> _cellLines = new();

        // Phase 2 쿼리 영역 라인 (마우스 따라다님).
        private LineRenderer _queryLine;

        private bool _phase2Active;

        // OnGUI 통계 표시용 (Update 에서 갱신).
        private int _lastCellsCovered;
        private int _lastCandidatesChecked;
        private int _lastResultCount;

        // 라인 머티리얼은 한 번만 만들어 모든 LineRenderer 가 공유.
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
            _cellLines.Clear();
            _queryLine = null;
            _phase2Active = false;
            _lastCellsCovered = _lastCandidatesChecked = _lastResultCount = 0;

            // [2] Spatial Hash 재생성 + 격자 라인 미리 그리기.
            _hash = new SpatialHash<int>(cellSize);
            BuildCellGrid();

            // [3] Phase 1 시작.
            StartCoroutine(Phase1Insert());
        }

        // ─────────────────────────────────────────────────────────────
        // 격자 라인 미리 그리기
        //   Spatial Hashing 은 격자가 *고정* 이라 처음부터 모든 셀을 그릴 수 있다.
        //   (Quadtree 는 분할이 일어날 때마다 새 라인이 추가됨 — 그 차이가 곧 두 알고리즘의 본질 차이)
        // ─────────────────────────────────────────────────────────────

        private void BuildCellGrid()
        {
            float halfWorld = worldSize * 0.5f;
            // 월드 좌표를 셀 좌표로 변환해 시각화할 셀 범위를 결정.
            int xMin = (int)Mathf.Floor(-halfWorld / cellSize);
            int xMax = (int)Mathf.Floor( halfWorld / cellSize);
            int yMin = (int)Mathf.Floor(-halfWorld / cellSize);
            int yMax = (int)Mathf.Floor( halfWorld / cellSize);

            for (int cx = xMin; cx <= xMax; cx++)
            {
                for (int cy = yMin; cy <= yMax; cy++)
                {
                    var line = CreateLineRenderer($"Cell_{cx}_{cy}", colorCellIdle);
                    UpdateCellLine(line, cx, cy, lineHeightOffset);
                    _cellLines[(cx, cy)] = line;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 1: 한 개씩 삽입 애니메이션
        // ─────────────────────────────────────────────────────────────

        private IEnumerator Phase1Insert()
        {
            Random.InitState(randomSeed);
            float halfWorld = worldSize * 0.5f;

            for (int i = 0; i < pointCount; i++)
            {
                // [1] 무작위 점 생성. Data = 인덱스.
                float x = Random.Range(-halfWorld, halfWorld);
                float y = Random.Range(-halfWorld, halfWorld);
                var point = new SpatialHashPoint<int>(x, y, i);

                // [2] Spatial Hash 에 등록.
                _hash.Insert(point);
                _allPoints.Add(point);

                // [3] 점 마커 + 노란 플래시.
                var marker = CreatePointMarker(x, y);
                _pointMarkers.Add(marker);
                StartCoroutine(FlashMarker(marker, colorPointFlash, colorPointIdle, insertFlashDuration));

                // [4] 점이 들어간 셀의 라인을 잠깐 노랗게 깜빡이게.
                int cx = (int)Mathf.Floor(x / cellSize);
                int cy = (int)Mathf.Floor(y / cellSize);
                if (_cellLines.TryGetValue((cx, cy), out var cellLine))
                {
                    StartCoroutine(FlashLine(cellLine, colorCellCovered, colorCellIdle, insertFlashDuration));
                }

                if (insertStepDelay > 0f)
                {
                    yield return new WaitForSeconds(insertStepDelay);
                }
            }

            // [5] Phase 1 완료 → Phase 2 진입.
            _queryLine = CreateLineRenderer("QueryArea", colorQueryArea);
            UpdateRectLine(_queryLine, 0f, 0f, queryHalfSize, queryHalfSize, lineHeightOffset + 0.5f);
            _phase2Active = true;

            Debug.Log($"[SpatialHash] 삽입 완료. 총 {_allPoints.Count} 점, {_hash.OccupiedCellCount} 셀 사용중. 마우스로 쿼리해보세요.");
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 2: 마우스 쿼리 (Update 마다)
        // ─────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_phase2Active || _queryLine == null) return;

            // [1] 마우스 위치를 XZ 평면(Y=0) 위 좌표로 변환.
            if (!TryGetMouseOnPlane(out Vector3 mouseWorld)) return;

            // [2] 쿼리 영역 갱신 (마우스 중심 정사각형).
            //     알고리즘의 (X, Y) 가 시각화의 (worldX, worldZ) 이므로
            //     SpatialHashBounds 의 두 번째 축은 mouseWorld.z.
            var queryBounds = new SpatialHashBounds(mouseWorld.x, mouseWorld.z, queryHalfSize, queryHalfSize);
            UpdateRectLine(_queryLine, queryBounds.CenterX, queryBounds.CenterY,
                           queryBounds.HalfWidth, queryBounds.HalfHeight, lineHeightOffset + 0.5f);

            // [3] 쿼리 실행 (통계 포함).
            var result = _hash.Query(in queryBounds, out var stats);
            _lastCellsCovered = stats.CellsCovered;
            _lastCandidatesChecked = stats.CandidatesChecked;
            _lastResultCount = result.Count;

            // [4] 쿼리가 덮는 셀 좌표 범위 계산 — 시각화도 같은 셀 범위를 노랗게.
            int xMin = (int)Mathf.Floor(queryBounds.MinX / cellSize);
            int xMax = (int)Mathf.Floor(queryBounds.MaxX / cellSize);
            int yMin = (int)Mathf.Floor(queryBounds.MinY / cellSize);
            int yMax = (int)Mathf.Floor(queryBounds.MaxY / cellSize);

            // [5] 셀 라인 색칠 — 쿼리가 덮는 셀만 노랑, 나머지는 회색.
            foreach (var kv in _cellLines)
            {
                var (cx, cy) = kv.Key;
                bool isCovered = cx >= xMin && cx <= xMax && cy >= yMin && cy <= yMax;
                SetLineColor(kv.Value, isCovered ? colorCellCovered : colorCellIdle);
            }

            // [6] 점 마커 색칠 — 일단 모두 회색.
            for (int i = 0; i < _pointMarkers.Count; i++)
            {
                SetMarkerColor(_pointMarkers[i], colorPointIdle);
            }

            // [7] 덮인 셀의 *모든* 점은 후보(주황) — broad-phase 통과.
            //     단, 쿼리 영역 *안* 의 점은 다음 단계에서 분홍으로 덮어쓴다.
            for (int cx = xMin; cx <= xMax; cx++)
            {
                for (int cy = yMin; cy <= yMax; cy++)
                {
                    if (!_hash.Cells.TryGetValue((cx, cy), out var list)) continue;
                    foreach (var p in list)
                    {
                        if (p.Data >= 0 && p.Data < _pointMarkers.Count)
                        {
                            SetMarkerColor(_pointMarkers[p.Data], colorPointCandidate);
                        }
                    }
                }
            }

            // [8] 결과(narrow-phase 통과) 점은 분홍으로 덮어쓰기.
            //     주황 → 분홍으로 색이 바뀌는 것이 곧 broad → narrow 단계.
            foreach (var p in result)
            {
                if (p.Data >= 0 && p.Data < _pointMarkers.Count)
                {
                    SetMarkerColor(_pointMarkers[p.Data], colorPointResult);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // OnGUI: broad ↔ narrow 카운터
        // ─────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_phase2Active) return;

            const int W = 360, H = 150;
            GUI.Box(new Rect(10, 10, W, H), "");

            int total = _allPoints.Count;
            string text =
                "<b>Spatial Hashing Range Query</b>\n" +
                $"전체 점: {total}    셀 크기: {cellSize:0.0}\n" +
                $"덮은 셀: {_lastCellsCovered}    (broad-phase)\n" +
                $"검사한 점: {_lastCandidatesChecked}    (Brute Force = {total})\n" +
                $"결과 점: {_lastResultCount}    (narrow-phase 통과)";

            var style = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13 };
            GUI.Label(new Rect(20, 18, W - 20, H - 20), text, style);
        }

        // ─────────────────────────────────────────────────────────────
        // LineRenderer 헬퍼
        // ─────────────────────────────────────────────────────────────

        private LineRenderer CreateLineRenderer(string name, Color color)
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
            return line;
        }

        /// <summary>
        /// 셀 좌표 (cx, cy) 가 차지하는 사각형을 LineRenderer 에 그린다.
        /// 셀의 월드 영역: [cx * cellSize, (cx+1) * cellSize] × [cy * cellSize, (cy+1) * cellSize]
        /// </summary>
        private void UpdateCellLine(LineRenderer line, int cx, int cy, float y)
        {
            float xMin = cx * cellSize;
            float xMax = (cx + 1) * cellSize;
            float zMin = cy * cellSize;
            float zMax = (cy + 1) * cellSize;

            line.SetPosition(0, new Vector3(xMin, y, zMin));
            line.SetPosition(1, new Vector3(xMax, y, zMin));
            line.SetPosition(2, new Vector3(xMax, y, zMax));
            line.SetPosition(3, new Vector3(xMin, y, zMax));
            line.SetPosition(4, new Vector3(xMin, y, zMin));
        }

        /// <summary>
        /// 중심 + 반-크기 형식 사각형을 LineRenderer 에 그린다 (쿼리 영역용).
        /// </summary>
        private static void UpdateRectLine(LineRenderer line, float cx, float cy, float hw, float hh, float y)
        {
            line.SetPosition(0, new Vector3(cx - hw, y, cy - hh));
            line.SetPosition(1, new Vector3(cx + hw, y, cy - hh));
            line.SetPosition(2, new Vector3(cx + hw, y, cy + hh));
            line.SetPosition(3, new Vector3(cx - hw, y, cy + hh));
            line.SetPosition(4, new Vector3(cx - hw, y, cy - hh));
        }

        private static void SetLineColor(LineRenderer line, Color c)
        {
            line.startColor = c;
            line.endColor   = c;
        }

        private IEnumerator FlashLine(LineRenderer line, Color flash, Color rest, float duration)
        {
            SetLineColor(line, flash);
            yield return new WaitForSeconds(duration);
            // Phase 2 가 시작된 뒤엔 Update 가 매 프레임 색을 다시 칠하므로 굳이 복귀 불필요.
            if (!_phase2Active) SetLineColor(line, rest);
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

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            SetMarkerColor(go, colorPointIdle);
            return go;
        }

        private static void SetMarkerColor(GameObject go, Color c)
        {
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
            if (!_phase2Active) SetMarkerColor(go, rest);
        }

        // ─────────────────────────────────────────────────────────────
        // 마우스 → 월드 (XZ 평면 Y=0)
        //   Quadtree Visualizer 와 동일 패턴 — 신규 Input System 사용.
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
