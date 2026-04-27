using System.Collections.Generic;

namespace Algorithms.Search
{
    /// <summary>
    /// =====================================================================
    ///  DFS (Depth-First Search, 깊이 우선 탐색)
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// 시작 정점에서 출발해 한 방향으로 갈 수 있는 데까지 깊게 들어간 뒤,
    /// 더 이상 갈 곳이 없으면 마지막으로 분기했던 정점으로 되돌아와(백트래킹)
    /// 다른 가지를 탐색하는 방식.
    ///
    /// "한 길을 끝까지 따라가다 막히면 되돌아온다" — 미로에서 한쪽 벽에 손을
    /// 대고 따라 걷는 것과 같은 직관.
    ///
    /// BFS 와의 결정적 차이:
    ///   BFS : 가까운 것부터 동심원처럼 퍼져나간다 → 최단 거리 보장
    ///   DFS : 한 가지를 끝까지 파고든다           → 최단 거리 보장하지 않음
    ///
    /// 그래서 DFS 는 "어떤 경로든 하나만 찾으면 된다", "모든 정점을 방문해야 한다"
    /// 같은 경우에 적합하다. 최단 경로가 필요하다면 BFS / Dijkstra / A* 를 써야 한다.
    ///
    /// 2. 동작 흐름 (스택 기반 반복)
    /// ---------------------------------------------------------------------
    ///   ① 스택(Stack)에 시작 정점을 넣고, 방문 집합(HashSet)에도 추가한다.
    ///   ② 스택이 빌 때까지 반복:
    ///        a. 스택에서 정점 하나를 꺼낸다 (Pop).
    ///        b. 결과 리스트에 기록한다 (= 방문 처리).
    ///        c. 그 정점의 이웃들을 모두 살펴본다.
    ///           아직 방문하지 않은 이웃이라면:
    ///             · 방문 집합에 추가
    ///             · 스택의 위(Push)에 올린다.
    ///   ③ 스택이 비면, 도달 가능한 모든 정점을 처리한 것이므로 종료.
    ///
    ///   ※ BFS 코드와 비교해 보면 차이는 단 하나뿐:
    ///       Queue&lt;T&gt; → Stack&lt;T&gt; (FIFO → LIFO)
    ///     자료구조만 바꿔도 자연스럽게 너비 우선이 깊이 우선으로 변한다.
    ///     이게 두 알고리즘이 "쌍둥이" 라고 불리는 이유.
    ///
    ///   ※ 재귀 버전도 존재한다 (SearchRecursive). 두 방식의 차이:
    ///     - 재귀 : 코드가 짧고 직관적. 그래프가 매우 깊으면 호출 스택이 터질 수 있다.
    ///     - 반복 : 직접 Stack&lt;T&gt; 를 관리. 깊이에 안전. Unity / 게임 환경에서 권장.
    ///
    /// 3. 시간 / 공간 복잡도  (V = 정점 수, E = 간선 수)
    /// ---------------------------------------------------------------------
    ///   - 시간 : O(V + E)
    ///        BFS 와 동일. 각 정점은 1회만 처리되고, 각 간선도 1회만 검사된다.
    ///   - 공간 : O(V)
    ///        반복 버전 : 스택과 방문 집합이 최대 V 개 정점을 보관.
    ///        재귀 버전 : 호출 스택의 깊이가 최악의 경우 V 까지 갈 수 있다.
    ///                   즉, 정점이 수만 개를 넘는 그래프에서는 StackOverflowException
    ///                   위험이 있으므로 반복 버전이 안전하다.
    ///
    /// 4. 게임에서의 사용처
    /// ---------------------------------------------------------------------
    ///   - 미로 생성 (Recursive Backtracking)
    ///       → DFS 로 무작위 깊이 탐색하며 벽을 허물면 자연스러운 미로가 만들어진다.
    ///   - 연결 요소(Connected Component) 판정 / 그룹화
    ///       → "같은 색 블록끼리 묶기" 같은 그룹 판별
    ///   - 위상 정렬(Topological Sort)
    ///       → 의존 그래프 정렬 (예: 빌드 순서, 퀘스트 의존 순서)
    ///   - 사이클 검출
    ///       → 어떤 정점에 다시 돌아오는지 검사
    ///   - 백트래킹 퍼즐 (스도쿠, N-Queens, 크로스워드)
    ///   - 게임 트리 / AI 의사 결정
    ///       → Minimax, Alpha-Beta 가 본질적으로 DFS 위에 쌓인 알고리즘
    ///
    /// 5. 사용한 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   - Stack&lt;T&gt;       : 마지막에 들어간 것이 먼저 나오는(LIFO) 자료구조.
    ///                       이웃을 위에 쌓고 위에서 꺼내야 한 가지를 끝까지 파고들 수 있다.
    ///                       Queue(FIFO)를 쓰면 너비가 우선이 되어 BFS 가 되어버린다.
    ///   - HashSet&lt;T&gt;     : 방문 여부를 평균 O(1) 에 검사/추가. (BFS 와 동일)
    ///   - Dictionary&lt;K,V&gt;: 그래프의 인접 리스트 / 부모 추적.
    ///   - List&lt;T&gt;        : 방문 순서/경로를 순서대로 저장.
    /// </summary>
    public static class DFSAlgorithm
    {
        /// <summary>
        /// 시작 정점에서 도달 가능한 모든 정점을 DFS 순서로 방문하고,
        /// 그 방문 순서를 리스트로 반환한다 (반복 버전, 권장).
        ///
        /// BFS 의 Search 와 거의 동일한 골격이다 — 단, 자료구조가 Stack 으로 바뀌었다.
        /// 그것만으로 탐색 방향이 너비 → 깊이 로 바뀐다.
        /// </summary>
        public static List<T> Search<T>(Graph<T> graph, T start)
        {
            // ───────────────────────────────────────────────────────────
            // [1] 결과 컨테이너 — 방문한 정점을 방문한 순서대로 누적.
            // ───────────────────────────────────────────────────────────
            var visitedOrder = new List<T>();

            // [2] 시작 정점이 그래프에 없으면 탐색 자체가 불가능 → 빈 결과 반환.
            if (!graph.Contains(start))
            {
                return visitedOrder;
            }

            // ───────────────────────────────────────────────────────────
            // [3] 방문 집합 — 같은 정점이 스택에 중복으로 쌓이는 것을 막는다.
            //     (BFS 와 같은 이유로 List 가 아닌 HashSet 을 쓴다.)
            // ───────────────────────────────────────────────────────────
            var visited = new HashSet<T>();

            // ───────────────────────────────────────────────────────────
            // [4] DFS 의 심장: 스택(LIFO)
            //     '가장 최근에 발견된 정점' 을 '가장 먼저 처리' 해야
            //     깊이 우선의 성질이 성립한다.
            // ───────────────────────────────────────────────────────────
            var stack = new Stack<T>();

            // ───────────────────────────────────────────────────────────
            // [5] 시작 정점을 스택에 올리고 동시에 방문 표시.
            //     ※ 스택에 넣는 시점에 visited 에 추가하는 것이 정석.
            //       꺼낼 때 추가하면 같은 정점이 스택에 중복으로 쌓일 수 있다.
            //       (BFS 의 큐와 정확히 같은 이유)
            // ───────────────────────────────────────────────────────────
            stack.Push(start);
            visited.Add(start);

            // ───────────────────────────────────────────────────────────
            // [6] 스택이 비면 = 도달 가능한 모든 정점을 처리한 것이므로 종료.
            // ───────────────────────────────────────────────────────────
            while (stack.Count > 0)
            {
                // [6-1] 스택의 가장 위 정점을 꺼낸다.
                T current = stack.Pop();

                // [6-2] 결과 리스트에 기록 = '방문 완료' 처리.
                visitedOrder.Add(current);

                // [6-3] 현재 정점의 이웃들을 살펴, 처음 보는 이웃을 스택에 올린다.
                foreach (T neighbor in graph.GetNeighbors(current))
                {
                    // HashSet.Add 는 새로 추가됐으면 true, 이미 있으면 false 를 반환.
                    // BFS 코드와 동일한 관용적 패턴.
                    if (visited.Add(neighbor))
                    {
                        // 처음 발견한 이웃을 스택 위에 올린다.
                        // 다음 반복에서 즉시 이 이웃부터 깊이 들어간다.
                        stack.Push(neighbor);
                    }
                }
            }

            // [7] 누적된 방문 순서 반환.
            return visitedOrder;
        }

