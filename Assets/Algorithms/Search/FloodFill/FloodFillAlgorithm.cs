using System.Collections.Generic;

namespace Algorithms.Search
{
    /// <summary>
    /// =====================================================================
    ///  Flood Fill (영역 채우기)
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// 격자(2차원 배열) 위에서 시작 셀과 "같은 값" 을 가진 4-연결(혹은 8-연결) 인접 셀들을
    /// 모두 새로운 값(newValue)으로 교체하는 알고리즘.
    ///
    /// 본질적으로는 BFS / DFS 의 *그리드 응용판* 이다.
    ///   · 정점(Vertex) = 격자의 한 셀
    ///   · 간선(Edge)   = 같은 값을 가진 인접 셀로의 이동 가능성
    /// 그래프가 외부에 따로 존재하지 않고, "셀 값이 같은가?" 라는 *값 매칭 조건* 이
    /// 간선을 동적으로 만들어내는 것이 다른 점이다.
    ///
    /// BFS / DFS 와의 결정적 차이:
    ///   - BFS / DFS : 미리 만들어진 Graph&lt;T&gt; 의 인접 리스트를 따라간다.
    ///   - Flood Fill: 그래프 자체가 없다. 4 방향 이웃을 즉석에서 살피고
    ///                  "같은 색이면 같은 영역" 이라는 규칙으로 연결을 판단한다.
    ///
    /// 2. 동작 흐름 (Queue 기반 반복 — 권장)
    /// ---------------------------------------------------------------------
    ///   ① 시작 셀의 값을 targetValue 로 기억해 둔다.
    ///   ② targetValue == newValue 면 무한 루프가 발생하므로 즉시 종료.
    ///   ③ 시작 셀을 큐에 넣고, 동시에 newValue 로 교체한다.
    ///        ※ '큐에 넣는 시점에 교체' = '방문 표시' 와 동일한 효과.
    ///          별도 visited 집합이 필요 없다 (BFS 와의 미묘한 차이).
    ///   ④ 큐가 빌 때까지 반복:
    ///        a. 큐에서 셀 하나를 꺼낸다.
    ///        b. 결과 리스트(채워진 셀 목록)에 기록한다.
    ///        c. 4 방향 이웃을 살펴, 격자 안 + targetValue 와 같은 값이면:
    ///             · newValue 로 교체 (방문 표시 + 결과 반영을 한 번에)
    ///             · 큐에 넣는다.
    ///   ⑤ 큐가 비면, 시작 셀과 4-연결로 이어진 모든 같은 값 셀이 newValue 로 바뀐 상태.
    ///
    ///   ※ 핵심 포인트:
    ///     - "큐에 넣는 순간" 에 newValue 로 즉시 교체하는 것이 정석.
    ///       꺼낼 때 교체하면 같은 셀이 큐에 여러 번 들어가 중복 처리 + 성능 저하.
    ///     - HashSet&lt;T&gt; 가 필요 없다. 격자 자체가 방문 여부를 기억한다
    ///       (newValue 로 바뀐 셀은 더 이상 targetValue 와 일치하지 않으므로 자연스럽게 걸러짐).
    ///
    /// 3. 시간 / 공간 복잡도  (N = 격자 셀 수, W·H = 가로·세로)
    /// ---------------------------------------------------------------------
    ///   - 시간 : O(N)
    ///        각 셀은 큐에 최대 1회 들어가고, 4 방향 검사는 상수 시간.
    ///        단, "채워지는 영역 크기" 만큼만 실제로 처리된다 (영역 외 셀은 건드리지 않음).
    ///   - 공간 : O(N)
    ///        Queue 가 최악의 경우 모든 셀을 보관할 수 있다 (예: 격자 전체가 한 영역인 경우).
    ///        재귀 버전은 호출 스택 깊이가 영역 크기에 비례 → 큰 영역에서 StackOverflowException 위험.
    ///
    /// 4. 게임에서의 사용처
    /// ---------------------------------------------------------------------
    ///   - 페인트 도구 (그림판의 '페인트 통' 기능)
    ///       → 클릭한 픽셀과 같은 색 영역을 한 번에 다른 색으로 교체.
    ///   - 같은 색 블록 매칭 (애니팡 / 캔디크러시류)
    ///       → 3개 이상 연결된 같은 색 그룹을 찾아 제거.
    ///   - 영역 점령 / 지형 칠하기 (스플래툰, 헥사 점령 게임)
    ///       → 플레이어 색이 인접한 같은 색 영역으로 번져 나가는 효과.
    ///   - 지도의 연결 구역(Connected Region) 식별
    ///       → 섬 / 호수 / 대륙 자동 라벨링, 던전의 방 분리.
    ///   - AOE / 가스 / 불 확산 시뮬레이션
    ///       → 한 셀에서 시작된 효과가 같은 속성을 가진 인접 셀로 퍼지는 연출.
    ///   - 마인스위퍼의 '빈 칸 자동 열기'
    ///       → 클릭한 빈 칸과 4-연결된 모든 빈 칸을 한 번에 공개.
    ///
    /// 5. 사용한 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   - Queue&lt;T&gt;       : FIFO. 시작점에서 가까운 셀부터 채워나간다 → 동심원 모양으로 퍼짐.
    ///                       Stack(LIFO) 으로 바꾸면 한 방향으로 깊게 파고드는 모양이 된다
    ///                       (= DFS 기반 Flood Fill). 결과(채워진 영역)는 어느 쪽이든 동일.
    ///   - T[,] (2D 배열)  : 격자의 셀 값을 직접 저장. 인덱스 [x, y] 로 O(1) 접근.
    ///                       Dictionary 보다 빠르고, 메모리도 인접 리스트보다 적게 든다.
    ///   - List&lt;(int x, int y)&gt; : 채워진 셀의 좌표를 *채워진 순서대로* 누적.
    ///                       시각화 (한 스텝씩 보기) 와 후처리 (예: 점수 계산) 에 활용.
    ///
    ///   ※ HashSet 이 필요 없다 — 이게 BFS / DFS 와의 가장 큰 코드상 차이.
    ///     격자 셀의 값을 newValue 로 바꾸는 행위 자체가 '방문 처리' 역할을 한다.
    /// </summary>
    public static class FloodFillAlgorithm
    {
        // 그리드 4 방향 이웃 오프셋 (상하좌우).
        // 8 방향(대각선 포함)으로 확장하려면 (1,1), (1,-1), (-1,1), (-1,-1) 을 추가하면 된다.
        private static readonly (int dx, int dy)[] FourDirections =
        {
            ( 1,  0),
            (-1,  0),
            ( 0,  1),
            ( 0, -1),
        };

