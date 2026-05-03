using System.Collections.Generic;
using Algorithms.Physics; // AABB — 노드 cell 영역 표현 (시각화 보조). K-d tree 알고리즘 자체는 AABB 미사용.

namespace Algorithms.Spatial
{
    // =====================================================================
    //  K-d tree 가 저장하는 점 한 개
    // =====================================================================
    //
    // ▶ Quadtree / BVH 와 같은 패턴 — T 는 GameObject id, 적 인덱스 같은 *원본 데이터의 핸들*.
    public readonly struct KdPoint<T>
    {
        public readonly float X;
        public readonly float Y;
        public readonly T Data;

        public KdPoint(float x, float y, T data)
        {
            X = x;
            Y = y;
            Data = data;
        }
    }

    /// <summary>
    /// kNN 결과 항목 한 개. 시각화 / 게임 로직에서 거리까지 함께 필요할 때 사용.
    /// </summary>
    public struct KnnResult<T>
    {
        public KdPoint<T> Point;
        /// <summary>거리의 *제곱*. sqrt 비용을 피하기 위해 알고리즘 내부는 모두 거리 제곱으로 비교.</summary>
        public float DistSq;
    }

    /// <summary>
    /// kNN Query 통계. 시각화의 "방문 / 가지치기 / Brute Force 비교" 카운터에 사용.
    /// </summary>
    public struct KdQueryStats
    {
        /// <summary>실제로 들어가서 *점 검사를 수행한* 노드 수 (= 시각화의 주황 노드).
        /// 클래식 K-d tree 는 노드마다 점 1 개라 NodesVisited == PointsChecked.</summary>
        public int NodesVisited;

        /// <summary>가지치기 분기 횟수 (= "분할 반평면 너머가 현재 best 반경보다 멀어 far 자식 통째 스킵" 발생 횟수).
        /// BVH / Quadtree 의 "early return" 카운터와 같은 의미 — *하나의 prune 이 여러 노드를 한꺼번에 스킵* 하므로
        /// "절감된 실제 방문 수" 와는 다름. 카운터 자체는 *가지치기 빈도* 의 지표.</summary>
        public int NodesPruned;
    }

    /// <summary>
    /// K-d tree 노드 (클래식 구조). 모든 점이 자기만의 노드를 가진다 (BVH 의 leaf-only 와 다름).
    ///
    /// ▶ 한 클래스로 leaf / internal 통합
    ///   Quadtree / BVH 와 같은 이유 — 학습용으로 흐름이 한눈에 들어오도록 다형성 분기 회피.
    ///   잎인지는 Left == null && Right == null 로 판정 가능.
    /// </summary>
    public class KdNode<T>
    {
        /// <summary>이 노드가 보관하는 점 (= 분할 hyperplane 을 정의하는 그 점).</summary>
        public KdPoint<T> Point;

        /// <summary>이 노드의 분할 축. 0 = X 축, 1 = Y 축. 깊이가 1 늘어날 때마다 번갈아 바뀐다.</summary>
        public int SplitAxis;

        /// <summary>루트 = 0 부터 시작.</summary>
        public int Depth;

        /// <summary>왼쪽 자식 — 분할 축 값이 *작은* 점들을 보관한 서브트리.</summary>
        public KdNode<T> Left;

        /// <summary>오른쪽 자식 — 분할 축 값이 *큰* 점들을 보관한 서브트리.</summary>
        public KdNode<T> Right;

        /// <summary>
        /// 이 노드가 "차지하는" 공간 영역 (조상의 모든 분할 반평면의 교집합).
        /// 알고리즘 본체는 사용하지 않지만, 시각화에서 분할선을 *부모 영역 안에서만* 그리려면 필요.
        /// (= Mondrian 패턴이 정확히 그려지는 비결.)
        /// </summary>
        public AABB CellBounds;
    }

