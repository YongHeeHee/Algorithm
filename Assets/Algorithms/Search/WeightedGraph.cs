using System.Collections.Generic;

namespace Algorithms.Search
{
    /// <summary>
    /// =====================================================================
    ///  WeightedGraph&lt;T&gt; — 가중치 인접 리스트 그래프
    /// =====================================================================
    ///
    /// ▶ Graph&lt;T&gt; 와의 차이
    ///   - Graph&lt;T&gt;          : 간선의 *존재 여부* 만 보관 (BFS / DFS 용)
    ///   - WeightedGraph&lt;T&gt;  : 간선마다 *가중치(이동 비용)* 를 함께 보관 (Dijkstra / A* 용)
    ///
    ///   가중치가 모두 같으면 BFS 와 Dijkstra 의 결과가 같아진다.
    ///   가중치가 들쭉날쭉하면 BFS 는 잘못된 답을 낼 수 있으므로 반드시 Dijkstra 를 써야 한다.
    ///
    /// ▶ 게임에서의 가중치 예시
    ///   - 지형 비용     : 평지(1) / 풀밭(2) / 숲(3) / 산(5) — 같은 거리라도 통과 비용이 다름
    ///   - 스킬 트리     : 스킬 사이의 잠금 해제 비용
    ///   - 무역 그래프   : 도시 간 운송 비용
    ///   - 네트워크      : 노드 간 지연 시간
    ///
    /// ▶ 자료구조
    ///   Dictionary&lt;T, List&lt;(T neighbor, float weight)&gt;&gt;
    ///   각 정점은 "이웃 + 그쪽으로 가는 비용" 의 리스트를 보유.
    ///   ValueTuple 을 사용해 두 값을 하나의 배열 슬롯에 묶는다 (struct → 추가 할당 없음).
    /// </summary>
    public class WeightedGraph<T>
    {
        // 정점 → (이웃, 가중치) 리스트.
        // ValueTuple (T, float) 은 struct 라 별도 객체를 할당하지 않는다 → GC 친화적.
        private readonly Dictionary<T, List<(T neighbor, float weight)>> _adjacency = new();

        /// <summary>그래프에 등록된 모든 정점.</summary>
        public IEnumerable<T> Nodes => _adjacency.Keys;

        /// <summary>등록된 정점 개수(V).</summary>
        public int NodeCount => _adjacency.Count;

        /// <summary>
        /// 정점을 추가한다. 이미 존재하면 아무 일도 하지 않는다 (멱등).
        /// </summary>
        public void AddNode(T node)
        {
            if (!_adjacency.ContainsKey(node))
            {
                _adjacency[node] = new List<(T, float)>();
            }
        }

        /// <summary>
        /// from → to 방향의 가중치 간선을 추가한다.
        /// bidirectional 이 true 면 to → from 방향도 같은 가중치로 추가.
        /// (방향에 따라 비용이 다르다면 false 로 두 번 따로 호출.)
        /// </summary>
        public void AddEdge(T from, T to, float weight, bool bidirectional = true)
        {
            // 정점이 미등록이어도 자동 등록 → AddEdge 만으로 그래프가 완성될 수 있다.
            AddNode(from);
            AddNode(to);

            _adjacency[from].Add((to, weight));

            if (bidirectional)
            {
                _adjacency[to].Add((from, weight));
            }
        }

        /// <summary>
        /// 정점 node 의 이웃 + 가중치 리스트.
        /// 등록되지 않은 정점이면 빈 배열 반환 → 호출 측에서 null 검사 불필요.
        /// </summary>
        public IReadOnlyList<(T neighbor, float weight)> GetNeighbors(T node)
        {
            if (_adjacency.TryGetValue(node, out var neighbors))
            {
                return neighbors;
            }
            return System.Array.Empty<(T, float)>();
        }

        /// <summary>해당 정점이 등록되어 있는지 검사.</summary>
        public bool Contains(T node) => _adjacency.ContainsKey(node);
    }
}
