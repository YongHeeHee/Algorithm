using System;
using System.Collections.Generic;
using Algorithms.Physics; // AABB — 객체 박스 + Overlaps (분리축 검사) 재사용

namespace Algorithms.Spatial
{
    // =====================================================================
    //  SAP 가 다루는 객체 한 개 (AABB + 임의 데이터)
    // =====================================================================
    //
    // ▶ BVH 의 BVHObject<T> 와 같은 패턴 — T 는 GameObject id, 콜라이더 인덱스 같은
    //   원본 데이터의 핸들. 알고리즘은 *박스 위치* 만 보고 동작한다.
    public readonly struct SAPObject<T>
    {
        public readonly AABB Bounds;
        public readonly T Data;

        public SAPObject(in AABB bounds, T data)
        {
            Bounds = bounds;
            Data = data;
        }
    }

    /// <summary>
    /// SAP 의 핵심 자료 — 한 객체의 한 축 끝점 (lo 또는 hi).
    ///
    /// ▶ 왜 lo / hi 를 *별도 항목* 으로 다루는가
    ///   N 개 객체에서 2N 개 끝점이 한 축의 *수직선* 위에 늘어선다.
    ///   이 2N 개를 정렬해 sweep 하면 "active 상태에 있는 객체쌍 = X 축 projection 이 겹치는 쌍" 이 자연스럽게 추출됨.
    ///   객체 자체를 정렬하는 게 아니라 *끝점* 을 정렬하는 게 SAP 의 트릭.
    /// </summary>
    public struct SAPEndpoint
    {
        /// <summary>이 끝점이 어느 객체 (인덱스) 의 것인지.</summary>
        public int ObjectIndex;

        /// <summary>true = lo (시작점), false = hi (끝점). sweep 동작이 이 플래그로 갈린다.</summary>
        public bool IsLo;

        /// <summary>이 끝점의 X 좌표 (정렬 키).</summary>
        public float Value;
    }

    /// <summary>
    /// 두 객체 인덱스의 *순서 없는* 쌍. (A, B) == (B, A) 로 다뤄지도록 항상 A &lt; B 보장.
    /// HashSet&lt;SAPPair&gt; 등에 직접 넣어 중복 제거에 사용.
    /// </summary>
    public readonly struct SAPPair : IEquatable<SAPPair>
    {
        public readonly int A;
        public readonly int B;

        public SAPPair(int a, int b)
        {
            if (a < b) { A = a; B = b; }
            else       { A = b; B = a; }
        }

        public bool Equals(SAPPair other) => A == other.A && B == other.B;
        public override bool Equals(object obj) => obj is SAPPair p && Equals(p);
        public override int GetHashCode() => unchecked((A * 397) ^ B);
    }

    /// <summary>
    /// 정적 sweep 의 통계 — 한 번의 SweepAll 동안 검사 / 통과 횟수.
    /// </summary>
    public struct SAPSweepStats
    {
        /// <summary>active set 에 새 객체가 진입할 때 *기존 actives 와 검사* 한 후보 쌍 수 (= broad-phase 비용).</summary>
        public int CandidatePairs;

        /// <summary>그 중 AABB.Overlaps 통과한 실제 겹침 쌍 수 (= narrow-phase 통과).</summary>
        public int ConfirmedPairs;

        /// <summary>sweep 도중 active set 의 최대 크기 — 객체 분포 밀집도 지표.</summary>
        public int MaxActiveSetSize;
    }

    /// <summary>
    /// 동적 모드의 프레임당 통계 — *temporal coherence* 의 시각적 증거.
    /// </summary>
    public struct SAPDynamicStats
    {
        /// <summary>이번 프레임 insertion sort 에서 발생한 swap 횟수.
        /// 객체가 *조금* 움직이면 정렬 순서가 *거의* 그대로라 swap 이 매우 적게 발생 (= temporal coherence).
        /// 매 프레임 *full re-sort* 가 O(N log N) 인 것과 대비되는 SAP 의 본질.</summary>
        public int SwapsThisFrame;

        /// <summary>이번 프레임 현재 겹치는 쌍 수.</summary>
        public int ActiveOverlaps;
    }

    /// <summary>
    /// Phase 1 Visualizer 가 sweep 한 endpoint 씩 진행하며 한 단계의 모든 중간 상태를 받기 위한 record.
    /// </summary>
    public class SAPSweepStep<T>
    {
        public int EndpointIndex;
        public SAPEndpoint Endpoint;

        /// <summary>이 step *처리 직전* 의 active set (시각화에서 "현재 active" 색칠에 사용).</summary>
        public HashSet<int> ActiveBefore;