    /// <summary>
    /// =====================================================================
    ///  K-d tree (k-dimensional tree, 여기서는 k=2) — 점 집합 분할 트리
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// K-d tree 는 *공간을 한 축씩 번갈아 가르며* 만든 이진 트리. 모든 점이 자기만의 노드를 가지고,
    /// 각 노드는 *한 축의 한 좌표* 에서 공간을 *직각 반평면* 으로 가른다. 깊이가 1 늘어날 때마다
    /// 분할 축이 바뀐다 (depth 0 = X 축, depth 1 = Y 축, depth 2 = X 축, …).
    ///
    /// ▶ Quadtree / BVH / Spatial Hashing 사이의 위치
    ///   - **Quadtree**       : 공간을 *균등 4 등분*. 데이터 분포 무시. 형제 영역 안 겹침.
    ///   - **Spatial Hashing** : 공간을 *균등 격자*. 데이터 분포 무시. 형제 영역 안 겹침.
    ///   - **BVH**             : *객체* 를 가까운 둘로 묶음. 데이터 적응. 형제 영역 *겹침*.
    ///   - **K-d tree**        : *공간* 을 데이터 *중앙값* 에서 한 축씩 가름. 데이터 적응. 형제 영역 안 겹침.
    ///   → "Quadtree 의 공간 명확성 + BVH 의 데이터 적응성" 의 하이브리드.
    ///     이게 K-d tree 가 점이 *편향 분포* 일 때 Quadtree 보다 균형이 잡히는 이유.
    ///
    /// ▶ K-d tree 의 킬러 앱 — *kNN (k-Nearest Neighbors) Query*
    ///   "쿼리 점 근처의 가장 가까운 k 개 점을 찾아라" 가 K-d tree 의 사실상 표준 사용처:
    ///     · 머신러닝 KNN 분류기 / 벡터 유사도 검색 (사진 / 텍스트 임베딩)
    ///     · AI 의 "가장 가까운 적" / "가장 가까운 자원" 탐색
    ///     · 입자 시뮬레이션의 "주변 입자 N 개" (SPH 유체)
    ///     · 음성 / 음향 — DTW 매칭
    ///   *영역 쿼리* 는 Quadtree 가, *광선 쿼리* 는 BVH 가, *최근접 이웃 쿼리* 는 K-d tree 가 표준.
    ///
    /// ▶ kNN 의 핵심 트릭 — *Branch-and-Bound* 가지치기
    ///   각 노드에서 query 점이 어느 쪽 자식 영역에 있는지로 *near / far* 를 결정.
    ///     ① near 자식 먼저 들어가 best-k 후보를 좁힌다 (= 반경 r 이 작아진다).
    ///     ② far 자식은 *분할 반평면이 query 로부터 r 보다 멀면 통째 스킵*.
    ///       "현재 r 안에 있는 더 가까운 점이 far 영역에 있을 수 있는가?" 의 boolean 검사.
    ///     ③ 이 한 줄이 평균 O(N) → O(log N) 가속을 만든다.
    ///
    /// 2. 동작 흐름
    /// ---------------------------------------------------------------------
    ///   ▷ Build (top-down, alternating-axis median split)
    ///     ① 빈 트리 + 점 인덱스 리스트로 시작. 깊이 = 0, 축 = X.
    ///     ② 인덱스를 현재 축의 *좌표* 로 정렬.
    ///     ③ 정렬된 리스트의 *중앙값* 점 → 이 노드의 Point.
    ///     ④ 좌측 절반 → Left 서브트리, 우측 절반 → Right 서브트리.
    ///     ⑤ 두 자식 모두에 ① 부터 재귀. 단 *축은 (depth + 1) % 2* 로 바꿔서.
    ///
    ///   ▷ kNN Query (kNearest)
    ///     ① 현재 노드의 점을 best-k 후보에 추가 시도 (더 가까우면 worst 와 교체).
    ///     ② 분할 축 좌표로 query 가 *near / far* 어느 쪽인지 판정.
    ///     ③ near 자식 재귀 — query 가 *있는* 쪽 우선 탐색 (반경을 빨리 좁힘).
    ///     ④ *Pruning 검사* — far 자식 들어갈 가치가 있는가:
    ///        · best-k 가 아직 k 개 미만 → 무조건 들어감 (후보가 부족).
    ///        · |query[axis] − node.Point[axis]|² ≥ kth_best_dist² → 통째 스킵 (가지치기 ✂️).
    ///        · 그 외 → far 자식도 재귀.
    ///
    /// 3. 시간 / 공간 복잡도  (N = 점 수, k = 요청 이웃 수)
    /// ---------------------------------------------------------------------
    ///   - Build  : O(N log² N)  — 단순 정렬 기반. nth_element 쓰면 O(N log N).
    ///   - kNN    : 평균 O(log N + k), 최악 O(N) (점이 한 직선 위에 정렬된 경우)
    ///   - 공간   : O(N) — 모든 점이 노드, 트리 헤더 포함
    ///
    ///   ※ Brute Force 와의 비교 (k=5 가정):
    ///        N=100      → O(N) ≈ 100  vs  O(log N) ≈ 12  → 약 8 배
    ///        N=10,000   → 10,000      vs  ≈ 18           → 약 550 배
    ///        N=1,000,000 → 1,000,000  vs  ≈ 25           → 약 40,000 배
    ///        kNN 은 보통 N 이 클 때 (벡터 검색 등) 쓰이므로 차이가 폭발적.
    ///
    /// 4. 게임에서의 사용처
    /// ---------------------------------------------------------------------
    ///   - **NPC AI 의 "가장 가까운 적/자원" 탐색** : 매 프레임 모든 NPC ↔ 모든 적 = O(N²)
    ///       → 적들을 K-d tree 에 넣고 NPC 마다 1-NN 쿼리 = O(N log N)
    ///   - **입자 시뮬레이션의 SPH (Smoothed Particle Hydrodynamics)** : 각 입자의 "주변 K 개" 검색
    ///   - **벡터 유사도 검색** : 사진 / 텍스트 임베딩 공간에서 가장 비슷한 K 개 (RAG, 추천 시스템)
    ///   - **머신러닝 KNN 분류기** : "가장 가까운 K 개의 라벨 다수결" — 알고리즘 코어가 K-d tree
    ///   - **로봇 / 자율주행 path planning** : 샘플링 기반 알고리즘 (RRT, PRM) 의 "근처 노드" 검색
    ///   - **photogrammetry / 3D 스캐닝** : 점군 (point cloud) 정합 (ICP 의 매칭 단계)
    ///
    /// 5. 사용 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   - **클래식 K-d tree (모든 점이 노드)** 를 채택
    ///       : leaf-only 변형도 있지만 학습용으로는 *Bentley 1975 원형* 이 가장 직관적.
    ///         "트리의 각 노드가 곧 점이고, 그 점이 평면을 가르는 hyperplane 을 정의" 라는
    ///         핵심 직관이 코드에 직접 드러난다.
    ///   - KdNode (class — 참조 타입)
    ///       : 자식 참조가 필요해 자연스럽게 class. struct 면 자기참조 불가.
    ///   - List&lt;KnnResult&lt;T&gt;&gt; (size = k) — 단순 배열 (heap 아님)
    ///       : k 가 보통 1 ~ 20 정도라 O(k) 검사 비용 무시 가능. heap 은 코드 길어 학습용 부적합.
    ///       실무 BIG K (수만) 라면 max-heap (PriorityQueue) 으로 교체.
    ///   - AABB (Algorithms.Physics) — 노드의 CellBounds 보관용
    ///       : 알고리즘 자체는 cell 모름. *시각화* 가 분할선을 부모 영역 안에서만 그리려면 필요.
    ///         BVH 가 AABB.Raycast 를 부품으로 쓰는 것과 같은 카테고리 횡단 의존.
    ///   - **별도 visited 집합 없음**
    ///       : 트리는 사이클 없음 → 같은 노드 재방문 자체가 불가능 (Quadtree / BVH 와 같은 이유).
    ///   - **거리 *제곱* 으로 비교**
    ///       : sqrt 는 비싸고 비교에는 불필요. 실제 거리는 결과 반환 후 한 번만 계산.
    /// </summary>
    public class KdTree<T>
    {
        // ─────────────────────────────────────────────────────────────
        // 인스턴스 상태
        // ─────────────────────────────────────────────────────────────

