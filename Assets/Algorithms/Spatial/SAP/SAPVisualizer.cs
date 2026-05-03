using System.Collections;
using System.Collections.Generic;
using Algorithms.Common;
using Algorithms.Physics; // AABB — 객체 박스 (SAP 알고리즘과 공유)
using UnityEngine;

namespace Algorithms.Spatial
{
    /// <summary>
    /// =====================================================================
    ///  SAP (Sweep and Prune) 시각화 데모 (Unity 용)
    /// =====================================================================
    ///
    /// ▶ 이 데모는 두 단계로 진행된다 — 다른 Spatial 데모들과 다른 골격.
    ///
    ///   [Phase 1] Sweep 애니메이션 (정적, X 축, 한 번 실행)
    ///       · AABB 객체들이 회색으로 화면에 등장
    ///       · 화면 *하단* 에 endpoint strip — 각 객체의 X 축 lo (초록 ●) + hi (빨강 ●) 마커
    ///       · 파란 sweep line 이 좌→우로 한 endpoint 씩 이동 (delay)
    ///       · sweep 이 lo 만나면 → 객체 *active* (노랑 외곽선) + 기존 actives 와 candidate (노란 연결선)
    ///       · candidate 에 AABB.Overlaps → 통과면 confirmed (분홍 fill + 분홍 영구 연결선)
    ///       · sweep 이 hi 만나면 → active 해제
    ///       · OnGUI 우측 패널: 현재 active 리스트 + sweep 진행 상황
    ///
    ///   [Phase 2] Dynamic Mode (동적, 진짜 사용처 — temporal coherence)
    ///       · Phase 1 종료 후 잠깐 정지, 그 다음 객체들이 무작위 속도로 이동 (벽 반사)
    ///       · Endpoint strip 마커들이 X 축 따라 슬라이드
    ///       · 매 프레임: insertion sort 로 정렬 유지 → *swap 횟수 카운트*
    ///       · 현재 겹침 쌍 실시간 검출 → 분홍 연결선 (매 프레임 재생성)
    ///       · OnGUI: Frame swap 횟수 (= temporal coherence 의 시각적 증거),
    ///                현재 겹침 쌍, Brute Force 비교 (N²/2)
    ///
    /// ▶ AABB 데모와의 연결
    ///   sweep 의 candidate → confirmed 검사가 `AABB.Overlaps` (분리축 검사) 를 *그대로* 호출.
    ///   AABB 의 3 핵심 연산 (Contains / Overlaps / Raycast) 중 **Overlaps** 가 SAP 의 narrow-phase 부품.
    ///   BVH 가 AABB.Raycast 를 부품으로 쓴 것과 같은 "원자 → 분자" 패턴.
    ///
    /// ▶ 색상 의미
    ///     AABB 회색       : idle — 검사 안 됨 / active 아님
    ///     AABB 노랑       : 현재 active set 멤버 (sweep line 이 통과 중)
    ///     AABB 분홍       : 확정된 겹침에 포함된 객체
    ///     Endpoint 초록   : lo (sweep 이 만나면 active 추가)
    ///     Endpoint 빨강   : hi (sweep 이 만나면 active 제거)
    ///     Sweep line 파랑 : 현재 sweep 위치
    ///     연결선 노랑     : candidate 쌍 (검사 직전, 짧게 깜빡)
    ///     연결선 분홍     : confirmed 겹침 쌍 (영구 / Phase 2 에서는 매 프레임 재생성)
    ///
    /// ▶ 사용 방법
    ///   1) 빈 GameObject 를 만들고 이 컴포넌트를 붙인다.
    ///   2) Camera 위에서 내려다보는 각도. 권장: Position (0, 28, -3), Rotation (75, 0, 0),
    ///      Projection Orthographic, Size 14 (endpoint strip 까지 보이도록 다른 데모보다 약간 크게).
    ///   3) Play → Phase 1 자동 시작 → 종료 후 1~2 초 정지 → Phase 2 자동 진입.
    /// </summary>
    public class SAPVisualizer : MonoBehaviour, IAlgorithmDemo
    {
        // ─────────────────────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────────────────────

