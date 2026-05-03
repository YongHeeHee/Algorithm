using System;
using System.Collections.Generic;

namespace Algorithms.Spatial
{
    // =====================================================================
    //  Quadtree 가 사용하는 2D 사각형 영역 (Axis-Aligned Bounding Box, AABB)
    // =====================================================================
    //
    // ▶ 왜 '중심점 + 반-폭/반-높이' 형식인가
    //   같은 사각형을 (xMin, yMin, xMax, yMax) 로 표현해도 결과는 같다.
    //   하지만 4 분할(subdivide) 계산이 빈번하게 일어나는 Quadtree 의 핵심 연산에서
    //   "중심점을 기준으로 반-크기를 절반으로 만들어 4 자식을 만든다" 라는 자연스러운
    //   대칭 표현이 가능해 코드가 짧고 실수가 적다.
    //
    //     자식 NW 의 중심  = (CenterX - HalfWidth/2, CenterY + HalfHeight/2)
    //     자식 NW 의 반-크기 = (HalfWidth/2, HalfHeight/2)
    //
    //   AABB-AABB 교차 검사도 이 형식이 가장 단순:
    //     |c1.x - c2.x| ≤ hw1 + hw2 AND |c1.y - c2.y| ≤ hh1 + hh2
    //
    // ▶ Y 축 방향 약속
    //   이 알고리즘은 수학 좌표계를 따른다 (Y 가 위쪽). Unity 시각화 측에서
    //   (X, Y) → (worldX, worldZ) 로 매핑해 *위에서 내려다보는* 평면으로 보여준다.
    public readonly struct QuadtreeBounds
    {
        public readonly float CenterX;
        public readonly float CenterY;
        public readonly float HalfWidth;
        public readonly float HalfHeight;

        public QuadtreeBounds(float centerX, float centerY, float halfWidth, float halfHeight)
        {
            CenterX = centerX;
            CenterY = centerY;
            HalfWidth = halfWidth;
            HalfHeight = halfHeight;
        }

        public float MinX => CenterX - HalfWidth;
        public float MaxX => CenterX + HalfWidth;
        public float MinY => CenterY - HalfHeight;
        public float MaxY => CenterY + HalfHeight;

        /// <summary>
        /// 점 (x, y) 가 이 영역 안에 있는지. 경계선 위는 포함(closed interval)으로 처리.
        /// 정확히 자식 경계에 걸친 점은 Insert 시 첫 번째로 매칭되는 자식에 들어간다.
        /// </summary>
        public bool Contains(float x, float y)
            => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;

        /// <summary>
        /// 다른 AABB 와 교차하는지. 닿기만 해도 true.
        /// 이 검사가 거짓이면 그 노드 + 모든 자손을 통째로 스킵할 수 있다 (Quadtree 가속의 핵심).
        /// </summary>
        public bool Intersects(in QuadtreeBounds other)
            => MathF.Abs(CenterX - other.CenterX) <= HalfWidth + other.HalfWidth
            && MathF.Abs(CenterY - other.CenterY) <= HalfHeight + other.HalfHeight;
    }

    /// <summary>
    /// Quadtree 에 저장되는 점 하나. (X, Y) 좌표 + 임의 데이터 T.
    /// 게임에서는 T 가 GameObject id, 적 인덱스, 입자 핸들 같은 게 된다.
    /// </summary>
    public readonly struct QuadtreePoint<T>
    {
        public readonly float X;
        public readonly float Y;
        public readonly T Data;

        public QuadtreePoint(float x, float y, T data)
        {
            X = x;
            Y = y;
            Data = data;
        }
    }

    /// <summary>
    /// Query 호출 결과를 정량화하기 위한 통계.
    /// 시각화 데모에서 "Brute Force vs Quadtree" 가속 효과를 숫자로 보여줄 때 쓴다.
    /// </summary>
    public struct QuadtreeQueryStats
    {
        /// <summary>교차 검사를 통과해 *내부로 들어간* 노드 수 (= 노란색으로 칠해지는 노드).</summary>
        public int NodesVisited;

        /// <summary>교차 검사에서 거부돼 *통째로 스킵된* 노드 수 (= 가속 효과의 핵심).</summary>
        public int NodesPruned;