        /// <summary>
        /// 시작 좌표(startX, startY)와 같은 값을 가진 4-연결 영역을 모두 newValue 로 교체한다.
        /// 반환 리스트는 채워진 셀의 좌표를 *채워진 순서(BFS 순서)* 대로 담고 있다.
        /// 시작점이 격자 밖이거나 targetValue == newValue 이면 빈 리스트를 반환.
        ///
        /// ※ grid 는 in-place 로 변경된다. 원본을 보존하려면 복사본을 넘길 것.
        /// </summary>
        public static List<(int x, int y)> Fill<T>(T[,] grid, int startX, int startY, T newValue)
        {
            // ───────────────────────────────────────────────────────────
            // [1] 결과 컨테이너
            //     채워진 셀을 채워진 순서대로 누적. 시각화에서 한 스텝씩 그리기에 적합.
            // ───────────────────────────────────────────────────────────
            var filled = new List<(int x, int y)>();

            // [2] 격자 크기 추출. T[,] 는 GetLength(0) = 첫 번째 차원 크기.
            int width  = grid.GetLength(0);
            int height = grid.GetLength(1);

            // [3] 시작 좌표가 격자 밖이면 채울 게 없다.
            if (startX < 0 || startX >= width || startY < 0 || startY >= height)
            {
                return filled;
            }

            // ───────────────────────────────────────────────────────────
            // [4] 시작 셀의 값 = targetValue.
            //     이 값과 같은 값을 가진 인접 셀들만 채우는 대상이 된다.
            //     제네릭 T 의 동등 비교는 == 가 아닌 EqualityComparer&lt;T&gt;.Default 사용
            //     (T 가 어떤 타입이든 안전하게 비교).
            // ───────────────────────────────────────────────────────────
            T targetValue = grid[startX, startY];
            var comparer  = EqualityComparer<T>.Default;

            // [5] 무한 루프 방지: target == newValue 이면 교체할 게 없으므로 즉시 종료.
            //     이 검사가 없으면 큐에 넣고 → 같은 값으로 교체 → 다시 같은 값으로 인식 → 무한 enqueue.
            if (comparer.Equals(targetValue, newValue))
            {
                return filled;
            }

            // ───────────────────────────────────────────────────────────
            // [6] Flood Fill 의 심장: 큐(FIFO)
            //     '먼저 발견된 셀' 을 '먼저 처리' 해야 동심원처럼 자연스럽게 퍼진다.
            //     Stack 으로 바꾸면 한 방향으로 깊게 들어가는 모양이 된다 (= DFS 기반 Flood Fill).
            //     채워지는 *결과* 는 어느 쪽이든 동일하지만, 시각적으로는 매우 다르다.
            // ───────────────────────────────────────────────────────────
            var queue = new Queue<(int x, int y)>();

            // [7] 시작 셀을 큐에 넣고, 동시에 newValue 로 교체.
            //     ※ 교체하는 행위 자체가 '방문 표시' 역할.
            //       이후 같은 셀이 이웃 검사에 걸려도 grid[x,y] != targetValue 이므로
            //       자연스럽게 건너뛰어진다 → 별도 HashSet 이 필요 없다.
            queue.Enqueue((startX, startY));
            grid[startX, startY] = newValue;

            // ───────────────────────────────────────────────────────────
            // [8] 큐가 빌 때까지 반복.
            // ───────────────────────────────────────────────────────────
            while (queue.Count > 0)
            {
                // [8-1] 큐의 가장 앞 셀을 꺼낸다. 이 셀은 이미 newValue 로 바뀌어 있다.
                var (x, y) = queue.Dequeue();

                // [8-2] 결과 리스트에 기록 = '처리 완료' 표시.
                filled.Add((x, y));

                // [8-3] 4 방향 이웃 검사.
                foreach (var (dx, dy) in FourDirections)
                {
                    int nx = x + dx;
                    int ny = y + dy;

                    // 격자 밖이면 건너뛴다.
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;

                    // 이웃의 값이 targetValue 와 다르면 = 다른 영역 → 건너뛴다.
                    // 이미 처리된 같은-영역 셀도 newValue 로 바뀌어 있으므로 여기서 같이 걸러진다
                    // (HashSet 없이도 중복 방문이 자연스럽게 차단되는 이유).
                    if (!comparer.Equals(grid[nx, ny], targetValue)) continue;

                    // 처음 발견한 같은-영역 셀 → newValue 로 즉시 교체 + 큐에 넣기.
                    // (꺼낼 때 교체하면 같은 셀이 큐에 여러 번 들어가 성능이 떨어진다.)
                    grid[nx, ny] = newValue;
                    queue.Enqueue((nx, ny));
                }
            }

            // [9] 채워진 셀 목록 반환. 시각화 측은 이 리스트를 한 칸씩 그리며 애니메이션화 가능.
            return filled;
        }