        [Header("월드 영역 (정사각형, -size/2 ~ +size/2)")]
        [SerializeField] private float worldSize = 20f;

        [Header("객체 생성")]
        [SerializeField] private int objectCount = 20;
        [SerializeField] private int randomSeed = 12345;
        [SerializeField] private float objectHalfSizeMin = 0.5f;
        [SerializeField] private float objectHalfSizeMax = 1.2f;
        [Tooltip("객체 cube 두께 (Y). 거의 0 으로 두어 평면처럼 보이게.")]
        [SerializeField] private float objectThickness = 0.1f;

        [Header("Phase 1 — Sweep 애니메이션")]
        [Tooltip("endpoint 한 개 처리 사이의 대기 시간(초). 0.2~0.5 권장.")]
        [SerializeField] private float sweepStepDelay = 0.25f;
        [Tooltip("candidate 노란 연결선이 보이는 시간 (확인 전).")]
        [SerializeField] private float candidateFlashDuration = 0.15f;
        [Tooltip("Phase 1 → Phase 2 전환 전 대기 시간 (초). 사용자가 결과 보도록.")]
        [SerializeField] private float interPhaseDelay = 1.5f;

        [Header("Phase 2 — Dynamic Mode")]
        [Tooltip("객체 최대 속도 (단위/초).")]
        [SerializeField] private float maxSpeed = 2.0f;
        [Tooltip("Update 주기 — 너무 빠르면 swap 이 폭증, 너무 느리면 보기 답답함.")]
        [SerializeField] private float dynamicTimeScale = 1.0f;

        [Header("시각 옵션")]
        [SerializeField] private float lineHeightOffset = 0.05f;
        [SerializeField] private float sweepLineWidth = 0.08f;
        [SerializeField] private float overlapLineWidth = 0.06f;
        [SerializeField] private float endpointMarkerSize = 0.35f;
        [Tooltip("endpoint strip 의 z 위치 (월드 하단 바깥쪽).")]
        [SerializeField] private float endpointStripZ = -11.5f;

        [Header("색상 — 객체")]
        [SerializeField] private Color colorObjectIdle      = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] private Color colorObjectActive    = new Color(1.00f, 0.85f, 0.20f);
        [SerializeField] private Color colorObjectOverlap   = new Color(1.00f, 0.40f, 0.80f);

        [Header("색상 — Endpoint / Sweep")]
        [SerializeField] private Color colorEndpointLo  = new Color(0.30f, 0.85f, 0.40f);
        [SerializeField] private Color colorEndpointHi  = new Color(0.95f, 0.30f, 0.30f);
        [SerializeField] private Color colorSweepLine   = new Color(0.30f, 0.60f, 1.00f);

        [Header("색상 — 연결선")]
        [SerializeField] private Color colorCandidateLine = new Color(1.00f, 0.95f, 0.30f);
        [SerializeField] private Color colorConfirmedLine = new Color(1.00f, 0.40f, 0.80f);

        // ─────────────────────────────────────────────────────────────
        // Internal state
        // ─────────────────────────────────────────────────────────────

        private SweepAndPrune<int> _sap;

        // 객체 정보 — Phase 2 motion 용으로 위치 / 속도 / 반-크기 분리.
        private readonly List<GameObject> _objectMarkers = new();
        private Vector2[] _positions;
        private Vector2[] _velocities;
        private Vector2[] _halfSizes;

        // Endpoint strip 마커 — 각 객체당 lo / hi 2 개. _endpointMarkers[i*2]=lo, [i*2+1]=hi.
        private readonly List<GameObject> _endpointMarkers = new();

        // Sweep line / overlap 연결선들.
        private LineRenderer _sweepLine;
        private readonly List<LineRenderer> _confirmedLines = new();   // Phase 1 누적 / Phase 2 매 프레임 재사용

        // Phase 1 active set 상태 (UI 표시용).
        private readonly HashSet<int> _currentActiveSet = new();
        private readonly HashSet<int> _phase1OverlapObjects = new();

