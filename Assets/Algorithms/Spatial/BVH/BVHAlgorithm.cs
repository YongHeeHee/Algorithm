using System.Collections.Generic;
using Algorithms.Physics; // AABB struct + Slab Raycast — BVH 가 노드 경계 / 광선 검사에 그대로 재사용

namespace Algorithms.Spatial
{
    // =====================================================================
    //  BVH 가 사용하는 객체 한 개 (Bounding Volume + 임의 데이터)
    // =====================================================================
    //
    // ▶ 왜 점이 아니라 *AABB 를 가진 객체* 인가
    //   Quadtree / Spatial Hashing 은 "점" 을 인덱싱하지만, BVH 는 *부피를 가진 객체*
    //   (게임의 메시, 콜라이더, 레이트레이싱의 삼각형) 를 인덱싱한다. 트리의 각 노드는
    //   "이 서브트리가 담는 모든 객체 AABB 의 합집합 (= 둘러싸는 AABB)" 을 가진다.
    //   → 이게 바로 *Bounding Volume Hierarchy* 라는 이름의 정확한 의미.
    //
    // ▶ T 의 의미
    //   QuadtreePoint<T> 와 같은 패턴 — 게임에서 T 는 보통 GameObject id, 콜라이더 인덱스,
    //   삼각형 인덱스 같은 *원본 데이터의 핸들* 이 된다.
    public readonly struct BVHObject<T>
    {
        public readonly AABB Bounds;
        public readonly T Data;

        public BVHObject(in AABB bounds, T data)
        {
            Bounds = bounds;
            Data = data;
        }
    }

    /// <summary>
    /// 광선이 통과한 객체 한 개의 결과. 시각화 / 디버깅 / 게임 로직에서 hit 지점이 필요할 때 사용.
    /// </summary>
    public struct BVHRayHit<T>
    {
        /// <summary>객체의 인덱스 — Visualizer 가 마커 GameObject 와 매핑할 때 쓴다.</summary>
        public int ObjectIndex;
        /// <summary>객체 데이터 (T).</summary>
        public T Data;
        /// <summary>광선 위 객체 진입 시각 — 가장 작은 값이 *first hit* 이다.</summary>
        public float TEnter;
    }

    /// <summary>
    /// Query 통계. 시각화의 "방문 / 가지치기 / 검사한 객체 / Brute Force 비교" 카운터에 사용.
    /// </summary>
    public struct BVHQueryStats
    {
        /// <summary>광선 검사를 통과해 *내부 재귀로 들어간* 노드 수 (= 시각화에서 주황으로 칠해지는 노드).</summary>
        public int NodesVisited;

        /// <summary>광선 검사에서 거부돼 *서브트리 통째로 스킵된* 노드 수 (= 가속 효과의 본체).</summary>
        public int NodesPruned;

        /// <summary>leaf 안에서 실제 객체에 대해 광선 검사를 수행한 횟수.
        /// Brute Force 였다면 항상 = 전체 객체 수. BVH 면 보통 그보다 훨씬 작다.</summary>
        public int ObjectsTested;
    }

    /// <summary>
    /// BVH 트리의 노드. leaf / internal 두 형태가 같은 클래스를 공유한다.
    ///
    /// ▶ 왜 한 클래스로 묶었는가
    ///   Quadtree 와 같은 이유 — leaf / internal 을 별도 클래스로 두면 트리 walk 코드가
    ///   다형성으로 흩어져 학습용으로는 흐름 파악이 어렵다. 한 클래스 + IsLeaf 플래그가
    ///   "이 알고리즘은 한 파일에서 끝까지 읽힌다" 라는 프로젝트 원칙에 맞다.
    ///
    /// ▶ 시각화가 직접 walk 할 수 있도록 모든 필드를 public 노출
    ///   알고리즘 자체는 Bounds / Children 에 직접 접근하지 않는 메서드만으로도 동작하지만,
    ///   Visualizer 가 트리를 BFS 로 walk 하며 라인을 그리려면 외부에서 접근 가능해야 한다.
    /// </summary>
    public class BVHNode
    {
        /// <summary>이 서브트리가 담는 모든 객체 AABB 의 합집합 (= 둘러싸는 AABB).</summary>
        public AABB Bounds;

