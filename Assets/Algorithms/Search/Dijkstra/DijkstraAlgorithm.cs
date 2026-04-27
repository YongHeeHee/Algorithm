using System.Collections.Generic;

namespace Algorithms.Search
{
    /// <summary>
    /// =====================================================================
    ///  Dijkstra (다익스트라 최단 경로 알고리즘)
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// 시작 정점에서 다른 *모든* 정점까지의 *최단 비용* 을 구한다.
    /// 단, 음수 가중치 간선이 있으면 안 된다 (있다면 Bellman-Ford / SPFA 사용).
    ///
    /// 핵심 아이디어 — 탐욕(greedy):
    ///   "지금까지 알아낸 거리 중 가장 짧은 정점부터 처리하면, 그 정점의 거리는 더 이상 줄어들지 않는다."
    ///
    /// 우선순위 큐(Min-Heap)로 "현재 알려진 거리가 가장 짧은 정점" 을 항상 꺼낼 수 있게 만든다.
    /// 큐에서 꺼낸 시점의 거리가 곧 그 정점까지의 *진짜* 최단 거리 (음수 간선이 없기 때문).
    ///
    /// BFS 와의 관계:
    ///   가중치가 모두 1(혹은 같은 값) 이면 Dijkstra = BFS.
    ///   즉, BFS 는 Dijkstra 의 *특수 케이스*. BFS 의 큐를 우선순위 큐로 바꾸고,
    ///   "방문 카운트" 대신 "누적 비용" 을 추적하면 그것이 곧 Dijkstra 다.
    ///
    /// 2. 동작 흐름
    /// ---------------------------------------------------------------------
    ///   ① 모든 정점의 거리(distances) 를 무한대로 초기화.
    ///       → 우리는 "키 미존재 = 무한대" 로 표현한다 (Dictionary 의 자연스러운 표현).
    ///   ② 시작 정점의 거리 = 0. 우선순위 큐에 (start, 0) 을 넣는다.
    ///   ③ 큐가 빌 때까지 반복:
    ///        a. 거리가 가장 짧은 정점(current) 을 꺼낸다.
    ///        b. 이미 settled 된 정점이면 건너뛴다 (lazy deletion).
    ///        c. current 를 settled 처리.
    ///        d. current 의 이웃들을 살피며 'relax' 한다:
    ///             newDist = distances[current] + 간선 가중치
    ///             newDist 가 현재 distances[neighbor] 보다 짧으면:
    ///                · distances[neighbor] = newDist
    ///                · parent[neighbor] = current  (FindPath 용)
    ///                · 우선순위 큐에 (neighbor, newDist) push
    ///   ④ 큐가 비면 모든 정점의 distances 가 확정된다.
    ///
    /// 3. 시간 / 공간 복잡도  (V = 정점 수, E = 간선 수)
    /// ---------------------------------------------------------------------
    ///   이진 힙 기반 PQ 사용 시:
    ///   - 시간 : O((V + E) log V)
    ///        각 정점은 최대 O(log V) 비용으로 추출되고, 각 간선은 1번 검사.
    ///   - 공간 : O(V)
    ///        distances / parent / settled / PQ 모두 V 규모.
    ///
    ///   E ≈ V² 인 밀집 그래프에서는 O(V²) 단순 배열 구현이 더 빠를 수 있지만,
    ///   게임의 그래프(타일맵, 노드맵)는 대개 희소(E ≪ V²) 라 힙이 표준.
    ///
    /// 4. 게임에서의 사용처
    /// ---------------------------------------------------------------------
    ///   - 지형 가중 최단 경로
    ///       → 평지 / 풀밭 / 숲 / 산 처럼 셀마다 통과 비용이 다른 그리드
    ///   - AI 경로 탐색의 기반
    ///       → A* 의 휴리스틱 h(n)=0 이면 정확히 Dijkstra. A* 는 Dijkstra 의 가속 버전.
    ///   - 자원 수송 / 무역 비용 최적화
    ///       → 도시-도시 간 운송 비용이 다른 경제 시뮬레이션
    ///   - 스킬 트리 잠금 해제 최단 경로
    ///       → 스킬 사이 잠금 해제 비용을 가중치로 두면 "최소 비용으로 X 스킬 도달" 계산
    ///   - 네트워크 라우팅 시뮬레이션
    ///       → MMORPG 의 NPC 길찾기, RTS 의 유닛 이동
    ///
    /// 5. 사용한 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   - MinPriorityQueue&lt;T&gt;  : 거리가 가장 짧은 정점을 O(log n) 에 꺼낸다.
    ///                            BFS 의 Queue&lt;T&gt; 와 같은 역할이지만,
    ///                            가중치가 다양하므로 "단순 FIFO" 가 아니라 "거리 기준 정렬"이 필요.
    ///                            → 자료구조 한 줄 차이로 BFS → Dijkstra 가 된다.
    ///   - Dictionary&lt;T, float&gt; : 시작 정점에서 각 정점까지의 *현재까지 알려진* 최단 거리.
    ///                            "키 미존재 == 무한대" 라는 관용. float.PositiveInfinity 를
    ///                            매번 채워 두지 않아도 되어 깔끔하다.
    ///   - HashSet&lt;T&gt;           : 이미 처리(settled) 된 정점 집합.
    ///                            lazy deletion: 같은 정점이 PQ 에 여러 번 들어갈 수 있으므로,
    ///                            꺼낼 때 "이미 settled" 면 무시한다.
    ///   - Dictionary&lt;T, T&gt;     : 부모 추적. FindPath 의 경로 복원에 사용.
    /// </summary>
    public static class DijkstraAlgorithm
    {
        /// <summary>
        /// 시작 정점에서 도달 가능한 모든 정점까지의 최단 거리를 반환한다.
        /// 결과 dictionary 의 키 = 도달 가능한 정점, 값 = 시작점에서의 최단 비용 합.
        /// 도달 불가능한 정점은 결과에 키가 없다.
        /// </summary>
        public static Dictionary<T, float> Search<T>(WeightedGraph<T> graph, T start)
        {
            // ───────────────────────────────────────────────────────────
            // [1] 결과 컨테이너 — 각 정점까지의 최단 거리.
            //     Dictionary 의 키가 없으면 "아직 도달 못 함 (= 무한대)" 으로 해석.
            // ───────────────────────────────────────────────────────────
            var distances = new Dictionary<T, float>();

            // [2] 시작 정점이 그래프에 없으면 탐색 자체가 불가능 → 빈 결과 반환.
            if (!graph.Contains(start))
            {
                return distances;
            }

            // ───────────────────────────────────────────────────────────
            // [3] 우선순위 큐 — 거리가 가장 짧은 정점부터 처리.
            //     BFS 의 Queue 가 정확히 이 자리에 있었다는 점을 비교해 보면 좋다.
            // ───────────────────────────────────────────────────────────
            var pq = new MinPriorityQueue<T>();

            // ───────────────────────────────────────────────────────────
            // [4] settled 집합 — 거리가 *확정된* 정점.
            //     lazy deletion 패턴: PQ 에 같은 정점이 여러 번 들어갈 수 있어서,
            //     꺼낼 때 "이미 처리됐으면 무시" 한다. 힙 내부에서 직접 키를 갱신하는 것보다 단순.
            // ───────────────────────────────────────────────────────────
            var settled = new HashSet<T>();

            // [5] 시작 정점 초기화.
            distances[start] = 0f;
            pq.Enqueue(start, 0f);

            // ───────────────────────────────────────────────────────────
            // [6] 큐가 빌 때까지 반복.
            // ───────────────────────────────────────────────────────────
            while (pq.Count > 0)
            {
                // [6-1] 현재 알려진 거리가 가장 짧은 정점을 꺼낸다.
                T current = pq.Dequeue();

                // [6-2] lazy deletion: 이미 처리된 정점이면 건너뛴다.
                //       HashSet.Add 는 새로 추가됐으면 true, 이미 있으면 false.
                if (!settled.Add(current)) continue;

                // [6-3] current 의 이웃들을 'relax' 한다.
                //       relaxation = "현재 알려진 neighbor 까지의 거리를 줄일 수 있는지 검사"
                foreach (var (neighbor, weight) in graph.GetNeighbors(current))
                {
                    // 이미 확정된 이웃은 더 짧아질 수 없다 (음수 간선이 없으므로).
                    if (settled.Contains(neighbor)) continue;

                    float newDist = distances[current] + weight;

                    // 현재 distances 보다 짧으면 갱신.
                    // (키가 없거나 = 무한대) 이거나 (값이 더 컸으면) 갱신.
                    if (!distances.TryGetValue(neighbor, out float oldDist) || newDist < oldDist)
                    {
                        distances[neighbor] = newDist;

                        // 같은 neighbor 가 더 짧은 거리로 PQ 에 또 들어간다 (이전 stale 항목은 무시될 것).
                        pq.Enqueue(neighbor, newDist);
                    }
                }
            }

            // [7] 도달 가능한 모든 정점의 최단 거리가 들어 있는 distances 반환.
            return distances;
        }

