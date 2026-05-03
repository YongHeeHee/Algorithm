using System;
using System.Collections.Generic;

namespace Algorithms.Spatial
{
    // =====================================================================
    //  Spatial Hashing 가 사용하는 2D 사각형 영역 (AABB)
    // =====================================================================
    //
    // ▶ Quadtree 의 QuadtreeBounds 와 형식이 동일하다 (중심 + 반-크기).
    //   원래는 같은 타입을 공유해도 되지만, 이 프로젝트는 "각 알고리즘 파일을
    //   처음부터 끝까지 읽으면 이해되도록" 하는 컨벤션이라 자기 카테고리에
    //   필요한 구조체를 자체 보유한다 — 학습 시 다른 파일을 열지 않아도 된다.
    //
    // ▶ 두 영역 표현 형식 비교
    //     min-max 형식    (xMin, yMin, xMax, yMax)         → Contains/Merge 에 유리
    //     center+halfsize (cx, cy, hw, hh)  ← *이 프로젝트* → 분할/이동/교차에 유리
    //   AABB-AABB 교차 검사:
    //     |c1.x − c2.x| ≤ hw1 + hw2  AND  |c1.y − c2.y| ≤ hh1 + hh2
    public readonly struct SpatialHashBounds
    {
        public readonly float CenterX;
        public readonly float CenterY;
        public readonly float HalfWidth;
        public readonly float HalfHeight;

        public SpatialHashBounds(float centerX, float centerY, float halfWidth, float halfHeight)
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

        public bool Contains(float x, float y)
            => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
    }

    /// <summary>
    /// Spatial Hash 에 저장되는 점 하나. (X, Y) 좌표 + 임의 데이터 T.
    /// 게임에서는 T 가 GameObject id, 적 인덱스, 입자 핸들 같은 게 된다.
    /// </summary>
    public readonly struct SpatialHashPoint<T>
    {
        public readonly float X;
        public readonly float Y;
        public readonly T Data;

        public SpatialHashPoint(float x, float y, T data)
        {
            X = x;
            Y = y;
            Data = data;
        }
    }

    /// <summary>
    /// Query 호출 결과를 정량화하기 위한 통계.
    /// 시각화 데모에서 broad-phase ↔ narrow-phase 의 비용 차이를 보여줄 때 쓴다.
    ///
    ///   - CellsCovered     : 쿼리 영역이 *덮은* 격자 셀 수 (broad-phase 후보 추리기)
    ///   - CandidatesChecked: 그 셀들 안에 있던 점에 대해 Contains 검사한 횟수 (narrow-phase)
    ///   - 최종 결과 점 수는 List 의 Count 로 직접 확인.
    /// </summary>
    public struct SpatialHashQueryStats
    {
        /// <summary>쿼리 영역이 덮는 셀 수 — 빈 셀 포함.</summary>
        public int CellsCovered;

        /// <summary>실제로 점에 대해 Contains 검사한 횟수 (= narrow-phase 비용의 핵심).
        /// Brute Force 였다면 항상 = 전체 점 개수. Spatial Hash 면 보통 그보다 훨씬 작다.</summary>
        public int CandidatesChecked;
    }