        /// <summary>트리의 루트 노드. 빈 트리면 null.</summary>
        public KdNode<T> Root { get; private set; }

        /// <summary>총 노드 수 (= 점 수). 통계 / 시각화 패널용.</summary>
        public int NodeCount { get; private set; }

        /// <summary>최대 깊이. 시각화 색 그라데이션 정규화에 사용.</summary>
        public int MaxDepth { get; private set; }

        /// <summary>전체 점 배열 (build 입력 그대로). Brute Force 비교 호출자가 같은 데이터를 사용하기 위함.</summary>
        public IReadOnlyList<KdPoint<T>> Points => _points;

        private readonly KdPoint<T>[] _points;

        // ─────────────────────────────────────────────────────────────
        // 생성자 — 즉시 트리 빌드
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 점 집합을 받아 즉시 K-d tree 를 구축한다 (alternating-axis median split).
        ///
        /// worldBounds:
        ///   루트 노드의 CellBounds 초깃값. 모든 점이 이 영역 안에 있어야 시각화가 자연스럽다.
        ///   알고리즘 자체는 이 값을 *수학적으로* 사용하지 않고, 자식 cell 계산의 출발점 역할만 한다.
        /// </summary>
        public KdTree(IList<KdPoint<T>> points, in AABB worldBounds)
        {
            _points = new KdPoint<T>[points.Count];
            for (int i = 0; i < points.Count; i++) _points[i] = points[i];

            NodeCount = 0;
            MaxDepth = 0;

            if (_points.Length == 0) { Root = null; return; }

            // 인덱스 리스트로 빌드 — 점 자체는 절대 복사하지 않는다.
            var allIndices = new List<int>(_points.Length);
            for (int i = 0; i < _points.Length; i++) allIndices.Add(i);

            Root = BuildRecursive(allIndices, depth: 0, in worldBounds);
        }