        /// <summary>실제 점에 대해 Contains 검사를 수행한 횟수.
        /// Brute Force 였다면 항상 = 전체 점 개수. Quadtree 면 보통 그보다 훨씬 작다.</summary>
        public int PointsChecked;
    }

    /// <summary>
    /// =====================================================================
    ///  Quadtree (사분 트리) — 2D 공간 분할 자료구조
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// 2D 공간을 *재귀적으로 4 등분* 하면서 점/객체를 정리하는 트리.
    /// 한 노드가 담을 수 있는 점 수가 capacity 를 초과하면 그 노드의 영역을
    /// 4 개 (북서·북동·남서·남동) 로 나누어 자식 노드를 만들고, 보유 중이던 점들을
    /// 자식들에 재분배한다. 그 후로 그 노드는 *internal* (점을 직접 보관하지 않음) 이 된다.
    ///
    /// 핵심 아이디어 — *공간 자체를 인덱싱해 공간 쿼리를 가속한다*:
    ///   "이 영역과 겹치는 점들을 찾아라" 라는 쿼리(range query)를 Brute Force 로
    ///   하면 모든 점에 대해 한 번씩 검사 → O(N).
    ///   Quadtree 에서는 *영역과 교차하지 않는 노드는 통째로 스킵* 하므로
    ///   결과가 K 개일 때 평균 O(log N + K) 까지 줄어든다 (점들이 균등 분포일 때).
    ///
    /// 본질적으로는 이진 탐색 트리(BST)의 2D 일반화 — BST 가 *값* 을 반으로 가르듯,
    /// Quadtree 는 *공간* 을 4 등분으로 가른다. 3D 로 가면 8 등분 = Octree.
    ///
    /// 2. 동작 흐름
    /// ---------------------------------------------------------------------
    ///   ▷ Insert(p)
    ///     ① 점 p 가 내 영역(_bounds) 밖이면 false 반환 (이 노드의 책임 아님).
    ///     ② 내가 *leaf* 이고 (점 < capacity 또는 깊이 == maxDepth):
    ///          → 그냥 _points 에 추가 (overflow 허용 — 무한 재귀 방지).
    ///     ③ 내가 *leaf* 이고 capacity 초과 + 더 깊어질 수 있음:
    ///          → Subdivide() 로 4 자식 생성, 보유 중이던 점 + 새 점을 자식에 재분배.
    ///     ④ 내가 *internal* (자식이 있음):
    ///          → 4 자식 중 점을 받아주는 첫 자식에 위임.
    ///
    ///   ▷ Subdivide() (Insert 안에서만 호출)
    ///     ① 내 영역을 4 등분해 NW / NE / SW / SE 자식 4 개를 만든다.
    ///     ② 내가 보유 중이던 점들을 각 자식 Insert 로 재분배한다.
    ///     ③ _points 를 비운다 — 이 노드는 더 이상 점을 직접 보관하지 않는다.
    ///
    ///   ▷ Query(range, stats)
    ///     ① 내 영역이 range 와 *교차하지 않으면* 즉시 반환 (= 가속의 핵심, pruning).
    ///     ② 내가 *leaf* 면 보유 점들을 하나씩 검사해 range 안의 점만 결과에 추가.
    ///     ③ 내가 *internal* 이면 4 자식 모두에게 재귀 위임.
    ///
    /// 3. 시간 / 공간 복잡도  (N = 총 점 수, K = 쿼리 결과 수)
    /// ---------------------------------------------------------------------
    ///   - Insert : 평균 O(log N), 최악 O(N) (점이 한 점에 몰려 maxDepth 까지 분할)
    ///   - Query  : 평균 O(log N + K), 최악 O(N)
    ///   - 공간   : O(N) — 점 자체 + 트리 노드 (보통 N 의 작은 상수배)
    ///
    ///   ※ Brute Force 와의 비교:
    ///        매 프레임 N 개 객체끼리 충돌 검사 = O(N²)
    ///        Quadtree 위에서 각 객체의 근처 K 개만 검사 = O(N × log N + N × K)
    ///                                              ≈ O(N log N) (K 가 작을 때)
    ///        N = 1000 이면 N² = 1,000,000 vs N log N ≈ 10,000 → 약 100 배 가속.
    ///
    /// 4. 게임에서의 사용처
    /// ---------------------------------------------------------------------
    ///   - 충돌 검사의 broad-phase
    ///       → "이 객체와 가능성이 있는 충돌 후보만 추리기"
    ///         (precise 한 충돌 판정은 SAT/GJK 등 narrow-phase 가 담당)
    ///   - 시야 / 사정거리 안의 적 찾기
    ///       → "내 주위 R 반경 안의 적 모두 가져오기" 가 매 프레임 빠르게 됨
    ///   - 슈팅 게임의 탄막 / 입자 시뮬레이션
    ///       → 수천 발의 총알을 매 프레임 검사할 때 필수
    ///   - 렌더링의 frustum culling 가속
    ///       → 카메라 절두체와 교차하지 않는 노드를 통째로 스킵
    ///   - 미니맵 / 줌 레벨 별 LOD 표시
    ///       → 깊이 d 까지만 그리면 자연스러운 LOD 가 된다
    ///
    /// 5. 사용 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   - Quadtree&lt;T&gt;[] _children (길이 4)
    ///       : 자식 4 개를 고정 크기 배열로 두는 게 인덱스 (NW=0, NE=1, SW=2, SE=3)
    ///         로 직관적이고 캐시 친화적. List 로 두면 메모리 헤더가 더 붙는다.
    ///   - List&lt;QuadtreePoint&lt;T&gt;&gt; _points (leaf 일 때만 채워짐)
    ///       : 점 추가/순회가 평균 O(1). HashSet 은 좌표 비교 동등성이 까다로워 과한 도구.
    ///   - bool _isSubdivided
    ///       : "_children == null" 으로 대신 검사할 수도 있지만 의도가 코드에 드러나도록
    ///         별도 플래그를 둔다 — 학습 목적에 부합.
    ///   - 별도 visited 집합이 *없다* (BFS 와 다른 점)
    ///       : 트리는 사이클이 없는 비순환 구조이므로 같은 노드를 두 번 방문할 일이 없음.
    ///         Quadtree 는 *그래프 탐색* 이 아니라 *트리 재귀* 다.
    /// </summary>
    public class Quadtree<T>
    {
        // ─────────────────────────────────────────────────────────────
        // 노드의 고정 속성 (생성자에서만 결정)
        // ─────────────────────────────────────────────────────────────

