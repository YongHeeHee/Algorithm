using System.Collections.Generic;

namespace Algorithms.Search
{
    /// <summary>
    /// =====================================================================
    ///  인접 리스트(Adjacency List) 방식의 일반 그래프 자료구조
    /// =====================================================================
    ///
    /// ▶ 그래프 표현 방식 두 가지
    ///   1) 인접 행렬(Adjacency Matrix) : 2차원 배열로 모든 정점 쌍의 연결을 저장.
    ///        - 메모리 : O(V^2)
    ///        - 두 정점이 연결됐는지 확인 : O(1)
    ///        - 정점이 많고 간선이 적으면 메모리 낭비가 심하다.
    ///
    ///   2) 인접 리스트(Adjacency List) : 각 정점마다 '이 정점과 연결된 정점들의 목록' 만 저장.
    ///        - 메모리 : O(V + E)
    ///        - 한 정점의 이웃을 순회 : O(이웃 수)
    ///        - 게임의 타일맵, 노드맵처럼 정점은 많고 간선은 비교적 적은 그래프에 적합.
    ///
    /// ▶ 왜 Dictionary&lt;T, List&lt;T&gt;&gt; 인가?
    ///   - Key   = 정점(Vertex). 어떤 타입이든 받을 수 있도록 제네릭 T 로 둔다.
    ///   - Value = 그 정점에서 갈 수 있는 이웃 정점들의 리스트.
    ///   정점 ID가 0..N-1 처럼 연속적이지 않아도 좋다 (예: 좌표 Vector2Int, 문자열, 커스텀 객체).
    ///   배열 인덱스 대신 Dictionary 를 쓰면 어떤 타입의 키도 자연스럽게 다룰 수 있다.
    ///
    /// ▶ 게임에서의 용례
    ///   - 그리드 타일맵        : Vector2Int → 인접 4방향 좌표
    ///   - 던전 룸 그래프       : RoomId   → 연결된 방들
    ///   - 퀘스트 의존 관계     : QuestId  → 선행 퀘스트들
    ///   - 스킬 트리            : SkillId  → 잠금 해제 가능한 스킬들
    /// </summary>
    public class Graph<T>
    {
        // 그래프의 핵심 저장소. 정점(T) → 그 정점의 이웃 목록(List<T>).
        // readonly 로 두어 외부에서 컬렉션 자체를 통째로 교체하지 못하게 보호한다.
        // (List 의 내용물은 GetNeighbors 로 IReadOnlyList 형태로만 노출 → 캡슐화)
        private readonly Dictionary<T, List<T>> _adjacency = new();

        /// <summary>그래프에 등록된 모든 정점을 순회 가능한 컬렉션으로 반환.</summary>
        public IEnumerable<T> Nodes => _adjacency.Keys;

        /// <summary>등록된 정점 개수(V).</summary>
        public int NodeCount => _adjacency.Count;

        /// <summary>
        /// 그래프에 정점을 추가한다. 이미 존재하는 정점이면 아무 것도 하지 않는다(멱등 연산).
        /// </summary>
        public void AddNode(T node)
        {
            // ContainsKey 검사로 중복 등록을 막는다.
            // 이미 있는 정점에 새 빈 리스트를 덮어쓰면, 기존에 등록된 간선들이 통째로 사라지기 때문.
            if (!_adjacency.ContainsKey(node))
            {
                _adjacency[node] = new List<T>();
            }
        }

        /// <summary>
        /// from → to 방향의 간선을 추가한다.
        /// bidirectional 이 true 면 to → from 방향도 함께 추가하여 무방향 간선처럼 동작한다.
        /// </summary>
        /// <param name="bidirectional">
        /// 게임에서 일반 통로/도로는 양방향(true), 일방통행/점프대/낭떠러지는 단방향(false) 으로 표현 가능.
        /// </param>
        public void AddEdge(T from, T to, bool bidirectional = true)
        {
            // 정점이 미리 등록돼 있지 않더라도 자동 등록한다.
            // → 사용자가 AddEdge 만 호출해도 누락 없이 그래프가 구축된다.
            AddNode(from);
            AddNode(to);

            // from 의 이웃 리스트에 to 를 추가.
            _adjacency[from].Add(to);

            // 양방향이면 반대 방향도 추가.
            if (bidirectional)
            {
                _adjacency[to].Add(from);
            }
        }

        /// <summary>
        /// 정점 node 의 이웃 리스트를 반환한다.
        /// 등록되지 않은 정점이면 빈 배열을 반환하므로 호출 측에서 null 검사가 필요 없다.
        /// </summary>
        public IReadOnlyList<T> GetNeighbors(T node)
        {
            // TryGetValue : 키가 있으면 out 변수에 담고 true, 없으면 false.
            // ContainsKey + 인덱서 두 번 호출보다 빠르고 안전하다.
            if (_adjacency.TryGetValue(node, out var neighbors))
            {
                return neighbors;
            }
            // 키가 없는 경우엔 빈 배열로 안전 반환.
            return System.Array.Empty<T>();
        }

        /// <summary>해당 정점이 그래프에 등록되어 있는지 검사.</summary>
        public bool Contains(T node) => _adjacency.ContainsKey(node);
    }
}