        // ─────────────────────────────────────────────────────────────
        // Build — alternating-axis median split
        // ─────────────────────────────────────────────────────────────

        private KdNode<T> BuildRecursive(List<int> indices, int depth, in AABB cellBounds)
        {
            if (indices.Count == 0) return null;

            // [1] 현재 깊이의 분할 축 — 0=X, 1=Y. (k 차원 일반화: depth % k.)
            int axis = depth % 2;

            // [2] 인덱스를 현재 축 좌표로 정렬.
            //     ※ 단순 Sort = O(n log n). 빠르게 하려면 nth_element (Quickselect) 로 O(n) 가능.
            //       학습용은 Sort 가 명확하다.
            indices.Sort((a, b) =>
            {
                float ca = AxisValue(_points[a], axis);
                float cb = AxisValue(_points[b], axis);
                return ca.CompareTo(cb);
            });

            // [3] 정렬된 리스트의 중앙값을 이 노드의 점으로 채택.
            //     중앙값 = 정렬 후 인덱스 N/2 위치. 이 점이 hyperplane 을 정의.
            int mid = indices.Count / 2;
            int medianIdx = indices[mid];
            var medianPoint = _points[medianIdx];

            var node = new KdNode<T>
            {
                Point = medianPoint,
                SplitAxis = axis,
                Depth = depth,
                CellBounds = cellBounds,
            };
            NodeCount++;
            if (depth > MaxDepth) MaxDepth = depth;

            // [4] 자식 cell 영역 계산 — 부모 영역을 분할 축의 splitValue 에서 둘로 가른다.
            //     좌측 = [parent.Min, splitValue], 우측 = [splitValue, parent.Max].
            float splitValue = AxisValue(medianPoint, axis);
            AABB leftCell, rightCell;
            if (axis == 0)
            {
                // X 축으로 가르기 — 세로선
                leftCell = MakeAabb(cellBounds.MinX, splitValue,           cellBounds.MinY, cellBounds.MaxY);
                rightCell = MakeAabb(splitValue,         cellBounds.MaxX, cellBounds.MinY, cellBounds.MaxY);
            }
            else
            {
                // Y 축으로 가르기 — 가로선
                leftCell = MakeAabb(cellBounds.MinX, cellBounds.MaxX, cellBounds.MinY, splitValue);
                rightCell = MakeAabb(cellBounds.MinX, cellBounds.MaxX, splitValue,         cellBounds.MaxY);
            }

            // [5] 좌/우 인덱스 분배. 중앙값 *자기 자신* 은 이미 노드에 들어갔으므로 제외.
            var leftIndices = indices.GetRange(0, mid);
            var rightIndices = indices.GetRange(mid + 1, indices.Count - mid - 1);

            // [6] 두 자식 모두에 *축을 한 단계 회전시켜* 재귀.
            node.Left = BuildRecursive(leftIndices, depth + 1, in leftCell);
            node.Right = BuildRecursive(rightIndices, depth + 1, in rightCell);

            return node;
        }