        /// <summary>이 step *처리 직후* 의 active set.</summary>
        public HashSet<int> ActiveAfter;

        /// <summary>lo 만났을 때 검사한 후보 파트너들 (= 노란 candidate 연결선 표시용). hi 면 비어 있음.</summary>
        public List<int> CandidatePartners;

        /// <summary>그 중 AABB.Overlaps 통과한 confirmed 파트너들 (= 분홍 영구 연결선 표시용).</summary>
        public List<int> ConfirmedPartners;
    }

    /// <summary>
    /// =====================================================================
    ///  Sweep and Prune (SAP) — 정렬 끝점 기반 broad-phase 충돌 검출
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// SAP 는 N 개 AABB 의 *모든 겹치는 쌍* 을 평균 O(N + k) 로 찾는 broad-phase 알고리즘.
    /// 핵심 아이디어:
    ///   ① 각 객체가 한 축 (X) 에 *2 개 끝점* (lo, hi) 을 만든다. 총 2N 개.
    ///   ② 이 2N 끝점을 X 좌표로 정렬.
    ///   ③ sweep line 을 좌→우로 이동시키며 "현재 active 한 객체 집합" 을 유지:
    ///        · lo 만나면 → active 집합에 추가. 기존 모든 actives 와 *X projection 겹침* 후보쌍.
    ///        · hi 만나면 → active 집합에서 제거.
    ///   ④ 후보쌍에 대해 full AABB.Overlaps 로 narrow-phase 확정.
    ///
    /// ▶ 다른 Spatial 4 형제와의 결정적 차이 — *트리가 아닌 정렬*
    ///   - Quadtree / BVH / K-d tree : 모두 *재귀 트리*
    ///   - Spatial Hashing            : Dictionary 격자
    ///   - **SAP**                    : *축별 정렬된 끝점 리스트* — 트리도 격자도 아님
    ///
    /// ▶ SAP 의 진짜 정체성 — *Temporal Coherence* (시간적 일관성)
    ///   객체가 매 프레임 *조금* 움직이면 정렬 순서가 *거의* 그대로다.
    ///   그래서 *insertion sort* 로 정렬을 유지하면 평균 O(N) 으로 끝난다 (full re-sort O(N log N) 대신).
    ///   매 프레임 swap 횟수가 객체 30 개여도 보통 0 ~ 3 회.
    ///   → **Bullet / PhysX / 초기 Box2D 의 broad-phase 표준이었던 이유**.
    ///   K-d tree / BVH 가 한 프레임 build 후 정적 쿼리 위주인 반면, SAP 는 *매 프레임 갱신* 이 본업.
    ///
    /// ▶ AABB 와의 관계 — *AABB.Overlaps 의 sorted-list 활용*
    ///   BVH 가 `AABB.Raycast` 를 트리 노드마다 부품으로 호출했듯,
    ///   SAP 는 `AABB.Overlaps` (분리축 검사의 단순형) 를 *후보쌍마다* 호출.
    ///   AABB 3 핵심 연산 (Contains / Overlaps / Raycast) 중 **Overlaps** 가 SAP 의 narrow-phase 부품.
    ///   "원자 (AABB) → 분자 (자료구조)" 패턴의 또 다른 사례.
    ///
    /// 2. 동작 흐름
    /// ---------------------------------------------------------------------
    ///   ▷ Build (초기 정렬, O(N log N))
    ///     ① N 개 객체에서 2N 개 끝점 생성 (각 객체당 lo, hi).
    ///     ② Array.Sort (또는 quicksort) 로 X 좌표 기준 정렬.
    ///
    ///   ▷ SweepAll (정적 모드 — 한 번에 모든 겹침 쌍, O(N + k))
    ///     ① active = empty set.
    ///     ② 정렬된 끝점을 좌→우 순회:
    ///        · lo 만나면 → 기존 actives 모두와 (this, active) 후보쌍 검사 (AABB.Overlaps).
    ///                      통과한 쌍을 결과에 추가. 그 후 active 에 this 추가.
    ///        · hi 만나면 → active 에서 this 제거.
    ///
    ///   ▷ UpdateAndSweep (동적 모드 — temporal coherence 의 본체)
    ///     ① 객체 위치 변경 후 호출 → 모든 endpoint 의 Value 갱신.
    ///     ② Insertion sort 로 정렬 유지 — *조금만 움직이면 swap 거의 없음*.
    ///     ③ SweepAll 같은 sweep 으로 현재 겹침 쌍 재검출.
    ///
    /// 3. 시간 / 공간 복잡도  (N = 객체 수, k = 겹치는 쌍 수)
    /// ---------------------------------------------------------------------
    ///   - Build (초기 정렬)   : O(N log N)
    ///   - SweepAll (정적)     : O(N + C) — C = candidate 쌍 수, k ≤ C ≤ N²/2
    ///   - UpdateAndSweep (동적)
    ///       · Insertion sort  : 평균 O(N) (temporal coherence), 최악 O(N²)
    ///       · Sweep            : O(N + C)
    ///       · 합계 평균        : ≈ O(N + k)
    ///   - 공간                 : O(N) — 객체 + 2N 끝점
    ///
    ///   ※ Brute Force vs SAP (동적):
    ///        N=100   매 프레임 N²/2 = 5,000 vs SAP ≈ 100 + few swaps   → 약 50 배
    ///        N=1000  매 프레임 N²/2 = 500K vs SAP ≈ 1000 + few swaps   → 약 500 배
    ///       객체 분포가 한쪽으로 *몰리면* (= MaxActiveSet 폭발) 가속비 급락 — SAP 의 약점.
    ///
    /// 4. 게임에서의 사용처
    /// ---------------------------------------------------------------------
    ///   - **물리 엔진 broad-phase (전통 표준)** : Bullet / PhysX / 초기 Box2D 의 충돌 후보 추리기
    ///       (모던 PhysX 는 Dynamic AABBTree 로 이전, 하지만 SAP 는 여전히 Bullet 의 기본 옵션)
    ///   - **2D 게임 엔진 충돌** : 셀로폰 같은 액션 게임에서 수십 ~ 수백 객체 충돌
    ///   - **GUI 레이아웃 충돌 검사** : 윈도우 / 토스트 / 툴팁 겹침 방지
    ///   - **Frustum culling 보조** : 카메라 절두체 ↔ 객체 AABB 후보 추리기
    ///   - **충돌 검사 결과 캐싱** : "지난 프레임에 겹쳤던 쌍" 추적이 자연스러움
    ///
    /// 5. 사용 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   - **SAPEndpoint[]** (배열, 길이 2N)
    ///       : 정렬이 빈번해 *연속 메모리 + 빠른 swap* 이 중요. List 보다 array 가 cache friendly.
    ///         struct 라 박싱도 없음.
    ///   - **HashSet&lt;int&gt;** (active set)
    ///       : Add / Remove / iterate 가 모두 평균 O(1). sweep 도중 매 endpoint 마다 호출되므로
    ///         성능 민감. Sorted 자료구조는 불필요 — sweep 순서로 들어오므로.
    ///   - **AABB** (Algorithms.Physics) 재사용
    ///       : 후보쌍의 narrow-phase 검사에 AABB.Overlaps 를 *그대로* 호출. BVH 의 AABB.Raycast
    ///         재사용과 같은 카테고리 횡단 의존. "원자 → 분자" 의 또 다른 사례.
    ///   - **별도 트리 / 격자 *없음***
    ///       : 정렬된 배열 + active set 만으로 작동. SAP 가 다른 Spatial 알고리즘과 가장 다른 점.
    ///   - **Insertion sort** (동적 모드)
    ///       : 일반 sort (Array.Sort = QuickSort) 는 거의 정렬된 배열에 *오히려 비효율적*.
    ///         Insertion sort 는 정렬된 배열에 평균 O(N) — temporal coherence 와 완벽히 맞아떨어짐.
    /// </summary>
    public class SweepAndPrune<T>
    {
        // ─────────────────────────────────────────────────────────────
        // 인스턴스 상태
        // ─────────────────────────────────────────────────────────────