    /// <summary>
    /// =====================================================================
    ///  Spatial Hashing — 균등 격자 기반 2D 공간 인덱싱
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// 2D 공간을 *균등한 격자 셀* 로 나누고, 각 셀이 그 안에 있는 점들의 리스트를 보유한다.
    /// 점을 셀에 넣을 때는 좌표를 셀 크기로 나눠 *셀 좌표* 를 구해 (= 해시), 그 셀의 리스트에 추가.
    /// 쿼리 영역이 덮는 셀들만 순회하며 그 안의 점만 정밀 검사 (Contains).
    ///
    /// 핵심 아이디어 — *공간을 균등 격자로 인덱싱해 공간 쿼리를 가속한다*:
    ///   "이 영역과 겹치는 점들을 찾아라" 라는 range query 를 Brute Force O(N) 으로 하지 않고,
    ///   쿼리가 덮는 *몇 개 셀* 만 보면 끝. 각 셀에 평균 N/M 개 점이 있을 때
    ///   결과 K 개를 평균 O(K + 덮인 셀 수 × N/M) 만에 얻는다.
    ///
    /// Quadtree 와 다른 점 — *적응형 vs 균등형*:
    ///   - **Quadtree** : 점이 많은 곳을 *더 깊이* 분할. 점 분포가 편향되어도 자연스럽게 적응.
    ///   - **Spatial Hashing**: 모든 셀이 *같은 크기*. 점 분포가 균등할수록 가속 효과 최대화.
    ///                          매 프레임 객체 위치가 변해도 *셀 좌표만 다시 계산* 하면 끝 →
    ///                          **동적 객체** (탄막, 입자) 에 압도적으로 유리.
    ///
    /// 셀 크기 (cellSize) 는 이 알고리즘의 *유일하지만 결정적인 튜닝 포인트*:
    ///   너무 크면 한 셀에 점이 몰려 → 사실상 Brute Force 회귀.
    ///   너무 작으면 빈 셀이 폭증하고 큰 객체가 여러 셀에 걸쳐 등록 비용 증가.
    ///   경험칙: cellSize ≈ (가장 큰 객체 크기 × 2) 또는 (전형적 쿼리 반경) 정도.
    ///
    /// 2. 동작 흐름
    /// ---------------------------------------------------------------------
    ///   ▷ Insert(p)
    ///     ① 점 p 의 좌표를 셀 크기로 나눠 셀 좌표 (cx, cy) = (⌊p.x/cellSize⌋, ⌊p.y/cellSize⌋) 계산.
    ///     ② Dictionary 에 (cx, cy) 키가 없으면 빈 List 를 새로 만들어 등록.
    ///     ③ 그 List 에 점 p 추가. 끝.
    ///
    ///   ▷ Query(range, stats)
    ///     ① 쿼리 영역의 (MinX, MinY) 와 (MaxX, MaxY) 각각의 셀 좌표를 계산.
    ///     ② 그 직사각형 셀 범위를 이중 for 로 순회.
    ///     ③ 각 셀의 List 가 존재하면, 그 안의 점들에 대해 range.Contains(p) 검사.
    ///        → 통과한 점만 결과에 추가.
    ///     ④ 셀 좌표 검사는 정수 비교라 거의 공짜. 가속의 핵심은 ②③ 의 ratio.
    ///
    ///   ※ 핵심 포인트:
    ///     - "셀 안에 있다" ≠ "쿼리 영역 안에 있다". 셀이 쿼리 영역을 *덮는* 것뿐이라
    ///       narrow-phase 의 Contains 검사가 반드시 필요하다.
    ///     - 이 broad ↔ narrow 의 분리가 시각화에서 후보(candidate, 주황) 와 결과(result, 분홍)
    ///       의 색상 차이로 드러난다.
    ///
    /// 3. 시간 / 공간 복잡도  (N = 총 점 수, M = 총 셀 수, K = 쿼리 결과 수)
    /// ---------------------------------------------------------------------
    ///   - Insert : 평균 O(1)
    ///        Dictionary 의 Hash 계산 + List.Add 모두 평균 O(1).
    ///   - Query  : 평균 O(K + 덮인 셀 수 × N/M)
    ///        균등 분포라면 보통 매우 작은 상수에 가깝다.
    ///   - 공간   : O(N + M)
    ///        점 자체 + 비어 있지 않은 셀의 Dictionary 항목.
    ///
    ///   ※ Quadtree 와의 비교 (N=1000 균등 분포, 작은 쿼리 영역 가정):
    ///        Brute Force         : 1000 회 검사
    ///        Quadtree            : 약 30~50 회 검사 (트리 깊이 ≈ log₄ 1000 ≈ 5)
    ///        Spatial Hashing     : 약 10~30 회 검사 (덮은 셀 수 × 평균 점/셀)
    ///        → 두 자료구조 모두 Brute Force 대비 수십 배 가속. 누가 더 빠르냐는 분포·동적성에 달림.
    ///
    /// 4. 게임에서의 사용처
    /// ---------------------------------------------------------------------
    ///   - 슈팅 게임의 탄막 / 입자 시뮬레이션 (수천 개 객체가 매 프레임 *움직임*)
    ///       → 트리 재구축 비용 없이 셀 좌표만 갱신
    ///   - MMO 의 관심 영역 (Area of Interest)
    ///       → "내 주변 N 미터 안의 다른 플레이어" 를 매 프레임 갱신
    ///   - Boids / 군집 시뮬레이션
    ///       → 새/물고기 떼의 *근접 이웃 평균 속도* 계산
    ///   - Unity 의 Physics2D.OverlapBox / OverlapCircle
    ///       → 내부적으로 비슷한 균등 격자 broad-phase 사용
    ///
    /// 5. 사용 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   - Dictionary&lt;(int, int), List&lt;Point&gt;&gt;
    ///       : 셀 좌표 → 그 셀의 점 리스트.
    ///         배열 [width × height] 로도 가능하지만 월드 크기가 *런타임에 변할 수 있는*
    ///         경우 (스트리밍 월드, 무한 맵) Dictionary 가 유연하다. 빈 셀은 키 없음 = 메모리 0.
    ///   - List&lt;SpatialHashPoint&lt;T&gt;&gt;
    ///       : 한 셀이 보관하는 점들. Add/순회가 평균 O(1).
    ///   - (int, int) ValueTuple 키
    ///       : 두 정수를 그대로 묶어 Dictionary 키로 사용. struct 라 박싱 없음.
    ///         Vector2Int 로도 가능하지만 ValueTuple 이 라이트하고 GetHashCode 가 자동.
    ///   - 별도 visited 집합이 *없다* (BFS 와 다른 점, Quadtree 와 같음)
    ///       : 한 점은 정확히 한 셀에만 들어가므로 같은 점을 두 번 검사할 일이 없다.
    /// </summary>
    public class SpatialHash<T>
    {
        // ─────────────────────────────────────────────────────────────
        // 고정 속성 (생성자에서만 결정)
        // ─────────────────────────────────────────────────────────────