        /// <summary>min-max → center+halfsize 형식으로 변환 (AABB struct 호환).</summary>
        private static AABB MakeAabb(float minX, float maxX, float minY, float maxY)
        {
            float cx = (minX + maxX) * 0.5f;
            float cy = (minY + maxY) * 0.5f;
            float hw = (maxX - minX) * 0.5f;
            float hh = (maxY - minY) * 0.5f;
            return new AABB(cx, cy, hw, hh);
        }

        private static float AxisValue(in KdPoint<T> p, int axis) => axis == 0 ? p.X : p.Y;

        // ─────────────────────────────────────────────────────────────
        // kNN Query — Branch-and-Bound (가지치기)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// (queryX, queryY) 에 가장 가까운 k 개의 점을 거리 오름차순으로 반환.
        /// stats 로 방문 / 가지치기 횟수 통계 함께 반환 (시각화 카운터용).
        /// </summary>
        public List<KnnResult<T>> KNearest(float queryX, float queryY, int k, out KdQueryStats stats)
        {
            var results = new List<KnnResult<T>>(k);
            stats = default;
            if (Root == null || k <= 0) return results;

            KnnRecursive(Root, queryX, queryY, k, results, ref stats);

            // 결과 거리 오름차순 정렬 — 호출자가 first-NN 을 0번 인덱스로 받을 수 있도록.
            results.Sort((a, b) => a.DistSq.CompareTo(b.DistSq));
            return results;
        }

        private void KnnRecursive(KdNode<T> node, float qx, float qy, int k, List<KnnResult<T>> results, ref KdQueryStats stats)
        {
            if (node == null) return;

            stats.NodesVisited++;

            // [1] 이 노드의 점을 best-k 후보에 추가 시도.
            //     거리 *제곱* 으로 비교 (sqrt 회피). 최종 거리가 필요하면 호출자가 sqrt(DistSq).
            float dx = node.Point.X - qx;
            float dy = node.Point.Y - qy;
            float distSq = dx * dx + dy * dy;
            TryAddToTopK(results, node.Point, distSq, k);

            // [2] query 가 어느 쪽 자식 영역에 있는지로 near / far 결정.
            //     axisDelta < 0 → query 가 분할선의 *왼쪽/아래* → near = Left.
            //     axisDelta > 0 → query 가 분할선의 *오른쪽/위* → near = Right.
            int axis = node.SplitAxis;
            float queryAxisVal = axis == 0 ? qx : qy;
            float nodeAxisVal = AxisValue(node.Point, axis);
            float axisDelta = queryAxisVal - nodeAxisVal;

            KdNode<T> near = axisDelta < 0f ? node.Left : node.Right;
            KdNode<T> far = axisDelta < 0f ? node.Right : node.Left;

            // [3] near 자식 *항상* 재귀 — query 가 있는 쪽을 먼저 탐색해 반경을 빨리 좁힌다.
            KnnRecursive(near, qx, qy, k, results, ref stats);

            // [4] far 자식 가지치기 검사 — *알고리즘의 핵심 한 줄*.
            //     분할 반평면까지의 query 의 거리 = |axisDelta|.
            //     이게 현재 best-k 의 *worst distance* 보다 멀면 → far 영역에 더 가까운 점이 있을 수 없음 → 스킵.
            float worstDistSq = WorstOfTopK(results, k);
            float planeDistSq = axisDelta * axisDelta;

            if (results.Count < k || planeDistSq < worstDistSq)
            {
                KnnRecursive(far, qx, qy, k, results, ref stats);
            }
            else
            {
                // 가지치기 발생 — far 가 null 이면 사실상 의미 없는 카운트라 null 체크.
                if (far != null) stats.NodesPruned++;
            }
        }

