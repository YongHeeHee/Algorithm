using System;

namespace Algorithms.Physics
{
    /// <summary>
    /// =====================================================================
    ///  AABB (Axis-Aligned Bounding Box) — 충돌 검사의 원자 단위
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// AABB 는 *축에 평행한* (회전 없는) 직사각형/박스. 게임의 모든 충돌 시스템이
    /// 가장 먼저 만나는 *원자 단위* 다. 회전된 OBB / 폴리곤 / 곡선 충돌 같은 비싼 검사 전에
    /// AABB 가 먼저 안 겹친다는 걸 확인하면 정밀 검사 자체를 스킵할 수 있어
    /// **broad-phase 1 차 필터** 로 거의 모든 곳에 등장한다.
    ///
    /// 두 가지 표현 — 같은 사각형을 두 형식으로 표현 가능하지만 용도가 다르다:
    ///   · min-max 형식    (xMin, yMin, xMax, yMax)         → Contains / Merge 에 유리
    ///   · center+halfsize (cx, cy, hw, hh)  ← *이 프로젝트* → 분할 / 이동 / 교차 검사에 유리
    /// 이 프로젝트의 Quadtree (`QuadtreeBounds`) / Spatial Hashing (`SpatialHashBounds`) 도
    /// 같은 형식이라 자료구조 *내부 부품* 으로 AABB 를 이미 쓰고 있다. 이 페이지는 *연산 자체* 가 주제.
    ///
    /// 핵심 3 연산:
    ///   ① Contains(점)         : 점이 박스 안에 있는가
    ///   ② Overlaps(다른 박스)  : 두 박스가 겹치는가
    ///   ③ Raycast (Slab method): 광선이 박스를 통과하는가 (이게 가장 비자명)
    ///
    /// 2. 동작 흐름
    /// ---------------------------------------------------------------------
    ///   ▷ Contains(x, y)
    ///     ① x 가 [MinX, MaxX] 안에 있고 y 가 [MinY, MaxY] 안에 있으면 true.
    ///     ② 그 외에는 false. 한 줄짜리 검사.
    ///
    ///   ▷ Overlaps(other)
    ///     ① 중심점 사이 거리의 *각 축 성분* 이 두 박스의 반-크기 합 이하면 그 축에서 겹친다.
    ///     ② 모든 축에서 겹치면 두 박스는 겹치는 것 (= 분리 축이 없음).
    ///     ③ 한 축이라도 거리가 반-크기 합을 초과하면 *그 축이 분리 축* 이고 두 박스는 안 겹친다.
    ///        → 이 \"분리 축\" 개념이 SAT (Separating Axis Theorem) 의 시작점.
    ///
    ///   ▷ Raycast — Slab method
    ///     광선을 r(t) = origin + t · direction (t ≥ 0) 으로 표현할 때, 박스를 *두 슬랩 (slab)* 의 교집합으로 본다:
    ///       · X 슬랩: [MinX, MaxX] × (-∞, +∞)
    ///       · Y 슬랩: (-∞, +∞) × [MinY, MaxY]
    ///     광선이 박스 안에 있으려면 *두 슬랩 모두에* 있어야 한다.
    ///     각 슬랩에 대해 광선이 들어가는 시각 t_enter 와 나오는 시각 t_exit 를 구한다.
    ///     ① X 슬랩 진입/이탈: t1x = (MinX − originX) / dirX,  t2x = (MaxX − originX) / dirX
    ///        dirX 가 음수면 t1x, t2x 를 swap (작은 값이 진입, 큰 값이 이탈).
    ///     ② Y 슬랩도 마찬가지로 t1y, t2y 계산.
    ///     ③ 두 슬랩 모두 안에 있는 구간:
    ///          tEnter = max(tEnterX, tEnterY)   ← 늦게 들어간 슬랩의 진입 시각
    ///          tExit  = min(tExitX, tExitY)     ← 먼저 나가는 슬랩의 이탈 시각
    ///     ④ 광선이 박스를 통과 ⇔ tEnter ≤ tExit 이고 tExit ≥ 0.
    ///        (tExit < 0 이면 박스가 광선 *뒤쪽* 에 있다는 뜻 → hit 아님)
    ///
    ///   ※ 핵심 통찰:
    ///     \"각 축의 진입/이탈 구간을 *교집합* 한 게 박스 통과 구간\" — 이 한 줄이 Slab method 의 전부다.
    ///     이 통찰 없이 코드만 보면 왜 max/min 이 등장하는지 비자명하다.
    ///
    /// 3. 시간 / 공간 복잡도
    /// ---------------------------------------------------------------------
    ///   - Contains : O(1) — 비교 4 번
    ///   - Overlaps : O(1) — 거리 비교 2 번
    ///   - Raycast  : O(1) — 나눗셈 4 번 + max/min 2 번
    ///   - 공간     : O(1) — 박스는 4 float 만 차지
    ///
    /// 4. 게임에서의 사용처
    /// ---------------------------------------------------------------------
    ///   - **broad-phase 1 차 필터** : Quadtree / Spatial Hashing 의 셀마다 AABB 보관,
    ///                                 narrow-phase (SAT, GJK) 가 비싸므로 AABB 가 안 겹치면 정밀 검사 자체를 스킵
    ///   - **카메라 frustum culling** : 객체 AABB ↔ 절두체 6 평면 검사
    ///   - **선택 박스 (RTS)**         : 마우스 드래그 영역 안의 유닛 = AABB-AABB Overlap 한 번
    ///   - **FPS 라인 오브 사이트**    : Slab Raycast 로 \"적과 나 사이에 벽이 있는가\"
    ///   - **타일맵 충돌**             : 타일 = AABB, 캐릭터 박스 ↔ 타일 박스 Overlaps
    ///   - **픽셀 충돌의 1 차 필터**   : 정밀 픽셀 비교 전에 AABB 로 후보 추리기
    ///
    /// 5. 사용 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   - readonly struct (불변)
    ///       : AABB 는 박스 *그 자체* — 위치를 \"바꾸는\" 게 아니라 새 박스를 \"만드는\" 의미가 자연스럽다.
    ///         readonly struct 는 박싱도 없고, 함수 인자로 in 키워드로 넘기면 복사도 없다.
    ///   - 4 float (CenterX, CenterY, HalfWidth, HalfHeight)
    ///       : center + halfsize 형식. 4 분할 / 이동 / 교차 검사가 모두 *대칭적* 으로 표현된다.
    ///   - AABBRaycastResult (struct)
    ///       : Hit 여부 + 4 개 t 값 + 진입/이탈 t. 시각화에서 모든 중간 값을 볼 수 있도록 모두 노출.
    ///   - 별도 자료구조 *없음* (트리도, 해시맵도 없음)
    ///       : AABB 는 *연산이 자료구조* 다. 박스 하나가 모든 정보.
    /// </summary>
    public readonly struct AABB
    {
        public readonly float CenterX;
        public readonly float CenterY;
        public readonly float HalfWidth;
        public readonly float HalfHeight;

        public AABB(float centerX, float centerY, float halfWidth, float halfHeight)
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

        // ─────────────────────────────────────────────────────────────
        // [연산 1] Contains — 점이 박스 안인가
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 점 (x, y) 가 박스 안에 있는지. 경계선 위는 포함 (closed interval).
        /// </summary>
        public bool Contains(float x, float y)
            => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;

        // ─────────────────────────────────────────────────────────────
        // [연산 2] Overlaps — 두 박스가 겹치는가
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 다른 AABB 와 겹치는지. 닿기만 해도 true.
        ///
        /// 원리: 중심점 사이 거리의 *각 축 성분* 이 두 박스의 반-크기 합 이하면 그 축에서 겹친다.
        ///       모든 축에서 겹치면 두 박스가 겹친다 (= 분리 축이 없음).
        /// </summary>
        public bool Overlaps(in AABB other)
            => MathF.Abs(CenterX - other.CenterX) <= HalfWidth + other.HalfWidth
            && MathF.Abs(CenterY - other.CenterY) <= HalfHeight + other.HalfHeight;

        /// <summary>
        /// Overlaps 가 false 일 때 *어느 축이 분리 축인지* 반환.
        ///   0  = X 축에서 분리됨
        ///   1  = Y 축에서 분리됨
        ///  -1  = 분리 축 없음 (= 겹침)
        ///
        /// SAT (Separating Axis Theorem) 의 가장 단순한 형태. 회전된 박스로 가면 검사할 축이
        /// 늘어나지만 \"한 축이라도 분리되면 안 겹친다\" 라는 핵심 논리는 같다.
        /// </summary>
        public int GetSeparatingAxis(in AABB other)
        {
            float dx = MathF.Abs(CenterX - other.CenterX);
            float dy = MathF.Abs(CenterY - other.CenterY);
            float xLimit = HalfWidth + other.HalfWidth;
            float yLimit = HalfHeight + other.HalfHeight;

            // 두 축 모두 검사한 뒤 *더 크게 분리된* 축을 반환하는 것이 시각적으로 자연스럽다.
            // (단순히 \"먼저 발견된\" 축을 반환하면 X 축으로만 분리된 것처럼 보일 수 있음)
            float xExcess = dx - xLimit;
            float yExcess = dy - yLimit;

            if (xExcess <= 0f && yExcess <= 0f) return -1;     // 두 축 모두 안 분리 → 겹침
            if (xExcess > yExcess) return 0;                   // X 축이 더 분리됨
            return 1;                                          // Y 축이 더 분리됨
        }

        // ─────────────────────────────────────────────────────────────
        // [연산 3] Slab Raycast — 광선이 박스를 통과하는가
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 광선 r(t) = origin + t · direction 가 이 박스를 통과하는지 검사한다.
        ///
        /// **Slab method** 의 핵심 통찰:
        ///   박스 = X 슬랩 ∩ Y 슬랩
        ///   광선이 박스 안에 있는 t 구간 = (X 슬랩 안의 t 구간) ∩ (Y 슬랩 안의 t 구간)
        ///
        /// 흐름:
        ///   ① X 슬랩 진입/이탈 시각 t1x, t2x 계산. dirX 부호에 따라 swap.
        ///   ② Y 슬랩 진입/이탈 시각 t1y, t2y 계산. dirY 부호에 따라 swap.
        ///   ③ tEnter = max(t1x, t1y),  tExit = min(t2x, t2y).
        ///   ④ Hit ⇔ tEnter ≤ tExit ∧ tExit ≥ 0.
        ///
        /// direction 은 *정규화 안 해도 동작* 하지만, 정규화된 경우 t 가 \"월드 거리\" 의미라 직관적이다.
        /// </summary>
        public AABBRaycastResult Raycast(float originX, float originY, float dirX, float dirY)
        {
            var result = new AABBRaycastResult();

            // [1] X 슬랩 — dirX 가 0 이면 광선이 X 슬랩 경계에 평행.
            //     이 경우 origin.x 가 슬랩 안이면 모든 t 에서 안에 있고, 밖이면 절대 못 들어감.
            if (MathF.Abs(dirX) < 1e-7f)
            {
                if (originX < MinX || originX > MaxX)
                {
                    // 평행 + 슬랩 밖 → 절대 hit 아님.
                    result.Hit = false;
                    result.TEnterX = float.PositiveInfinity;
                    result.TExitX  = float.NegativeInfinity;
                    // Y 슬랩 값도 계산은 해두지만 어차피 Hit 는 false 로 결정.
                }
                else
                {
                    // 평행 + 슬랩 안 → 모든 t 에서 X 슬랩 안.
                    result.TEnterX = float.NegativeInfinity;
                    result.TExitX  = float.PositiveInfinity;
                }
            }
            else
            {
                float t1 = (MinX - originX) / dirX;
                float t2 = (MaxX - originX) / dirX;
                if (t1 > t2) (t1, t2) = (t2, t1);              // dirX < 0 일 때 swap
                result.TEnterX = t1;
                result.TExitX  = t2;
            }

            // [2] Y 슬랩 — 동일 패턴.
            if (MathF.Abs(dirY) < 1e-7f)
            {
                if (originY < MinY || originY > MaxY)
                {
                    result.Hit = false;
                    result.TEnterY = float.PositiveInfinity;
                    result.TExitY  = float.NegativeInfinity;
                }
                else
                {
                    result.TEnterY = float.NegativeInfinity;
                    result.TExitY  = float.PositiveInfinity;
                }
            }
            else
            {
                float t1 = (MinY - originY) / dirY;
                float t2 = (MaxY - originY) / dirY;
                if (t1 > t2) (t1, t2) = (t2, t1);
                result.TEnterY = t1;
                result.TExitY  = t2;
            }

            // [3] 두 슬랩의 교집합 = 박스 통과 구간.
            //     늦게 들어간 슬랩의 진입 시각이 박스 진입,
            //     먼저 나가는 슬랩의 이탈 시각이 박스 이탈.
            result.TEnter = MathF.Max(result.TEnterX, result.TEnterY);
            result.TExit  = MathF.Min(result.TExitX,  result.TExitY);

            // [4] Hit 판정: 진입이 이탈보다 빠르고 (구간이 비어있지 않고),
            //     박스가 광선 *앞쪽* 에 있어야 함 (TExit ≥ 0).
            result.Hit = result.TEnter <= result.TExit && result.TExit >= 0f;

            return result;
        }
    }

    /// <summary>
    /// Slab Raycast 결과. Hit 여부 + 4 개의 슬랩 t 값 + 통합된 진입/이탈 t.
    /// 시각화에서 모든 중간 값을 화면에 표시할 수 있도록 전부 공개한다.
    ///
    /// 사용 예:
    ///   if (result.Hit) {
    ///       Vector2 entry = origin + dir * result.TEnter;  // 박스 진입 지점
    ///       Vector2 exit  = origin + dir * result.TExit;   // 박스 이탈 지점
    ///   }
    /// </summary>
    public struct AABBRaycastResult
    {
        public bool Hit;

        /// <summary>X 슬랩 진입 시각 (= 광선이 [MinX, MaxX] 범위에 들어가는 t).</summary>
        public float TEnterX;
        /// <summary>X 슬랩 이탈 시각.</summary>
        public float TExitX;
        /// <summary>Y 슬랩 진입 시각.</summary>
        public float TEnterY;
        /// <summary>Y 슬랩 이탈 시각.</summary>
        public float TExitY;

        /// <summary>박스 진입 시각 = max(TEnterX, TEnterY).</summary>
        public float TEnter;
        /// <summary>박스 이탈 시각 = min(TExitX, TExitY).</summary>
        public float TExit;
    }
}