        /// <summary>
        /// Flood Fill 의 재귀 버전.
        /// 코드가 매우 짧아 알고리즘의 본질이 직관적으로 드러난다는 장점이 있다.
        /// 단, 영역이 매우 크면 호출 스택이 터질 수 있으므로 학습/소규모 격자 용.
        ///
        /// (DFSAlgorithm.SearchRecursive 와 동일한 트레이드오프를 가진다.)
        /// </summary>
        public static List<(int x, int y)> FillRecursive<T>(T[,] grid, int startX, int startY, T newValue)
        {
            var filled = new List<(int x, int y)>();
            int width  = grid.GetLength(0);
            int height = grid.GetLength(1);

            if (startX < 0 || startX >= width || startY < 0 || startY >= height)
            {
                return filled;
            }

            T targetValue = grid[startX, startY];
            var comparer  = EqualityComparer<T>.Default;

            // target == new 이면 무한 재귀 방지 위해 즉시 종료.
            if (comparer.Equals(targetValue, newValue))
            {
                return filled;
            }

            FillRecursiveStep(grid, startX, startY, targetValue, newValue, comparer, filled);
            return filled;
        }

        private static void FillRecursiveStep<T>(
            T[,] grid, int x, int y,
            T targetValue, T newValue,
            EqualityComparer<T> comparer,
            List<(int x, int y)> filled)
        {
            int width  = grid.GetLength(0);
            int height = grid.GetLength(1);

            // 격자 밖이면 종료.
            if (x < 0 || x >= width || y < 0 || y >= height) return;

            // 다른 영역(혹은 이미 채워진 셀) 이면 종료.
            // 채워진 셀은 grid[x,y] 가 newValue 로 바뀌어 있으므로 자연스럽게 걸러진다.
            if (!comparer.Equals(grid[x, y], targetValue)) return;

            // 현재 셀을 채우고 결과에 기록.
            grid[x, y] = newValue;
            filled.Add((x, y));

            // 4 방향으로 재귀.
            // 호출 스택의 깊이가 영역 크기에 비례 → 큰 영역에서 위험.
            FillRecursiveStep(grid, x + 1, y, targetValue, newValue, comparer, filled);
            FillRecursiveStep(grid, x - 1, y, targetValue, newValue, comparer, filled);
            FillRecursiveStep(grid, x, y + 1, targetValue, newValue, comparer, filled);
            FillRecursiveStep(grid, x, y - 1, targetValue, newValue, comparer, filled);
        }
    }
}