        private readonly float _cellSize;

        // ─────────────────────────────────────────────────────────────
        // 가변 상태 — 셀 좌표 → 점 리스트
        // ─────────────────────────────────────────────────────────────

        // 비어 있지 않은 셀만 키로 등록된다. 빈 셀은 Dictionary 에 존재하지 않음 → 메모리 절약.
        private readonly Dictionary<(int cellX, int cellY), List<SpatialHashPoint<T>>> _cells = new();

        // ─────────────────────────────────────────────────────────────
        // 시각화 / 디버그용 노출 속성
        // ─────────────────────────────────────────────────────────────

        public float CellSize => _cellSize;

        /// <summary>비어 있지 않은 셀들의 (좌표 → 점 리스트) 맵.</summary>
        public IReadOnlyDictionary<(int cellX, int cellY), List<SpatialHashPoint<T>>> Cells => _cells;

        /// <summary>비어 있지 않은 셀의 개수.</summary>
        public int OccupiedCellCount => _cells.Count;

        // ─────────────────────────────────────────────────────────────
        // 생성자
        // ─────────────────────────────────────────────────────────────

        public SpatialHash(float cellSize)
        {
            if (cellSize <= 0f)
                throw new ArgumentException("cellSize must be positive", nameof(cellSize));
            _cellSize = cellSize;
        }

        // ─────────────────────────────────────────────────────────────
        // 좌표 → 셀 좌표 (Hash)
        //   Floor 후 (int) 캐스팅 순서가 중요 — (int)(-0.5) = 0 이 되어버려
        //   음수 좌표가 잘못된 셀에 들어가게 됨. 반드시 Floor 먼저.
        // ─────────────────────────────────────────────────────────────

        private (int cellX, int cellY) Hash(float x, float y)
        {
            return ((int)MathF.Floor(x / _cellSize),
                    (int)MathF.Floor(y / _cellSize));
        }