        /// <summary>이 노드가 leaf 인가. true 면 ObjectIndices 가 채워지고 Left/Right 가 null.</summary>
        public bool IsLeaf;

        /// <summary>leaf 일 때만 채워짐 — 이 leaf 가 직접 보관하는 객체들의 인덱스.</summary>
        public List<int> ObjectIndices;

        /// <summary>internal 일 때만 채워짐 — 자식 2 개. (Quadtree 의 4 자식과 달리 BVH 는 보통 binary tree.)</summary>
        public BVHNode Left;
        public BVHNode Right;

        /// <summary>루트=0 부터 시작하는 깊이. 시각화에서 깊이별 색 그라데이션에 사용.</summary>
        public int Depth;
    }

    /// <summary>
    /// =====================================================================
    ///  BVH (Bounding Volume Hierarchy) — 객체 기반 공간 분할 트리
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// BVH 는 *부피를 가진 객체들* 을 가까운 것끼리 묶어 만든 이진 트리. 각 노드는
    /// "이 서브트리가 담는 모든 객체 AABB 의 합집합 (= 둘러싸는 AABB)" 을 가지고,
    /// leaf 노드만 실제 객체 인덱스 리스트를 보관한다.
    ///
    /// ▶ Quadtree / Spatial Hashing 과의 결정적 차이 — *공간이 아니라 객체를 분할*
    ///   - Quadtree         : 공간을 균등 4 등분. 자식 영역 *겹침 없음*. 객체 분포 무시.
    ///   - Spatial Hashing  : 공간을 균등 격자. 셀 영역 *겹침 없음*. 객체 분포 무시.
    ///   - **BVH**          : 객체를 가까운 둘로 묶음. 형제 AABB *겹쳐도 됨*. 객체 분포에 적응.
    ///   → BVH 의 형제 AABB 가 겹치는 게 "버그" 가 아니라 *정의* — 공간이 아닌 객체를 분할하므로
    ///     같은 영역을 두 노드가 공유할 수 있다. 이게 비균일 분포에서 강한 이유.
    ///
    /// ▶ BVH 의 킬러 앱 — *Ray Query*
    ///   BVH 가 모던 게임에서 *사실상 표준* 인 이유는 광선 ↔ 수많은 삼각형/객체 검사를 가속하기 때문:
    ///     · NVIDIA RTX / Unity DXR 의 레이트레이싱 가속 자료구조 = BVH (BLAS / TLAS)
    ///     · PhysX / Bullet / Box2D 의 broad-phase = Dynamic AABBTree (= 움직이는 BVH)
    ///     · 카메라 frustum culling, 음영 광선, AI 시야 광선 — 모두 같은 패턴
    ///   → 한 광선이 트리를 내려가며 *AABB.Raycast 가 false 인 서브트리는 통째로 스킵* 한다.
    ///     얇은 1 차원 광선이 2/3 차원 공간을 가르므로 가지치기 비율이 매우 높다.
    ///
    /// ▶ AABB (원자) ↔ BVH (트리) 관계
    ///   이 알고리즘은 `Algorithms.Physics.AABB.Raycast` (Slab method) 를 *그대로* 재사용한다.
    ///   트리의 모든 노드에서 같은 함수가 호출되므로, BVH 는 사실상 "AABB.Raycast 를 트리 위에 쌓아
    ///   O(N) → O(log N) 으로 가속한 자료구조" 라고 한 줄로 요약할 수 있다.
    ///
    /// 2. 동작 흐름
    /// ---------------------------------------------------------------------
    ///   ▷ Build (top-down, longest-axis median split)  ※ 가장 단순한 빌드 — 응용에서 SAH 언급
    ///     ① 모든 객체의 합집합 AABB 를 계산 → 루트 노드의 Bounds.
    ///     ② 객체 수 ≤ maxLeafSize 면 → leaf 로 종료 (이 노드가 객체 인덱스 리스트 보관).
    ///     ③ 그렇지 않으면 → 합집합 AABB 의 *가장 긴 축* 을 선택.
    ///     ④ 그 축의 객체 *중심 좌표* 로 객체 인덱스를 정렬.
    ///     ⑤ 정렬된 리스트의 *중앙값* 위치에서 좌/우로 가르고 → 두 자식 모두에 ① 부터 재귀.
    ///
    ///   ▷ Raycast(ray, stats)
    ///     ① 내 Bounds 에 광선이 안 맞으면 → NodesPruned++ 하고 즉시 return (= 가속의 본체).
    ///     ② NodesVisited++.
    ///     ③ 내가 leaf 면 → 보관한 객체들 각각에 대해 AABB.Raycast → hit 인 것만 결과에 추가.
    ///     ④ 내가 internal 이면 → 두 자식 모두에 재귀 위임.
    ///        (정렬 최적화: 더 가까운 자식부터 들어가면 first-hit 종료가 가능하지만,
    ///         학습용 BVH 는 *전부 수집* — 단순함 우선.)
    ///
    /// 3. 시간 / 공간 복잡도  (N = 객체 수, K = 광선이 hit 하는 객체 수)
    /// ---------------------------------------------------------------------
    ///   - Build   : O(N log N)  — 정렬 기반 분할의 표준 비용
    ///   - Raycast : 평균 O(log N + K), 최악 O(N) (모든 객체가 광선을 따라 일렬로 늘어선 경우)
    ///   - 공간    : O(N) — 객체 자체 + 트리 노드 (보통 N 의 작은 상수배)
    ///
    ///   ※ Brute Force 와의 비교:
    ///        N 개 객체에 대해 광선 검사 = O(N) AABB.Raycast 호출
    ///        BVH 위에서는 평균 O(log N + K) — N=1000, K=2 일 때 약 100 배 가속.
    ///        N 이 클수록, 광선이 적게 hit 할수록 차이가 폭발적이다 (= 레이트레이싱의 일반 상황).
    ///
    /// 4. 게임에서의 사용처
    /// ---------------------------------------------------------------------
    ///   - **레이트레이싱 (RTX / DXR)** : 광선 ↔ 수백만 삼각형 검사 가속. 게임 BVH 가 가장 화려한 사용처.
    ///   - **물리 broad-phase (Dynamic AABBTree)** : Box2D / Bullet / PhysX 의 충돌 후보 추리기.
    ///       동적 객체용으로 변형된 BVH — Insert / Remove / Refit 지원.
    ///   - **카메라 frustum culling** : 절두체 6 평면 ↔ 노드 AABB 검사로 통째로 컬링.
    ///   - **AI 시야 / 사격 광선** : NPC 의 시야 검사, 총알 hitscan.
    ///   - **마우스 픽킹 (3D 에디터)** : 클릭 광선 ↔ 씬 객체 → 첫 hit 객체 선택.
    ///   - **광원 / 그림자 가속** : 광원 영향권 ↔ 객체 AABB 검사.
    ///
    /// 5. 사용 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   - **AABB (Algorithms.Physics)** 를 *재사용* 하는 것이 핵심 설계 결정
    ///       : 노드의 둘러싸는 박스 = AABB. 광선 검사 = AABB.Raycast.
    ///         별도 BVHBounds 를 만들지 않음으로써 "BVH 는 AABB.Raycast 를 트리에 쌓은 것" 이라는
    ///         학습 메시지를 코드 import 한 줄로 표현한다 (using Algorithms.Physics).
    ///   - BVHNode (class — 참조 타입)
    ///       : 트리 노드는 자식 참조를 가지므로 class 가 자연스럽다. struct 로 두면 Left/Right 가
    ///         값 복사 / 자기참조 불가능 문제로 어색해진다.
    ///   - List&lt;int&gt; ObjectIndices (leaf 일 때만)
    ///       : 객체 자체를 leaf 마다 복사하면 정렬 / 분할 비용이 폭발한다. *인덱스만* 보관하고
    ///         원본 객체 배열은 BVH 가 한 번만 갖는다.
    ///   - 별도 visited 집합이 *없다*
    ///       : 트리는 사이클이 없는 비순환 구조 → 같은 노드를 두 번 방문할 일이 없음 (Quadtree 와 동일 이유).
    ///   - Binary tree (자식 2 개)
    ///       : Quadtree 의 4 자식 / Octree 의 8 자식과 다른 BVH 의 또 다른 특징. 객체를 *둘로 가르는*
    ///         재귀 분할이라 자식 수가 2 가 자연스럽다. 4-way / 8-way BVH 도 존재하지만 SIMD 최적화 목적이며
    ///         학습용으로는 binary 가 표준.
    /// </summary>
    public class BVH<T>
    {
        // ─────────────────────────────────────────────────────────────
        // 인스턴스 상태
        // ─────────────────────────────────────────────────────────────