        // Phase 단계 / 통계.
        private enum Phase { None, Phase1Sweeping, Phase1Done, Phase2Dynamic }
        private Phase _phase = Phase.None;

        private string _phase1Message = "";
        private float _phase1SweepX;
        private SAPSweepStats _phase1FinalStats;

        private SAPDynamicStats _phase2Stats;
        private List<SAPPair> _phase2CurrentPairs = new();

        // 라인 머티리얼 — Quadtree / BVH / K-d tree 와 같은 패턴.
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
            _objectMarkers.Clear();
            _endpointMarkers.Clear();
            _confirmedLines.Clear();
            _currentActiveSet.Clear();
            _phase1OverlapObjects.Clear();
            _phase2CurrentPairs.Clear();
            _sweepLine = null;
            _phase = Phase.None;
            _phase1Message = "";
            _phase1SweepX = -worldSize * 0.5f;
            _phase1FinalStats = default;
            _phase2Stats = default;

            // [2] 객체 + endpoint 마커 생성.
            GenerateObjects();
            CreateEndpointMarkers();
            UpdateAllEndpointMarkerPositions();

            // [3] Sweep line 생성 (Phase 1 에서 시각화).
            _sweepLine = CreateLineRenderer("SweepLine", colorSweepLine, sweepLineWidth);
            UpdateSweepLine(_phase1SweepX);

            // [4] SAP 인스턴스 빌드 (초기 정렬).
            var sapObjects = new List<SAPObject<int>>();
            for (int i = 0; i < _objectMarkers.Count; i++)
            {
                var bounds = ComputeBounds(i);
                sapObjects.Add(new SAPObject<int>(in bounds, i));
            }
            _sap = new SweepAndPrune<int>(sapObjects);