        private readonly QuadtreeBounds _bounds;
        private readonly int _capacity;
        private readonly int _maxDepth;
        private readonly int _depth;

        // ─────────────────────────────────────────────────────────────
        // 노드의 가변 상태
        // ─────────────────────────────────────────────────────────────

        // leaf 일 때만 점들을 직접 보관. 분할되면 자식에 재분배되고 비워진다.
        private readonly List<QuadtreePoint<T>> _points;

        // internal 일 때만 [NW, NE, SW, SE] 4 자식이 채워짐. leaf 면 null.
        // 인덱스 약속: 0=NW, 1=NE, 2=SW, 3=SE.
        private Quadtree<T>[] _children;

        // 이 노드가 분할되었는가 (= internal 노드인가).
        private bool _isSubdivided;

        // ─────────────────────────────────────────────────────────────
        // 시각화 / 디버그용 노출 속성
        //   알고리즘 본체에는 영향을 주지 않지만,
        //   Visualizer 가 트리를 walk 하며 라인을 그릴 때 필요하다.
        // ─────────────────────────────────────────────────────────────

        public QuadtreeBounds Bounds => _bounds;
        public int Depth => _depth;
        public int Capacity => _capacity;
        public bool IsSubdivided => _isSubdivided;
        public IReadOnlyList<QuadtreePoint<T>> Points => _points;
        public IReadOnlyList<Quadtree<T>> Children => _children;

        // ─────────────────────────────────────────────────────────────
        // 생성자
        // ─────────────────────────────────────────────────────────────

        public Quadtree(QuadtreeBounds bounds, int capacity, int maxDepth)
            : this(bounds, capacity, maxDepth, depth: 0) { }

