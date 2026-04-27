using System;
using System.Collections.Generic;

namespace Algorithms.Search
{
    /// <summary>
    /// =====================================================================
    ///  A* (A-Star Pathfinding)
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// A* 는 *목표 지향적인* 최단 경로 알고리즘이다. Dijkstra 에 *휴리스틱(heuristic)*
    /// 을 더해 "목표 쪽으로 더 그럴듯한 정점을 먼저 탐색" 하도록 가속한 버전.
    ///
    /// 핵심 평가 함수 :
    ///   f(n) = g(n) + h(n)
    ///     - g(n) : 시작점 → n 까지의 *현재까지 알려진* 최단 비용 (= Dijkstra 의 distance)
    ///     - h(n) : n → 목표 까지의 *예상* 비용 (휴리스틱 = 추정치)
    ///     - f(n) : n 을 거치는 경로의 *예상 총 비용*
    ///
    /// 우선순위 큐가 g 가 아닌 *f* 를 키로 사용한다는 점이 Dijkstra 와의 *유일한* 차이.
    /// h(n) ≡ 0 으로 두면 A* 는 그대로 Dijkstra 가 된다.
    ///
    /// 가족 관계 정리 :
    ///   BFS       : Queue (FIFO)             — 가중치 같음, 휴리스틱 없음
    ///   Dijkstra  : MinPQ priority = g(n)    — 가중치 다름, 휴리스틱 없음
    ///   A*        : MinPQ priority = g(n)+h(n) — 가중치 다름, 휴리스틱 있음
    ///
    /// 셋 다 *우선순위 큐의 priority 를 무엇으로 두느냐* 만 다른 동일 골격이다.
    ///
    /// 2. 휴리스틱의 두 가지 성질 (이 둘이 만족돼야 A* 가 최단 경로 보장)
    /// ---------------------------------------------------------------------
    ///   a) Admissible (허용성) :
    ///        h(n) ≤ 실제 n→goal 최단 비용
    ///        → 절대 *과대평가* 하지 말 것. 과대평가하면 진짜 최단 경로가 무시될 수 있다.
    ///
    ///   b) Consistent (일관성) — 더 강한 조건:
    ///        h(n) ≤ edge.weight + h(neighbor)
    ///        → 한 번 settled 된 노드를 다시 갱신할 일이 없어진다 (Dijkstra-like 효율).
    ///
    ///   허용성만 만족하고 일관성은 안 만족하는 경우에도 최단 경로는 보장되지만,
    ///   같은 노드를 여러 번 갱신할 수 있어 비효율적이다.
    ///
    ///   그리드의 일반적 휴리스틱 (이 프로젝트의 시각화 데모는 Manhattan 사용):
    ///   - **Manhattan distance** : |dx| + |dy| — 4 방향 이동만 허용 시 admissible & consistent.
    ///   - **Euclidean**          : √(dx² + dy²) — 자유 이동 시. 4 방향 환경에선 underestimate (느슨함).
    ///   - **Octile**             : 8 방향 이동 시. |dx| + |dy| - (2-√2)·min(|dx|,|dy|).
    ///
    /// 3. 동작 흐름
    /// ---------------------------------------------------------------------
    ///   ① g(start) = 0. PQ 에 (start, h(start)) push.   ← priority = f = 0 + h
    ///   ② PQ 가 빌 때까지 반복:
    ///        a. f 가 가장 작은 정점 current 를 꺼낸다.
    ///        b. 이미 settled 면 무시 (lazy deletion).
    ///        c. current 가 goal 이면 → 경로 복원 후 종료.
    ///        d. 이웃들에 대해 relax:
    ///             tentativeG = g(current) + edge.weight
    ///             tentativeG 가 g(neighbor) 보다 짧으면:
    ///                · g(neighbor) = tentativeG
    ///                · parent[neighbor] = current
    ///                · PQ 에 (neighbor, tentativeG + h(neighbor)) push
    ///   ③ PQ 가 비도록 goal 을 못 만나면 도달 불가.
    ///
    /// 4. 시간 / 공간 복잡도
    /// ---------------------------------------------------------------------
    ///   - 최악 시간 : O((V + E) log V) — Dijkstra 와 동일 (휴리스틱 = 0 인 경우)
    ///   - 최선 시간 : 휴리스틱이 정확할수록 *훨씬* 적은 노드만 탐색.
    ///                 그리드 + Manhattan 휴리스틱이면 일반적으로 Dijkstra 의 1/2 ~ 1/10 수준.
    ///   - 공간     : O(V)
    ///
    ///   "이론적 worst case 는 Dijkstra 와 같지만 *실측은 압도적으로 빠르다*" 가 A* 의 진가.
    ///
    /// 5. 게임에서의 사용처
    /// ---------------------------------------------------------------------
    ///   - 그리드 / 노드맵 길찾기의 *사실상 표준*. RTS / RPG / 보드게임 모두.
    ///   - Unity 의 NavMesh 도 내부적으로 A* (또는 그 변형) 사용.
    ///   - 게임 AI 의 행동 트리 / 의사결정 트리 가지치기 (구조는 다르지만 같은 아이디어).
    ///   - 퍼즐 풀이 (15-퍼즐, 8-puzzle 등) — 상태 공간 탐색에 응용.
    ///
    /// 6. 사용한 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   Dijkstra 와 동일. 다른 점은 *우선순위 큐의 priority 만*.
    ///   - MinPriorityQueue&lt;T&gt; : priority = f(n) = g(n) + h(n).
    ///   - Dictionary&lt;T, float&gt; gScore : 시작점에서 n 까지의 알려진 최단 비용 (g).
    ///   - HashSet&lt;T&gt; settled       : 처리 완료 (lazy deletion).
    ///   - Dictionary&lt;T, T&gt; parent  : 경로 복원용.
    /// </summary>
    public static class AStarAlgorithm
    {
        /// <summary>
        /// 시작 정점에서 목표 정점까지의 최단 경로(가중치 합 기준) 를 반환한다.
        /// 결과 리스트는 [start, ..., goal] 순서. 도달 불가능하면 빈 리스트.
        ///
        /// 휴리스틱은 호출 측에서 주입한다. 그리드 환경이라면 보통 Manhattan distance.
        /// h(n) ≡ 0 (= (a,b) =&gt; 0f) 으로 호출하면 정확히 Dijkstra 와 같은 결과.
        /// </summary>
        /// <param name="graph">가중치 그래프 (음수 간선 없을 것)</param>
        /// <param name="start">시작 정점</param>
        /// <param name="goal">목표 정점</param>
        /// <param name="heuristic">(node, goal) → 예상 남은 비용. admissible 이어야 최단 보장.</param>
        public static List<T> FindPath<T>(
            WeightedGraph<T> graph,
            T start,
            T goal,
            Func<T, T, float> heuristic)
        {
            // ───────────────────────────────────────────────────────────
            // [1] 입력 검증.
            // ───────────────────────────────────────────────────────────
            if (!graph.Contains(start) || !graph.Contains(goal))
                return new List<T>();

            if (EqualityComparer<T>.Default.Equals(start, goal))
                return new List<T> { start };

            if (heuristic == null)
                throw new ArgumentNullException(nameof(heuristic),
                    "A* 는 휴리스틱이 필요합니다. h(n)=0 으로 두려면 (a,b) => 0f 를 넘기면 됩니다 (= Dijkstra).");

            // ───────────────────────────────────────────────────────────
            // [2] 자료구조.
            //     gScore  : 시작점 → n 까지의 *현재까지 알려진* 최단 비용 (g 값).
            //     parent  : 경로 복원용.
            //     settled : 처리 완료 집합 (lazy deletion).
            //     pq      : priority = f(n) = g(n) + h(n).
            //               ← Dijkstra 는 priority = g(n) 이었다. 한 줄 차이.
            // ───────────────────────────────────────────────────────────
            var gScore  = new Dictionary<T, float>();
            var parent  = new Dictionary<T, T>();
            var settled = new HashSet<T>();
            var pq      = new MinPriorityQueue<T>();

            // [3] 시작 정점 초기화.
            //     g(start) = 0, f(start) = h(start) (g 가 0 이니).
            gScore[start] = 0f;
            pq.Enqueue(start, heuristic(start, goal));

            // ───────────────────────────────────────────────────────────
            // [4] PQ 가 빌 때까지 반복.
            // ───────────────────────────────────────────────────────────
            while (pq.Count > 0)
            {
                // [4-1] f 가 가장 작은 정점 추출.
                T current = pq.Dequeue();

                // [4-2] lazy deletion.
                if (!settled.Add(current)) continue;

                // [4-3] 목표에 도달했으면 즉시 경로 복원.
                //       ※ Dijkstra 와 마찬가지로 settled 시점이 곧 최단 확정.
                //         (admissible 휴리스틱 가정 하에서.)
                if (EqualityComparer<T>.Default.Equals(current, goal))
                    return ReconstructPath(parent, start, goal);

                // [4-4] 이웃 relax.
                foreach (var (neighbor, weight) in graph.GetNeighbors(current))
                {
                    if (settled.Contains(neighbor)) continue;

                    float tentativeG = gScore[current] + weight;

                    // 더 짧은 g 가 발견되면 갱신.
                    if (!gScore.TryGetValue(neighbor, out float oldG) || tentativeG < oldG)
                    {
                        gScore[neighbor] = tentativeG;
                        parent[neighbor] = current;

                        // 우선순위 = f = g + h.
                        // *여기가 Dijkstra 와의 유일한 코드 차이* — Dijkstra 는 priority = tentativeG.
                        float f = tentativeG + heuristic(neighbor, goal);
                        pq.Enqueue(neighbor, f);
                    }
                }
            }

            // PQ 가 비도록 목표를 못 만나면 도달 불가.
            return new List<T>();
        }

        /// <summary>
        /// 부모 매핑(child → parent)을 거꾸로 따라가며 [start, ..., goal] 경로를 복원한다.
        /// (Dijkstra / BFS / DFS 의 ReconstructPath 와 동일 로직.)
        /// </summary>
        private static List<T> ReconstructPath<T>(Dictionary<T, T> parent, T start, T goal)
        {
            var path = new List<T> { goal };
            T current = goal;

            while (!EqualityComparer<T>.Default.Equals(current, start))
            {
                current = parent[current];
                path.Add(current);
            }

            path.Reverse();
            return path;
        }
    }
}