        /// <summary>트리의 루트 노드. Visualizer 가 BFS walk 의 출발점으로 사용.</summary>
        public BVHNode Root { get; private set; }

        /// <summary>전체 객체 배열. leaf 의 ObjectIndices 가 이 배열을 가리킨다.</summary>
        public IReadOnlyList<BVHObject<T>> Objects => _objects;

        /// <summary>총 노드 수 (시각화 패널 / 통계용).</summary>
        public int NodeCount { get; private set; }

        /// <summary>최대 깊이 (시각화 색 그라데이션 정규화에 사용).</summary>
        public int MaxDepth { get; private set; }

        private readonly BVHObject<T>[] _objects;
        private readonly int _maxLeafSize;

        // ─────────────────────────────────────────────────────────────
        // 생성자 (= Build 트리거)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 객체 배열을 받아 즉시 트리를 구축한다 (top-down, longest-axis median split).
        ///
        /// maxLeafSize:
        ///   leaf 한 개가 담을 수 있는 최대 객체 수. 이 값 이하가 되면 더 분할하지 않는다.
        ///   - 작게 (1~2): 트리가 깊어지고 leaf 수가 많아짐. 광선 검사 정확도 ↑, 빌드 비용 ↑.
        ///   - 크게 (8~32): 트리가 얕아지고 leaf 가 두꺼워짐. 빌드 비용 ↓, 광선 검사 비용 ↑.
        ///   실무에서는 4~16 사이가 일반적.
        /// </summary>
        public BVH(IList<BVHObject<T>> objects, int maxLeafSize = 4)
        {
            // [1] 입력 객체를 내부 배열로 복사 — 외부 변경 영향을 차단.
            _objects = new BVHObject<T>[objects.Count];
            for (int i = 0; i < objects.Count; i++) _objects[i] = objects[i];

            _maxLeafSize = maxLeafSize < 1 ? 1 : maxLeafSize;

            // [2] 객체 인덱스 리스트로 빌드 시작 — 객체 자체는 절대 복사하지 않는다.
            //     0 ~ N-1 인덱스를 정렬 / 분할하면서 트리를 만든다.
            var allIndices = new List<int>(_objects.Length);
            for (int i = 0; i < _objects.Length; i++) allIndices.Add(i);

            NodeCount = 0;
            MaxDepth = 0;

            // [3] 객체가 0 개면 빈 트리.
            if (allIndices.Count == 0)
            {
                Root = null;
                return;
            }

            Root = BuildRecursive(allIndices, depth: 0);
        }

