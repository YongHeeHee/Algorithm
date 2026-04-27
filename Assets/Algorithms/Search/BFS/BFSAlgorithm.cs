using System.Collections.Generic;

namespace Algorithms.Search
{
    /// <summary>
    /// =====================================================================
    ///  BFS (Breadth-First Search, 너비 우선 탐색)
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// 시작 정점에서 가까운 정점부터 차례차례 방문하는 탐색 기법.
    /// "1단계 떨어진 모든 정점 → 2단계 떨어진 모든 정점 → 3단계 ..." 처럼
    /// 동심원이 퍼져 나가듯 탐색이 진행된다.
    ///
    /// 이 '가까운 것부터' 라는 성질 덕분에,
    /// 모든 간선의 가중치(이동 비용)가 같은 그래프에서는
    /// 시작점에서 어떤 정점까지의 "최단 거리(= 거쳐 간 간선 수)" 를 항상 보장한다.
    /// → DFS(깊이 우선)는 깊게 파고들기 때문에 최단 경로를 보장하지 않는다.
    /// → 가중치가 서로 다르면 BFS 만으로는 최단 비용을 보장할 수 없으므로
    ///   Dijkstra / A* 같은 다른 알고리즘을 써야 한다.
    ///
    /// 2. 동작 흐름
    /// ---------------------------------------------------------------------
    ///   ① 큐(Queue)에 시작 정점을 넣고, 방문 집합(HashSet)에도 추가한다.
    ///   ② 큐가 빌 때까지 반복:
    ///        a. 큐에서 정점 하나를 꺼낸다 (Dequeue).
    ///        b. 결과 리스트에 기록한다 (= 방문 처리).
    ///        c. 그 정점의 이웃들을 모두 살펴본다.
    ///           아직 방문하지 않은 이웃이라면:
    ///             · 방문 집합에 추가
    ///             · 큐의 뒤(Enqueue)에 넣는다.
    ///   ③ 큐가 비면, 도달 가능한 모든 정점을 방문한 것이므로 종료.
    ///
    ///   ※ 핵심 포인트:
    ///     - HashSet 에 추가하는 시점은 "큐에 넣는 순간" 이어야 한다.
    ///       "큐에서 꺼낼 때" 추가하면 같은 정점이 큐에 여러 번 들어가
    ///       중복 방문 + 성능 저하가 발생한다.
    ///
    /// 3. 시간 / 공간 복잡도  (V = 정점 수, E = 간선 수)
    /// ---------------------------------------------------------------------
    ///   - 시간 : O(V + E)
    ///        각 정점은 큐에 1회만 들어가고, 각 간선은 1회만 검사된다.
    ///   - 공간 : O(V)
    ///        방문 집합과 큐가 최대 V 개 정점을 보관한다.
    ///
    /// 4. 게임에서의 사용처
    /// ---------------------------------------------------------------------
    ///   - 그리드 기반 최단 경로 (이동 비용이 모두 1 인 경우)
    ///       → 보드게임의 이동 가능 칸 표시, 그리드 퍼즐의 해법 탐색
    ///   - 영역 채우기(Flood Fill)
    ///       → 페인트 도구, 같은 색 블록 매칭(애니팡류)
    ///   - 시야 / 사정거리 / 이동 범위 하이라이트
    ///       → 턴제 SRPG 에서 "이번 턴에 갈 수 있는 칸" 표시
    ///   - 미로 출구 찾기, 연결 요소(Connected Component) 판정
    ///   - "N 턴 안에 도달 가능한 모든 적" 같은 N-홉(neighbor) 검색
    ///
    /// 5. 사용한 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   - Queue&lt;T&gt;       : 먼저 들어간 것이 먼저 나오는(FIFO) 자료구조.
    ///                       이웃을 뒤에 넣고 앞에서 꺼내야 가까운 정점부터 처리된다.
    ///                       Stack(LIFO)을 쓰면 너비가 아닌 깊이 우선(DFS)이 되어 버린다.
    ///   - HashSet&lt;T&gt;     : 방문 여부를 평균 O(1) 에 검사/추가.
    ///                       List 의 Contains 는 O(N) 이라 정점이 많아질수록 매우 느려진다.
    ///   - Dictionary&lt;K,V&gt;: 그래프의 인접 리스트 (Graph&lt;T&gt; 클래스 참고).
    ///   - List&lt;T&gt;        : 방문 순서/경로를 순서대로 저장. 끝에 Add 가 평균 O(1).
    /// </summary>
    public static class BFSAlgorithm
    {
        /// <summary>
        /// 시작 정점에서 도달 가능한 모든 정점을 BFS 순서로 방문하고,
        /// 그 방문 순서를 리스트로 반환한다.
        /// 반환 리스트의 인덱스 = 방문된 순번.
        /// </summary>
        public static List<T> Search<T>(Graph<T> graph, T start)
        {
            // ───────────────────────────────────────────────────────────
            // [1] 결과 컨테이너
            //     방문한 정점을 방문한 순서대로 누적한다.
            //     List<T> 는 끝에 Add 하기가 평균 O(1) 이라 순서 누적에 적합.
            // ───────────────────────────────────────────────────────────
            var visitedOrder = new List<T>();

            // [2] 시작 정점이 그래프에 없으면 탐색 자체가 불가능 → 빈 결과 반환.
            if (!graph.Contains(start))
            {
                return visitedOrder;
            }

            // ───────────────────────────────────────────────────────────
            // [3] 방문 집합
            //     "이미 큐에 넣은 적이 있는가?" 를 O(1) 에 검사하기 위해 HashSet 사용.
            //     List 로 같은 일을 하려면 Contains 가 O(N) 이라 정점이 많아질수록 급격히 느려진다.
            // ───────────────────────────────────────────────────────────
            var visited = new HashSet<T>();

            // ───────────────────────────────────────────────────────────
            // [4] BFS 의 심장: 큐(FIFO)
            //     '먼저 발견된 정점' 을 '먼저 처리' 해야 너비 우선의 성질이 성립한다.
            // ───────────────────────────────────────────────────────────
            var queue = new Queue<T>();

            // ───────────────────────────────────────────────────────────
            // [5] 시작 정점을 큐에 넣고 동시에 방문 표시.
            //     ※ 큐에 넣는 시점에 visited 에 추가하는 것이 정석.
            //       큐에서 꺼낼 때 추가하면, 같은 정점이 큐에 중복으로 쌓일 수 있다.
            // ───────────────────────────────────────────────────────────
            queue.Enqueue(start);
            visited.Add(start);

            // ───────────────────────────────────────────────────────────
            // [6] 큐가 비면 = 도달 가능한 모든 정점을 처리한 것이므로 종료.
            // ───────────────────────────────────────────────────────────
            while (queue.Count > 0)
            {
                // [6-1] 큐의 가장 앞 정점을 꺼낸다.
                //       이 정점은 이미 visited 처리되어 있다(큐에 들어갈 때 표시했으므로).
                T current = queue.Dequeue();

                // [6-2] 결과 리스트에 기록 = '방문 완료' 처리.
                visitedOrder.Add(current);

                // [6-3] 현재 정점의 이웃들을 살펴보며, 처음 보는 이웃을 큐에 넣는다.
                foreach (T neighbor in graph.GetNeighbors(current))
                {
                    // HashSet.Add 는 새로 추가됐으면 true, 이미 있으면 false 를 반환한다.
                    // → "Contains 검사 후 Add" 두 줄을 한 줄로 합치는 관용적 패턴.
                    if (visited.Add(neighbor))
                    {
                        // 처음 발견한 이웃 → 큐 뒤에 넣는다.
                        // 앞쪽(= 더 가까운 정점)이 먼저 처리되므로 거리 순서가 자연스럽게 유지된다.
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // [7] 누적된 방문 순서 반환.
            return visitedOrder;
        }

        /// <summary>
        /// 시작 정점에서 목표 정점까지의 최단 경로(간선 수 기준)를 반환한다.
        /// 결과 리스트는 [start, ..., goal] 순서.
        /// 도달 불가능하면 빈 리스트를 반환.
        ///
        /// 핵심 트릭:
        ///   '각 정점을 처음으로 발견한 부모 정점' 을 Dictionary 에 기록해 둔다.
        ///   목표에 도달하면 부모를 거꾸로 따라가 경로를 복원한다.
        ///   BFS 특성상, 어떤 정점을 '처음' 큐에 넣은 정점이 곧 '최단 경로 상의 부모' 가 된다.
        /// </summary>
        public static List<T> FindPath<T>(Graph<T> graph, T start, T goal)
        {
            // 시작/목표 둘 중 하나라도 그래프에 없으면 경로를 만들 수 없다.
            if (!graph.Contains(start) || !graph.Contains(goal))
            {
                return new List<T>();
            }

            // 시작 = 목표 인 경우, 자기 자신만 들어 있는 경로를 반환.
            // 제네릭 T 의 동등 비교는 == 가 아닌 EqualityComparer<T>.Default 를 써야 한다.
            // (T 가 어떤 타입이든 안전하게 비교하기 위함)
            if (EqualityComparer<T>.Default.Equals(start, goal))
            {
                return new List<T> { start };
            }

            // child → parent 매핑.
            // BFS 의 자연스러운 부산물: '어떤 정점을 처음 큐에 넣은 정점' 을 기록해 두면
            // 그 기록이 곧 최단 경로 트리의 부모-자식 관계가 된다.
            var parent = new Dictionary<T, T>();

            var visited = new HashSet<T>();
            var queue   = new Queue<T>();

            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                T current = queue.Dequeue();

                foreach (T neighbor in graph.GetNeighbors(current))
                {
                    // 이미 방문(혹은 큐에 들어간 적 있는) 정점이면 건너뛴다.
                    if (!visited.Add(neighbor)) continue;

                    // 누가 이 정점을 처음 발견했는지 기록.
                    parent[neighbor] = current;

                    // 목표를 발견한 즉시 종료해도 BFS 는 최단 경로를 보장한다.
                    // (앞으로 큐에 들어올 어떤 경로도 반드시 같거나 더 길기 때문.)
                    if (EqualityComparer<T>.Default.Equals(neighbor, goal))
                    {
                        return ReconstructPath(parent, start, goal);
                    }

                    queue.Enqueue(neighbor);
                }
            }

            // 큐가 다 비도록 목표를 만나지 못했다면 두 정점은 서로 연결되어 있지 않다.
            return new List<T>();
        }

        /// <summary>
        /// 부모 매핑(child → parent)을 거꾸로 따라가며 [start, ..., goal] 경로를 복원한다.
        /// </summary>
        private static List<T> ReconstructPath<T>(Dictionary<T, T> parent, T start, T goal)
        {
            // goal 부터 부모를 따라 start 까지 거슬러 올라간다.
            // 이렇게 채우면 결과는 [goal, ..., start] 순서가 된다.
            var path = new List<T> { goal };
            T current = goal;

            while (!EqualityComparer<T>.Default.Equals(current, start))
            {
                current = parent[current];
                path.Add(current);
            }

            // 마지막에 뒤집어 [start, ..., goal] 로 정렬.
            path.Reverse();
            return path;
        }
    }
}