        /// <summary>
        /// best-k 리스트에 후보 추가 시도. k 미만이면 그냥 추가, k 도달이면 *현재 worst* 와 교체.
        ///
        /// O(k) 검사 — k 가 작을 때 (≤ 20) 충분히 빠르고 max-heap 보다 코드가 짧다.
        /// 실무 BIG K (수만) 라면 PriorityQueue 로 교체 권장.
        /// </summary>
        private static void TryAddToTopK(List<KnnResult<T>> results, in KdPoint<T> point, float distSq, int k)
        {
            if (results.Count < k)
            {
                results.Add(new KnnResult<T> { Point = point, DistSq = distSq });
                return;
            }

            // 현재 worst (가장 먼) 항목 찾기.
            int worstIdx = 0;
            for (int i = 1; i < results.Count; i++)
            {
                if (results[i].DistSq > results[worstIdx].DistSq) worstIdx = i;
            }

            // 새 점이 worst 보다 가까우면 교체.
            if (distSq < results[worstIdx].DistSq)
            {
                results[worstIdx] = new KnnResult<T> { Point = point, DistSq = distSq };
            }
        }

        /// <summary>
        /// 현재 best-k 중 *가장 먼* 점의 거리 제곱.
        /// k 개 미만이면 ∞ 반환 (= 가지치기 안 함, 후보를 더 채워야 함).
        /// </summary>
        private static float WorstOfTopK(List<KnnResult<T>> results, int k)
        {
            if (results.Count < k) return float.PositiveInfinity;
            float worst = results[0].DistSq;
            for (int i = 1; i < results.Count; i++)
            {
                if (results[i].DistSq > worst) worst = results[i].DistSq;
            }
            return worst;
        }

        // ─────────────────────────────────────────────────────────────
        // Brute Force 비교용 (학습/검증 baseline)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 모든 점에 대해 거리 검사 후 가까운 k 개 반환 (O(N log N) — 모든 점 거리 계산 + 정렬).
        /// K-d tree 가속 효과를 정량 비교하기 위한 baseline.
        /// </summary>
        public static List<KnnResult<T>> BruteForceKNearest(IReadOnlyList<KdPoint<T>> points, float queryX, float queryY, int k)
        {
            var results = new List<KnnResult<T>>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                float dx = points[i].X - queryX;
                float dy = points[i].Y - queryY;
                results.Add(new KnnResult<T> { Point = points[i], DistSq = dx * dx + dy * dy });
            }
            results.Sort((a, b) => a.DistSq.CompareTo(b.DistSq));
            if (results.Count > k) results.RemoveRange(k, results.Count - k);
            return results;
        }

        // ─────────────────────────────────────────────────────────────
        // 트리 walk — Visualizer 가 분할선을 BFS 순서로 등장시킬 때 사용
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 트리를 BFS 순서 (얕은 깊이 → 깊은 깊이) 로 walk.
        /// 시각화 빌드 애니메이션에서 "루트 분할선 → 자식 분할선 → 손자" 순으로 등장시킬 때 사용.
        /// </summary>
        public IEnumerable<KdNode<T>> WalkBFS()
        {
            if (Root == null) yield break;

            var queue = new Queue<KdNode<T>>();
            queue.Enqueue(Root);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                yield return node;

                if (node.Left != null) queue.Enqueue(node.Left);
                if (node.Right != null) queue.Enqueue(node.Right);
            }
        }
    }
}