        public IReadOnlyList<SAPObject<T>> Objects => _objects;
        public IReadOnlyList<SAPEndpoint> Endpoints => _endpoints;
        public int ObjectCount => _objects.Length;

        private readonly SAPObject<T>[] _objects;     // N
        private readonly SAPEndpoint[] _endpoints;    // 2N (각 객체당 lo, hi)

        // ─────────────────────────────────────────────────────────────
        // 생성자 — 초기 정렬 (Build)
        // ─────────────────────────────────────────────────────────────

        public SweepAndPrune(IList<SAPObject<T>> objects)
        {
            _objects = new SAPObject<T>[objects.Count];
            for (int i = 0; i < objects.Count; i++) _objects[i] = objects[i];

            // [1] N 개 객체에서 2N 개 끝점 생성.
            //     각 객체의 lo (MinX) 와 hi (MaxX) 가 별도 항목.
            _endpoints = new SAPEndpoint[_objects.Length * 2];
            for (int i = 0; i < _objects.Length; i++)
            {
                _endpoints[i * 2]     = new SAPEndpoint { ObjectIndex = i, IsLo = true,  Value = _objects[i].Bounds.MinX };
                _endpoints[i * 2 + 1] = new SAPEndpoint { ObjectIndex = i, IsLo = false, Value = _objects[i].Bounds.MaxX };
            }

            // [2] 초기 정렬은 일반 sort 로 충분 — O(N log N).
            //     Array.Sort 는 IntroSort (QuickSort + HeapSort + InsertionSort 하이브리드).
            Array.Sort(_endpoints, (a, b) => a.Value.CompareTo(b.Value));
        }