        // 자식 노드 생성용 (Subdivide 안에서만 호출).
        private Quadtree(QuadtreeBounds bounds, int capacity, int maxDepth, int depth)
        {
            _bounds = bounds;
            _capacity = capacity;
            _maxDepth = maxDepth;
            _depth = depth;
            _points = new List<QuadtreePoint<T>>();
            _children = null;
            _isSubdivided = false;
        }

        // ─────────────────────────────────────────────────────────────
        // Insert
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 점 p 를 트리에 삽입한다.
        /// 반환값: 성공(true) / 점이 트리 영역 밖이라 거부됨(false).
        ///
        /// 흐름:
        ///   ① 영역 밖이면 false.
        ///   ② leaf + 여유 있음 → _points 에 추가.
        ///   ③ leaf + 여유 없음 + 더 깊어질 수 있음 → Subdivide() 후 ④ 로 떨어짐.
        ///   ④ internal → 4 자식 중 받아주는 첫 자식에 위임.
        /// </summary>
        public bool Insert(QuadtreePoint<T> point)
        {
            // [1] 내 영역 밖의 점은 내 책임이 아니다.
            //     루트에서 이 검사가 false 면 점이 월드 밖 = 트리 전체에서 거부.
            if (!_bounds.Contains(point.X, point.Y))
            {
                return false;
            }

            // [2] 내가 leaf 일 때
            if (!_isSubdivided)
            {
                // [2-1] 아직 여유가 있거나, 더 이상 깊어질 수 없으면(maxDepth 도달)
                //       그냥 여기에 보관 (= overflow 허용).
                //       maxDepth 도달 후에도 capacity 검사를 강제하면, 점이 한 곳에
                //       몰린 경우 무한히 분할하려 들어 스택 폭발 / 메모리 폭발이 난다.
                if (_points.Count < _capacity || _depth >= _maxDepth)
                {
                    _points.Add(point);
                    return true;
                }

                // [2-2] capacity 초과 + 더 깊어질 수 있음 → 4 분할.
                //       Subdivide 안에서 기존 _points 를 자식에 재분배하므로
                //       이 줄 이후 _points 는 비어 있게 된다.
                Subdivide();
                // 흐름이 [3] 으로 떨어짐 (이제 internal 노드 상태).
            }

            // [3] 내가 internal 일 때 — 자식 중 받아주는 첫 곳에 위임.
            //     "받아주는" 의 정확한 정의 = 자식 _bounds 가 그 점을 Contains 함.
            //     경계선에 정확히 걸친 점은 NW/NE/SW/SE 중 먼저 검사된 자식이 가져간다.
            for (int i = 0; i < 4; i++)
            {
                if (_children[i].Insert(point)) return true;
            }

            // [4] 어떤 자식도 받지 않았다 = 부동소수 오차로 경계가 어긋난 매우 드문 경우.
            //     점을 잃지 않기 위해 안전하게 자기 영역에 보관 (overflow).
            //     이런 일이 자주 발생하면 _bounds 정의에 문제가 있다는 신호.
            _points.Add(point);
            return true;
        }