            // [5] Phase 1 시작.
            _phase = Phase.Phase1Sweeping;
            StartCoroutine(Phase1SweepCoroutine());
        }

        // ─────────────────────────────────────────────────────────────
        // 객체 / endpoint 마커 생성
        // ─────────────────────────────────────────────────────────────

        private void GenerateObjects()
        {
            Random.InitState(randomSeed);
            float halfWorld = worldSize * 0.5f;

            _positions  = new Vector2[objectCount];
            _velocities = new Vector2[objectCount];
            _halfSizes  = new Vector2[objectCount];

            for (int i = 0; i < objectCount; i++)
            {
                float hw = Random.Range(objectHalfSizeMin, objectHalfSizeMax);
                float hh = Random.Range(objectHalfSizeMin, objectHalfSizeMax);
                _halfSizes[i] = new Vector2(hw, hh);

                // 박스가 월드 경계 밖으로 삐져나가지 않도록 클램프.
                _positions[i] = new Vector2(
                    Random.Range(-halfWorld + hw, halfWorld - hw),
                    Random.Range(-halfWorld + hh, halfWorld - hh));

                // Phase 2 용 무작위 속도 (-maxSpeed ~ +maxSpeed).
                _velocities[i] = new Vector2(
                    Random.Range(-maxSpeed, maxSpeed),
                    Random.Range(-maxSpeed, maxSpeed));

                var marker = CreateObjectMarker(i, _positions[i], _halfSizes[i]);
                _objectMarkers.Add(marker);
            }
        }

        private GameObject CreateObjectMarker(int idx, Vector2 pos, Vector2 halfSize)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Obj_{idx}";
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.position = new Vector3(pos.x, 0f, pos.y);
            go.transform.localScale = new Vector3(halfSize.x * 2f, objectThickness, halfSize.y * 2f);

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            SetMarkerColor(go, colorObjectIdle);
            return go;
        }

        private void CreateEndpointMarkers()
        {
            for (int i = 0; i < objectCount; i++)
            {
                _endpointMarkers.Add(CreateEndpointMarker($"Lo_{i}", colorEndpointLo));
                _endpointMarkers.Add(CreateEndpointMarker($"Hi_{i}", colorEndpointHi));
            }
        }

        private GameObject CreateEndpointMarker(string name, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localScale = Vector3.one * endpointMarkerSize;

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            SetMarkerColor(go, color);
            return go;
        }

        /// <summary>
        /// 모든 endpoint 마커의 위치를 현재 객체 bounds 기준으로 갱신.
        /// _endpointMarkers[i*2] = lo (MinX), [i*2+1] = hi (MaxX).
        /// </summary>
        private void UpdateAllEndpointMarkerPositions()
        {
            for (int i = 0; i < objectCount; i++)
            {
                float minX = _positions[i].x - _halfSizes[i].x;
                float maxX = _positions[i].x + _halfSizes[i].x;

                _endpointMarkers[i * 2].transform.position     = new Vector3(minX, 0f, endpointStripZ);
                _endpointMarkers[i * 2 + 1].transform.position = new Vector3(maxX, 0f, endpointStripZ);
            }
        }

        private AABB ComputeBounds(int idx)
            => new AABB(_positions[idx].x, _positions[idx].y, _halfSizes[idx].x, _halfSizes[idx].y);

        // ─────────────────────────────────────────────────────────────
        // Phase 1: Sweep 애니메이션
        // ─────────────────────────────────────────────────────────────

        private IEnumerator Phase1SweepCoroutine()
        {
            _phase1Message = "Phase 1 시작 — sweep line 좌→우 이동";
            yield return new WaitForSeconds(0.5f);

            // _sap.SweepAllStepped 가 한 endpoint 씩 yield. 그 사이에 시각화 갱신 + delay.
            foreach (var step in _sap.SweepAllStepped())
            {
                // [1] sweep line 을 이 endpoint 위치로 이동.
                _phase1SweepX = step.Endpoint.Value;
                UpdateSweepLine(_phase1SweepX);

                int objIdx = step.Endpoint.ObjectIndex;
                if (step.Endpoint.IsLo)
                {
                    _phase1Message = $"lo[{objIdx}] 진입 — actives={step.ActiveBefore.Count}, " +
                                     $"candidates={step.CandidatePartners.Count}, " +
                                     $"confirmed={step.ConfirmedPartners.Count}";

                    // [2] candidate 노란 연결선 깜빡 (= broad-phase 후보 검사 시각화).
                    var candidateLines = new List<LineRenderer>();
                    foreach (int partner in step.CandidatePartners)
                    {
                        var line = CreateLineRenderer($"Candidate_{objIdx}_{partner}", colorCandidateLine, overlapLineWidth);
                        UpdateOverlapLine(line, objIdx, partner);
                        candidateLines.Add(line);
                    }
                    if (candidateLines.Count > 0)
                    {
                        yield return new WaitForSeconds(candidateFlashDuration);
                        // 후보 라인 제거 — confirmed 만 영구로 남음.
                        foreach (var l in candidateLines)
                        {
                            if (l != null) Destroy(l.gameObject);
                        }
                    }

                    // [3] confirmed 분홍 연결선 영구 추가.
                    foreach (int partner in step.ConfirmedPartners)
                    {
                        var line = CreateLineRenderer($"Confirmed_{objIdx}_{partner}", colorConfirmedLine, overlapLineWidth);
                        UpdateOverlapLine(line, objIdx, partner);
                        _confirmedLines.Add(line);

                        _phase1OverlapObjects.Add(objIdx);
                        _phase1OverlapObjects.Add(partner);
                    }
                }
                else
                {
                    _phase1Message = $"hi[{objIdx}] 진출 — actives={step.ActiveBefore.Count} → {step.ActiveAfter.Count}";
                }

                // [4] active set 상태 업데이트 + 객체 색칠 갱신.
                _currentActiveSet.Clear();
                foreach (int idx in step.ActiveAfter) _currentActiveSet.Add(idx);
                RecolorObjectsForPhase1();

                yield return new WaitForSeconds(sweepStepDelay);
            }

            // [5] Sweep 종료. 통계 다시 계산해서 저장 (Stepped 는 stats 안 줌).
            _sap.SweepAll(out _phase1FinalStats);
            _phase1Message = $"Phase 1 종료 — 총 {_confirmedLines.Count} 쌍 발견";
            _phase = Phase.Phase1Done;

            // [6] 잠시 정지 후 Phase 2 진입.
            yield return new WaitForSeconds(interPhaseDelay);
            EnterPhase2();
        }

        private void RecolorObjectsForPhase1()
        {
            for (int i = 0; i < _objectMarkers.Count; i++)
            {
                Color c;
                if (_phase1OverlapObjects.Contains(i)) c = colorObjectOverlap;
                else if (_currentActiveSet.Contains(i)) c = colorObjectActive;
                else c = colorObjectIdle;
                SetMarkerColor(_objectMarkers[i], c);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 2: Dynamic Mode 진입 + 매 프레임 갱신
        // ─────────────────────────────────────────────────────────────

        private void EnterPhase2()
        {
            // Phase 1 의 confirmed 라인 모두 제거 — Phase 2 에서 매 프레임 새로 그림.
            foreach (var l in _confirmedLines)
            {
                if (l != null) Destroy(l.gameObject);
            }
            _confirmedLines.Clear();

            // Sweep line 숨김 (Phase 2 에서는 전체 sweep 이 매 프레임 일어나서 sweep line 의미 없음).
            if (_sweepLine != null) _sweepLine.enabled = false;

            _phase = Phase.Phase2Dynamic;
            _phase1Message = "Phase 2 — 객체 이동 시작 (temporal coherence)";
        }

        private void Update()
        {
            if (_phase != Phase.Phase2Dynamic || _sap == null) return;

            float dt = Time.deltaTime * dynamicTimeScale;

            // [1] 위치 갱신 + 벽 반사.
            float halfWorld = worldSize * 0.5f;
            for (int i = 0; i < objectCount; i++)
            {
                _positions[i] += _velocities[i] * dt;

                // 벽 반사 — 객체가 벽 닿으면 속도 부호 반전 + 위치 클램프.
                if (_positions[i].x - _halfSizes[i].x < -halfWorld)
                {
                    _positions[i].x = -halfWorld + _halfSizes[i].x;
                    _velocities[i].x = -_velocities[i].x;
                }
                else if (_positions[i].x + _halfSizes[i].x > halfWorld)
                {
                    _positions[i].x = halfWorld - _halfSizes[i].x;
                    _velocities[i].x = -_velocities[i].x;
                }

                if (_positions[i].y - _halfSizes[i].y < -halfWorld)
                {
                    _positions[i].y = -halfWorld + _halfSizes[i].y;
                    _velocities[i].y = -_velocities[i].y;
                }
                else if (_positions[i].y + _halfSizes[i].y > halfWorld)
                {
                    _positions[i].y = halfWorld - _halfSizes[i].y;
                    _velocities[i].y = -_velocities[i].y;
                }

                // 객체 마커 위치 갱신.
                _objectMarkers[i].transform.position = new Vector3(_positions[i].x, 0f, _positions[i].y);
            }

            // [2] 새 bounds 리스트 만들어 SAP 에 전달 → insertion sort + sweep.
            var newBounds = new List<AABB>(objectCount);
            for (int i = 0; i < objectCount; i++) newBounds.Add(ComputeBounds(i));

            _phase2CurrentPairs = _sap.UpdateAndSweep(newBounds, out _phase2Stats);

            // [3] Endpoint 마커 위치 갱신 (객체 따라 슬라이드).
            UpdateAllEndpointMarkerPositions();

            // [4] 객체 색칠 — overlap 에 든 객체만 분홍.
            var overlapSet = new HashSet<int>();
            foreach (var p in _phase2CurrentPairs) { overlapSet.Add(p.A); overlapSet.Add(p.B); }
            for (int i = 0; i < _objectMarkers.Count; i++)
            {
                SetMarkerColor(_objectMarkers[i], overlapSet.Contains(i) ? colorObjectOverlap : colorObjectIdle);
            }

            // [5] Overlap 연결선 — 매 프레임 재생성 (개수가 동적이라 풀링 대신 단순 재생성).
            //     30 객체에서 보통 0~10 쌍이라 비용 무시 가능.
            foreach (var l in _confirmedLines)
            {
                if (l != null) Destroy(l.gameObject);
            }
            _confirmedLines.Clear();
            foreach (var p in _phase2CurrentPairs)
            {
                var line = CreateLineRenderer($"Overlap_{p.A}_{p.B}", colorConfirmedLine, overlapLineWidth);
                UpdateOverlapLine(line, p.A, p.B);
                _confirmedLines.Add(line);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // OnGUI: 통계 + active 리스트
        // ─────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            const int W = 380, H = 170;
            GUI.Box(new Rect(10, 10, W, H), "");

            int totalPairs = objectCount * (objectCount - 1) / 2;
            string text;

            if (_phase == Phase.Phase2Dynamic)
            {
                text = $"<b>SAP Dynamic Mode</b>\n" +
                       $"객체: {objectCount}    Endpoints: {objectCount * 2}\n" +
                       $"Insertion sort swaps (이번 프레임): <b>{_phase2Stats.SwapsThisFrame}</b>\n" +
                       $"  → Full re-sort 비용 = O(N log N) ≈ {(int)(objectCount * 2 * Mathf.Log(objectCount * 2 + 1, 2f))}\n" +
                       $"현재 겹치는 쌍: <b>{_phase2Stats.ActiveOverlaps}</b>\n" +
                       $"Brute Force 비교: N(N-1)/2 = {totalPairs} 쌍 검사\n" +
                       $"<i>※ 객체가 조금 움직이면 swap 이 몇 회 안 됨 = temporal coherence</i>";
            }
            else if (_phase == Phase.Phase1Sweeping || _phase == Phase.Phase1Done)
            {
                text = $"<b>SAP Sweep (Phase 1, X 축)</b>\n" +
                       $"{_phase1Message}\n" +
                       $"현재 sweep X: {_phase1SweepX:F2}\n" +
                       $"현재 active set ({_currentActiveSet.Count}): {ActiveSetToString()}\n" +
                       $"확정 겹침 쌍: {_confirmedLines.Count}\n" +
                       $"Brute Force 비교: N(N-1)/2 = {totalPairs} 쌍 검사";
            }
            else
            {
                text = "<b>SAP</b>\n준비 중...";
            }

            var style = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 };
            GUI.Label(new Rect(20, 18, W - 20, H - 20), text, style);
        }

        private string ActiveSetToString()
        {
            if (_currentActiveSet.Count == 0) return "(empty)";
            var sb = new System.Text.StringBuilder();
            int n = 0;
            foreach (int idx in _currentActiveSet)
            {
                if (n > 0) sb.Append(", ");
                sb.Append(idx);
                if (++n >= 10) { sb.Append(", ..."); break; }
            }
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────
        // LineRenderer 헬퍼
        // ─────────────────────────────────────────────────────────────

        private LineRenderer CreateLineRenderer(string name, Color color, float width)
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
            return line;
        }

        private void UpdateSweepLine(float x)
        {
            if (_sweepLine == null) return;
            float halfWorld = worldSize * 0.5f;
            // sweep line 은 endpoint strip 까지 닿도록 약간 더 길게.
            _sweepLine.SetPosition(0, new Vector3(x, lineHeightOffset + 0.5f, -halfWorld - 1f));
            _sweepLine.SetPosition(1, new Vector3(x, lineHeightOffset + 0.5f, +halfWorld + 0.5f));
        }

        private void UpdateOverlapLine(LineRenderer line, int idxA, int idxB)
        {
            line.SetPosition(0, new Vector3(_positions[idxA].x, lineHeightOffset + 0.4f, _positions[idxA].y));
            line.SetPosition(1, new Vector3(_positions[idxB].x, lineHeightOffset + 0.4f, _positions[idxB].y));
        }

        private static void SetMarkerColor(GameObject go, Color c)
        {
            if (go == null) return;
            if (go.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = c;
            }
        }
    }
}