        // ─────────────────────────────────────────────────────────────
        // SweepAll — 정적 모드 (한 번에 모든 겹침 쌍)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 정렬된 끝점을 좌→우 sweep 하며 active set 을 유지, 모든 겹침 쌍을 반환.
        /// 한 번 호출 = 한 번의 sweep. 동적 모드에서도 매 프레임 이 함수가 (insertion sort 후) 호출된다.
        /// </summary>
        public List<SAPPair> SweepAll(out SAPSweepStats stats)
        {
            var pairs = new List<SAPPair>();
            stats = default;
            var active = new HashSet<int>();

            for (int i = 0; i < _endpoints.Length; i++)
            {
                var ep = _endpoints[i];

                if (ep.IsLo)
                {
                    // [1] lo 끝점 — 기존 actives 모두와 후보쌍.
                    //     active 집합에 이미 있는 객체들 = 이 객체의 X projection 과 *겹치는* 객체들.
                    //     (왜? hi 에서 제거되니까 active 인 동안은 X projection 이 살아있음.)
                    foreach (int activeIdx in active)
                    {
                        stats.CandidatePairs++;
                        if (_objects[activeIdx].Bounds.Overlaps(_objects[ep.ObjectIndex].Bounds))
                        {
                            // X projection 뿐 아니라 full AABB 도 겹침 → 진짜 충돌 후보쌍.
                            pairs.Add(new SAPPair(activeIdx, ep.ObjectIndex));
                            stats.ConfirmedPairs++;
                        }
                    }

                    // [2] 후보쌍 검사 후 active 에 추가. 순서 중요 — 추가 전에 검사해야 자기 자신과 안 비교됨.
                    active.Add(ep.ObjectIndex);
                    if (active.Count > stats.MaxActiveSetSize) stats.MaxActiveSetSize = active.Count;
                }
                else
                {
                    // [3] hi 끝점 — active 에서 제거. 더 오른쪽에 있는 객체들과는 X projection 안 겹침.
                    active.Remove(ep.ObjectIndex);
                }
            }

            return pairs;
        }