        // ─────────────────────────────────────────────────────────────
        // Build — top-down, longest-axis median split
        // ─────────────────────────────────────────────────────────────

        private BVHNode BuildRecursive(List<int> indices, int depth)
        {
            // [1] 새 노드 생성. Bounds 는 이 인덱스들이 가리키는 객체 AABB 들의 합집합.
            var node = new BVHNode
            {
                Bounds = ComputeUnionBounds(indices),
                Depth = depth,
            };
            NodeCount++;
            if (depth > MaxDepth) MaxDepth = depth;

            // [2] leaf 종료 조건 — 객체가 maxLeafSize 이하면 더 안 가른다.
            if (indices.Count <= _maxLeafSize)
            {
                node.IsLeaf = true;
                node.ObjectIndices = indices;
                return node;
            }

            // [3] 가장 긴 축 선택 — 그 축으로 갈라야 자식 박스가 가장 좁아진다 (heuristic).
            //     0 = X, 1 = Y. (3D 라면 Z 축 추가만 하면 됨.)
            int splitAxis = LongestAxis(node.Bounds);

            // [4] 그 축의 객체 *중심 좌표* 로 인덱스 정렬.
            //     중심 좌표를 쓰는 이유: 객체가 큰 경우 min 으로 정렬하면 한쪽으로 쏠릴 수 있음.
            indices.Sort((a, b) =>
            {
                float ca = AxisCenter(_objects[a].Bounds, splitAxis);
                float cb = AxisCenter(_objects[b].Bounds, splitAxis);
                return ca.CompareTo(cb);
            });

            // [5] 중앙값에서 좌/우로 가르기 — 정렬된 인덱스의 중간 인덱스가 분할점.
            //     median split: 보장된 균형 트리. (SAH 는 더 영리하지만 코드가 길어진다 — 응용에서 언급.)
            int mid = indices.Count / 2;
            var leftIndices  = indices.GetRange(0, mid);
            var rightIndices = indices.GetRange(mid, indices.Count - mid);

            // [6] 두 자식 모두에 재귀.
            //     ※ 형제 AABB 가 겹칠 수 있다 — 객체 분할이라 영역이 자연스럽게 overlap 됨.
            //       이게 Quadtree 와의 결정적 차이.
            node.Left  = BuildRecursive(leftIndices,  depth + 1);
            node.Right = BuildRecursive(rightIndices, depth + 1);

            return node;
        }