        /// <summary>
        /// DFS 의 재귀 버전.
        /// 코드가 매우 짧고 알고리즘의 본질이 직관적으로 드러난다는 장점이 있다.
        /// 단, 그래프가 매우 깊으면 호출 스택이 터질 수 있으므로 학습/소규모 그래프 용.
        /// </summary>
        public static List<T> SearchRecursive<T>(Graph<T> graph, T start)
        {
            var visitedOrder = new List<T>();
            if (!graph.Contains(start))
            {
                return visitedOrder;
            }

            var visited = new HashSet<T>();
            DfsVisit(graph, start, visited, visitedOrder);
            return visitedOrder;
        }

        private static void DfsVisit<T>(Graph<T> graph, T node, HashSet<T> visited, List<T> visitedOrder)
        {
            // 이미 방문한 노드면 즉시 종료 → 사이클이 있어도 안전.
            if (!visited.Add(node)) return;

            // 현재 정점을 방문 처리.
            visitedOrder.Add(node);

            // 모든 이웃에 대해 재귀적으로 깊이 들어간다.
            // 각 재귀 호출이 끝나면 자연스럽게 백트래킹이 일어난다.
            foreach (T neighbor in graph.GetNeighbors(node))
            {
                DfsVisit(graph, neighbor, visited, visitedOrder);
            }
        }