        // ─────────────────────────────────────────────────────────────
        // SweepAllStepped — Visualizer 용 step-by-step iterator
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 한 endpoint 처리마다 yield 하며 모든 중간 상태 (active set, candidates, confirmed) 노출.
        /// Visualizer 가 yield 사이에 delay 를 넣어 sweep 애니메이션을 만든다.
        /// 알고리즘 본체 (SweepAll) 와 같은 로직 — 단지 중간 상태를 외부로 노출할 뿐.
        /// </summary>
        public IEnumerable<SAPSweepStep<T>> SweepAllStepped()
        {
            var active = new HashSet<int>();

            for (int i = 0; i < _endpoints.Length; i++)
            {
                var ep = _endpoints[i];
                var step = new SAPSweepStep<T>
                {
                    EndpointIndex = i,
                    Endpoint = ep,
                    ActiveBefore = new HashSet<int>(active),
                    CandidatePartners = new List<int>(),
                    ConfirmedPartners = new List<int>(),
                };

                if (ep.IsLo)
                {
                    foreach (int activeIdx in active)
                    {
                        step.CandidatePartners.Add(activeIdx);
                        if (_objects[activeIdx].Bounds.Overlaps(_objects[ep.ObjectIndex].Bounds))
                        {
                            step.ConfirmedPartners.Add(activeIdx);
                        }
                    }
                    active.Add(ep.ObjectIndex);
                }
                else
                {
                    active.Remove(ep.ObjectIndex);
                }

                step.ActiveAfter = new HashSet<int>(active);
                yield return step;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // UpdateAndSweep — 동적 모드 (temporal coherence 의 본체)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 매 프레임 호출. 새 bounds 를 받아:
        ///   ① 모든 endpoint 의 Value 갱신
        ///   ② Insertion sort 로 정렬 유지 (객체가 조금 움직였으면 거의 swap 없음)
        ///   ③ SweepAll 같은 sweep 으로 현재 겹침 쌍 재검출
        ///
        /// SAP 의 *진짜 강점* 이 이 함수에 있다 — 매 프레임 full re-sort O(N log N) 대신 *insertion sort 평균 O(N)*.
        /// </summary>
        public List<SAPPair> UpdateAndSweep(IList<AABB> newBounds, out SAPDynamicStats stats)
        {
            stats = default;

            // [1] 객체 bounds 갱신.
            //     SAPObject 는 readonly struct 라 새 인스턴스 할당 (Bounds 만 바뀌고 Data 는 유지).
            for (int i = 0; i < newBounds.Count && i < _objects.Length; i++)
            {
                _objects[i] = new SAPObject<T>(newBounds[i], _objects[i].Data);
            }

            // [2] Endpoint 의 Value 만 갱신 (위치 / 순서는 이전 프레임 그대로 — insertion sort 의 출발점).
            for (int i = 0; i < _endpoints.Length; i++)
            {
                var ep = _endpoints[i];
                ep.Value = ep.IsLo ? _objects[ep.ObjectIndex].Bounds.MinX : _objects[ep.ObjectIndex].Bounds.MaxX;
                _endpoints[i] = ep;
            }

            // [3] *Insertion sort* — temporal coherence 의 본체.
            //     객체가 조금만 움직였으면 정렬 순서가 거의 그대로라 swap 이 매우 적게 발생.
            //     Array.Sort (QuickSort) 는 거의 정렬된 데이터에 *오히려 비효율* 이라 손수 구현.
            stats.SwapsThisFrame = InsertionSort(_endpoints);

            // [4] Sweep 으로 현재 겹침 쌍 재검출. (active 추적이 매 프레임 새로 시작.)
            //     ※ 더 영리한 incremental SAP 는 swap 도중 *바뀐 쌍만* 추가/제거하지만,
            //       학습용으로는 매 프레임 full sweep 이 명료하다.
            var pairs = SweepAllSimple();
            stats.ActiveOverlaps = pairs.Count;
            return pairs;
        }

        /// <summary>
        /// Insertion sort. 거의 정렬된 배열에 평균 O(N) — temporal coherence 와 완벽히 맞아떨어짐.
        /// 반환값 = swap 횟수 (시각화 통계용).
        /// </summary>
        private static int InsertionSort(SAPEndpoint[] arr)
        {
            int swaps = 0;
            for (int i = 1; i < arr.Length; i++)
            {
                var key = arr[i];
                int j = i - 1;
                while (j >= 0 && arr[j].Value > key.Value)
                {
                    arr[j + 1] = arr[j];
                    j--;
                    swaps++;
                }
                arr[j + 1] = key;
            }
            return swaps;
        }

        /// <summary>SweepAll 의 통계 없는 버전 (UpdateAndSweep 내부용).</summary>
        private List<SAPPair> SweepAllSimple()
        {
            var pairs = new List<SAPPair>();
            var active = new HashSet<int>();

            for (int i = 0; i < _endpoints.Length; i++)
            {
                var ep = _endpoints[i];
                if (ep.IsLo)
                {
                    foreach (int activeIdx in active)
                    {
                        if (_objects[activeIdx].Bounds.Overlaps(_objects[ep.ObjectIndex].Bounds))
                        {
                            pairs.Add(new SAPPair(activeIdx, ep.ObjectIndex));
                        }
                    }
                    active.Add(ep.ObjectIndex);
                }
                else
                {
                    active.Remove(ep.ObjectIndex);
                }
            }
            return pairs;
        }

        // ─────────────────────────────────────────────────────────────
        // Brute Force 비교용 (학습/검증 baseline)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 모든 N(N-1)/2 쌍에 대해 AABB.Overlaps 검사 — O(N²).
        /// SAP 가속 효과의 baseline. 시각화에서 "Brute Force = N(N-1)/2 쌍 검사" 로 표시.
        /// </summary>
        public static List<SAPPair> BruteForceAllPairs(IReadOnlyList<SAPObject<T>> objects)
        {
            var pairs = new List<SAPPair>();
            for (int i = 0; i < objects.Count; i++)
            {
                for (int j = i + 1; j < objects.Count; j++)
                {
                    if (objects[i].Bounds.Overlaps(objects[j].Bounds))
                    {
                        pairs.Add(new SAPPair(i, j));
                    }
                }
            }
            return pairs;
        }
    }
}