        /// <summary>
        /// 인덱스가 가리키는 객체들의 AABB *합집합* — 둘러싸는 박스 (Bounding Volume).
        /// </summary>
        private AABB ComputeUnionBounds(List<int> indices)
        {
            var first = _objects[indices[0]].Bounds;
            float minX = first.MinX, maxX = first.MaxX;
            float minY = first.MinY, maxY = first.MaxY;

            for (int i = 1; i < indices.Count; i++)
            {
                var b = _objects[indices[i]].Bounds;
                if (b.MinX < minX) minX = b.MinX;
                if (b.MaxX > maxX) maxX = b.MaxX;
                if (b.MinY < minY) minY = b.MinY;
                if (b.MaxY > maxY) maxY = b.MaxY;
            }

            // min-max 형식 → center+halfsize 형식 변환 (AABB struct 의 표현).
            float cx = (minX + maxX) * 0.5f;
            float cy = (minY + maxY) * 0.5f;
            float hw = (maxX - minX) * 0.5f;
            float hh = (maxY - minY) * 0.5f;
            return new AABB(cx, cy, hw, hh);
        }

        /// <summary>가장 긴 축 반환 — 0 = X, 1 = Y. (3D 일반화: Z = 2.)</summary>
        private static int LongestAxis(in AABB box)
            => box.HalfWidth >= box.HalfHeight ? 0 : 1;

        /// <summary>주어진 축에서의 중심 좌표.</summary>
        private static float AxisCenter(in AABB box, int axis)
            => axis == 0 ? box.CenterX : box.CenterY;

        // ─────────────────────────────────────────────────────────────
        // Raycast — BVH 의 킬러 앱
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 광선 r(t) = origin + t · direction 가 통과하는 모든 객체를 반환.
        /// 트리를 내려가며 노드 AABB 에 안 맞는 서브트리는 통째로 스킵 (= pruning, 가속의 본체).
        ///
        /// 결과는 trail 처럼 *모든* hit 를 포함 — 게임에서 first-hit 만 필요하면 호출 쪽에서 TEnter 로 정렬.
        /// </summary>
        public List<BVHRayHit<T>> Raycast(float originX, float originY, float dirX, float dirY, out BVHQueryStats stats)
        {
            var result = new List<BVHRayHit<T>>();
            stats = default;

            if (Root == null) return result;

            RaycastRecursive(Root, originX, originY, dirX, dirY, result, ref stats);
            return result;
        }