        /// <summary>
        /// 시작 정점에서 목표 정점까지의 *어떤* 경로를 반환한다.
        /// 결과 리스트는 [start, ..., goal] 순서.
        /// 도달 불가능하면 빈 리스트를 반환.
        ///
        /// ※ 매우 중요: BFS.FindPath 는 최단 경로를 보장하지만,
        ///   DFS.FindPath 는 *최단을 보장하지 않는다*. 첫 번째로 발견되는 경로일 뿐.
        ///   미로에서 어떤 출구든 하나만 찾으면 되는 경우엔 충분하지만,
        ///   "가장 빠른 길" 이 필요하다면 반드시 BFS / Dijkstra / A* 를 써야 한다.
        ///
        /// 핵심 트릭은 BFS 와 동일:
        ///   '각 정점을 처음으로 발견한 부모 정점' 을 Dictionary 에 기록해 두고,
        ///   목표에 도달하면 부모를 거꾸로 따라가 경로를 복원한다.
        /// </summary>
        public static List<T> FindPath<T>(Graph<T> graph, T start, T goal)
        {
            if (!graph.Contains(start) || !graph.Contains(goal))
            {
                return new List<T>();
            }

            if (EqualityComparer<T>.Default.Equals(start, goal))
            {
                return new List<T> { start };
            }

            var parent  = new Dictionary<T, T>();
            var visited = new HashSet<T>();
            var stack   = new Stack<T>();

            stack.Push(start);
            visited.Add(start);

            while (stack.Count > 0)
            {
                T current = stack.Pop();

                foreach (T neighbor in graph.GetNeighbors(current))
                {
                    if (!visited.Add(neighbor)) continue;

                    // 누가 이 정점을 처음 발견했는지 기록 (경로 복원용).
                    parent[neighbor] = current;

                    // 목표 발견 즉시 종료.
                    // ※ BFS 와 달리, 이 시점의 경로가 최단이라는 보장은 없다.
                    //   단지 "발견된 어떤 경로" 중 하나일 뿐.
                    if (EqualityComparer<T>.Default.Equals(neighbor, goal))
                    {
                        return ReconstructPath(parent, start, goal);
                    }

                    stack.Push(neighbor);
                }
            }

            // 스택이 다 비도록 목표를 못 만났다면 두 정점은 연결돼 있지 않다.
            return new List<T>();
        }

        /// <summary>
        /// 부모 매핑(child → parent)을 거꾸로 따라가며 [start, ..., goal] 경로를 복원한다.
        /// (BFSAlgorithm 의 ReconstructPath 와 동일한 로직)
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