        // ─────────────────────────────────────────────────────────────
        // Insert
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 점 p 를 격자에 등록한다.
        /// 흐름:
        ///   ① 점 좌표를 셀 크기로 나눠 셀 좌표 (cx, cy) 계산.
        ///   ② Dictionary 에 키가 없으면 빈 List 를 새로 만든다.
        ///   ③ List 에 점을 추가한다.
        /// </summary>
        public void Insert(SpatialHashPoint<T> point)
        {
            // [1] 점 좌표를 셀 좌표로 변환 (정수 두 개).
            var key = Hash(point.X, point.Y);

            // [2] 그 셀의 List 가 아직 없으면 만든다.
            //     TryGetValue + Add 두 단계로 나누는 것이 GetOrAdd 보다 분기가 명확해서 학습용 코드에 적합.
            if (!_cells.TryGetValue(key, out var list))
            {
                list = new List<SpatialHashPoint<T>>();
                _cells[key] = list;
            }

            // [3] 점을 그 셀의 List 에 추가.
            list.Add(point);
        }

        // ─────────────────────────────────────────────────────────────
        // Query (range query — 영역 안의 점 찾기)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 주어진 사각 영역 range 안의 모든 점을 반환한다.
        /// 통계가 필요 없는 일반 사용 경로.
        /// </summary>
        public List<SpatialHashPoint<T>> Query(in SpatialHashBounds range)
        {
            return Query(in range, out _);
        }

        /// <summary>
        /// 통계까지 반환하는 버전. 시각화 데모에서 broad↔narrow 비용 카운터에 사용.
        ///
        /// 흐름:
        ///   ① 쿼리 영역의 좌하/우상 모서리를 셀 좌표로 변환 → 덮을 셀 좌표 범위 결정.
        ///   ② 그 직사각형 셀 범위를 이중 for 로 순회.
        ///   ③ 각 셀의 List 가 존재하면 점들을 하나씩 검사해 range 안이면 결과에 추가.
        /// </summary>
        public List<SpatialHashPoint<T>> Query(in SpatialHashBounds range, out SpatialHashQueryStats stats)
        {
            var result = new List<SpatialHashPoint<T>>();
            stats = default;

            // [1] 쿼리 영역의 모서리 두 점을 셀 좌표로 변환.
            //     이 두 (cx, cy) 가 직사각형 셀 범위의 양 끝.
            var (xMin, yMin) = Hash(range.MinX, range.MinY);
            var (xMax, yMax) = Hash(range.MaxX, range.MaxY);

            // [2] 덮은 셀들을 이중 for 로 순회.
            for (int cx = xMin; cx <= xMax; cx++)
            {
                for (int cy = yMin; cy <= yMax; cy++)
                {
                    // 덮은 셀의 수 = 빈 셀 포함. 통계용으로 카운트.
                    stats.CellsCovered++;

                    // [3] 셀이 비어 있으면 점도 없음 → 스킵.
                    if (!_cells.TryGetValue((cx, cy), out var list)) continue;

                    // [4] 셀 안의 모든 점에 대해 narrow-phase 검사.
                    //     ※ "셀 안에 있다" ≠ "쿼리 영역 안에 있다" — 셀이 쿼리를 *덮을* 뿐이라
                    //       Contains 정밀 검사가 반드시 필요하다.
                    for (int i = 0; i < list.Count; i++)
                    {
                        stats.CandidatesChecked++;
                        var p = list[i];
                        if (range.Contains(p.X, p.Y))
                        {
                            result.Add(p);
                        }
                    }
                }
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────
        // Clear (시각화 Restart 시 사용)
        // ─────────────────────────────────────────────────────────────

        public void Clear() => _cells.Clear();

        // ─────────────────────────────────────────────────────────────
        // Brute Force 비교용 (학습/검증 목적)
        //   같은 결과를 O(N) 으로 얻는 단순 구현. SpatialHash.Query 와의
        //   성능/카운터 비교를 위해 시각화 데모에서 호출한다.
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 평면 위 주어진 점들 중 range 안의 점을 모두 모아 반환한다 (O(N)).
        /// Spatial Hashing 가속 효과를 정량 비교하기 위한 baseline.
        /// </summary>
        public static List<SpatialHashPoint<T>> BruteForceQuery(IEnumerable<SpatialHashPoint<T>> allPoints, in SpatialHashBounds range)
        {
            var result = new List<SpatialHashPoint<T>>();
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