        /// <summary>
        /// 시작 정점에서 목표 정점까지의 최단 경로(가중치 합 기준) 를 반환한다.
        /// 결과 리스트는 [start, ..., goal] 순서. 도달 불가능하면 빈 리스트.
        ///
        /// 핵심 트릭은 BFS 와 동일:
        ///   '각 정점을 처음 *최단 거리로* 발견한 부모 정점' 을 Dictionary 에 기록해 두고,
        ///   목표에 도달하면 부모를 거꾸로 따라가 경로를 복원한다.
        /// </summary>
        public static List<T> FindPath<T>(WeightedGraph<T> graph, T start, T goal)
        {
            if (!graph.Contains(start) || !graph.Contains(goal))
            {
                return new List<T>();
            }

            if (EqualityComparer<T>.Default.Equals(start, goal))
            {
                return new List<T> { start };
            }

            var distances = new Dictionary<T, float>();
            var parent    = new Dictionary<T, T>();
            var settled   = new HashSet<T>();
            var pq        = new MinPriorityQueue<T>();

            distances[start] = 0f;
            pq.Enqueue(start, 0f);

            while (pq.Count > 0)
            {
                T current = pq.Dequeue();

                // lazy deletion.
                if (!settled.Add(current)) continue;

                // ※ 중요: Dijkstra 에서는 settled 처리 *직후*에 목표인지 검사해야 한다.
                //   "PQ 에 push 할 때" 검사하면 그 시점의 거리가 최단이라는 보장이 없다
                //   (이후 더 짧은 경로가 발견될 수 있으므로). settled 시점이 진짜 최단.
                if (EqualityComparer<T>.Default.Equals(current, goal))
                {
                    return ReconstructPath(parent, start, goal);
                }

                foreach (var (neighbor, weight) in graph.GetNeighbors(current))
                {
                    if (settled.Contains(neighbor)) continue;

                    float newDist = distances[current] + weight;

                    if (!distances.TryGetValue(neighbor, out float oldDist) || newDist < oldDist)
                    {
                        distances[neighbor] = newDist;

                        // *이번에 더 짧아졌으므로* 부모도 갱신.
                        // BFS 는 한 번만 발견하면 그게 최단이지만, Dijkstra 는 더 짧은 경로가 추후
                        // 발견될 수 있어 부모를 여러 번 갱신할 수 있다.
                        parent[neighbor] = current;

                        pq.Enqueue(neighbor, newDist);
                    }
                }
            }

            // 도달 불가.
            return new List<T>();
        }

        /// <summary>
        /// 부모 매핑(child → parent)을 거꾸로 따라가며 [start, ..., goal] 경로를 복원한다.
        /// (BFS / DFS 의 ReconstructPath 와 동일 로직.)
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