        private void RaycastRecursive(
            BVHNode node,
            float originX, float originY, float dirX, float dirY,
            List<BVHRayHit<T>> result,
            ref BVHQueryStats stats)
        {
            // [1] 가속의 본체 — 노드 AABB 와 광선이 안 맞으면 자손까지 통째로 스킵.
            //     ※ AABB.Raycast 는 Algorithms.Physics 의 Slab method — 같은 코드가 트리 모든 노드에서 호출된다.
            //       이 한 줄이 BVH 가 "AABB.Raycast 를 트리 위에 쌓은 것" 이라는 정체성의 표현.
            var nodeHit = node.Bounds.Raycast(originX, originY, dirX, dirY);
            if (!nodeHit.Hit)
            {
                stats.NodesPruned++;
                return;
            }

            stats.NodesVisited++;

            // [2] leaf — 보관한 객체 각각에 대해 광선 검사. (이게 narrow-phase 비슷한 마지막 단계.)
            if (node.IsLeaf)
            {
                for (int i = 0; i < node.ObjectIndices.Count; i++)
                {
                    int objIdx = node.ObjectIndices[i];
                    stats.ObjectsTested++;

                    var objHit = _objects[objIdx].Bounds.Raycast(originX, originY, dirX, dirY);
                    if (objHit.Hit && objHit.TExit >= 0f)
                    {
                        result.Add(new BVHRayHit<T>
                        {
                            ObjectIndex = objIdx,
                            Data = _objects[objIdx].Data,
                            TEnter = objHit.TEnter,
                        });
                    }
                }
                return;
            }

            // [3] internal — 두 자식 모두에 재귀.
            //     ※ 최적화 (생략) : nodeHit.TEnter 가 더 작은 자식부터 들어가면 first-hit 단축이 가능하지만,
            //       학습용 BVH 는 *모든 hit 수집* 이 목적이라 양쪽 다 본다.
            RaycastRecursive(node.Left,  originX, originY, dirX, dirY, result, ref stats);
            RaycastRecursive(node.Right, originX, originY, dirX, dirY, result, ref stats);
        }

        // ─────────────────────────────────────────────────────────────
        // Brute Force 비교용 (학습/검증 baseline)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 모든 객체에 대해 광선 검사 (O(N)). BVH 가속 효과를 정량 비교하기 위한 baseline.
        /// 시각화에서 OnGUI 카운터의 "검사한 객체 vs Brute Force = N" 비교에 사용.
        /// </summary>
        public static List<BVHRayHit<T>> BruteForceRaycast(
            IReadOnlyList<BVHObject<T>> objects,
            float originX, float originY, float dirX, float dirY)
        {
            var result = new List<BVHRayHit<T>>();
            for (int i = 0; i < objects.Count; i++)
            {
                var hit = objects[i].Bounds.Raycast(originX, originY, dirX, dirY);
                if (hit.Hit && hit.TExit >= 0f)
                {
                    result.Add(new BVHRayHit<T>
                    {
                        ObjectIndex = i,
                        Data = objects[i].Data,
                        TEnter = hit.TEnter,
                    });
                }
            }
            return result;
        }

        // ─────────────────────────────────────────────────────────────
        // 트리 walk — Visualizer 가 노드 라인을 한 개씩 등장시킬 때 사용
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 트리를 BFS 순서 (얕은 깊이 → 깊은 깊이) 로 walk. 시각화 빌드 애니메이션에서
        /// "루트 → 자식 → 손자" 순으로 노드 외곽선을 한 개씩 등장시킬 때 사용한다.
        /// </summary>
        public IEnumerable<BVHNode> WalkBFS()
        {
            if (Root == null) yield break;

            var queue = new Queue<BVHNode>();
            queue.Enqueue(Root);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                yield return node;

                if (!node.IsLeaf)
                {
                    if (node.Left  != null) queue.Enqueue(node.Left);
                    if (node.Right != null) queue.Enqueue(node.Right);
                }
            }
        }
    }
}