        /// <summary>
        /// 이 노드의 영역을 4 등분해 NW / NE / SW / SE 자식 노드를 만들고,
        /// 자기가 보관 중이던 점들을 각 자식에 재분배한다.
        /// 분할 후 자기는 internal 노드가 되어 _points 를 비운다.
        /// </summary>
        private void Subdivide()
        {
            // [1] 자식의 반-크기 = 부모의 절반. 중심점을 ±(half/2) 만큼 옮긴다.
            float qw = _bounds.HalfWidth * 0.5f;
            float qh = _bounds.HalfHeight * 0.5f;
            float cx = _bounds.CenterX;
            float cy = _bounds.CenterY;
            int childDepth = _depth + 1;

            _children = new Quadtree<T>[4];

            // 인덱스 약속 (Visualizer 와 일치):
            //   0 = NW  (왼쪽 위 ; X−, Y+)
            //   1 = NE  (오른쪽 위; X+, Y+)
            //   2 = SW  (왼쪽 아래; X−, Y−)
            //   3 = SE  (오른쪽 아래; X+, Y−)
            _children[0] = new Quadtree<T>(new QuadtreeBounds(cx - qw, cy + qh, qw, qh), _capacity, _maxDepth, childDepth);
            _children[1] = new Quadtree<T>(new QuadtreeBounds(cx + qw, cy + qh, qw, qh), _capacity, _maxDepth, childDepth);
            _children[2] = new Quadtree<T>(new QuadtreeBounds(cx - qw, cy - qh, qw, qh), _capacity, _maxDepth, childDepth);
            _children[3] = new Quadtree<T>(new QuadtreeBounds(cx + qw, cy - qh, qw, qh), _capacity, _maxDepth, childDepth);

            _isSubdivided = true;

            // [2] 기존 점들을 자식에 재분배.
            //     foreach 도중 _points 를 변경하면 안 되므로, 미리 list 를 떼어두고 비운다.
            var existing = new List<QuadtreePoint<T>>(_points);
            _points.Clear();

            foreach (var p in existing)
            {
                bool placed = false;
                for (int i = 0; i < 4; i++)
                {
                    if (_children[i].Insert(p)) { placed = true; break; }
                }

                // 어떤 자식도 받지 않았다면(경계 부동소수 오차) 자기 영역에 다시 보관.
                if (!placed) _points.Add(p);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Query (range query — 영역 안의 점 찾기)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 주어진 사각 영역 range 안의 모든 점을 반환한다.
        /// 통계가 필요 없는 일반 사용 경로.
        /// </summary>
        public List<QuadtreePoint<T>> Query(in QuadtreeBounds range)
        {
            var result = new List<QuadtreePoint<T>>();
            QuadtreeQueryStats stats = default;
            QueryRecursive(in range, result, ref stats);
            return result;
        }

        /// <summary>
        /// 통계까지 반환하는 버전. 시각화 데모에서 "방문 노드 / 스킵 노드 / 검사 점" 카운터에 사용.
        /// </summary>
        public List<QuadtreePoint<T>> Query(in QuadtreeBounds range, out QuadtreeQueryStats stats)
        {
            var result = new List<QuadtreePoint<T>>();
            stats = default;
            QueryRecursive(in range, result, ref stats);
            return result;
        }

        private void QueryRecursive(in QuadtreeBounds range, List<QuadtreePoint<T>> result, ref QuadtreeQueryStats stats)
        {
            // [1] 가속의 핵심: 내 영역이 쿼리와 *교차하지 않으면* 자손까지 통째로 스킵.
            //     이 한 줄이 평균 O(N) → O(log N + K) 차이를 만든다.
            if (!_bounds.Intersects(in range))
            {
                stats.NodesPruned++;
                return;
            }

            stats.NodesVisited++;

            // [2] leaf 노드: 직접 보관 중인 점들을 검사.
            //     일반적으로 N 보다 훨씬 작은 수 (capacity 부근 ~ 2*capacity) 만 검사한다.
            if (!_isSubdivided)
            {
                for (int i = 0; i < _points.Count; i++)
                {
                    stats.PointsChecked++;
                    var p = _points[i];
                    if (range.Contains(p.X, p.Y))
                    {
                        result.Add(p);
                    }
                }
                return;
            }

            // [3] internal 노드: 자식 4 개에게 재귀 위임.
            //     일부 자식은 [1] 에서 즉시 거부될 수 있고, 그것이 가속의 본체다.
            for (int i = 0; i < 4; i++)
            {
                _children[i].QueryRecursive(in range, result, ref stats);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Brute Force 비교용 (학습/검증 목적)
        //   같은 결과를 O(N) 으로 얻는 단순 구현. Quadtree.Query 와의
        //   성능/카운터 비교를 위해 시각화 데모에서 호출한다.
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 평면 위 주어진 점들 중 range 안의 점을 모두 모아 반환한다 (O(N)).
        /// Quadtree 가속 효과를 정량 비교하기 위한 baseline.
        /// </summary>
        public static List<QuadtreePoint<T>> BruteForceQuery(IEnumerable<QuadtreePoint<T>> allPoints, in QuadtreeBounds range)
        {
            var result = new List<QuadtreePoint<T>>();
            foreach (var p in allPoints)
            {
                if (range.Contains(p.X, p.Y))
                {
                    result.Add(p);
                }
            }
            return result;
        }
    }
}
