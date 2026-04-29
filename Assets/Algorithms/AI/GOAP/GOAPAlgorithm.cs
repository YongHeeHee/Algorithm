using System;
using System.Collections.Generic;
using System.Linq;
using Algorithms.Search;   // MinPriorityQueue 재사용 — A* 와 동일한 OpenSet 구조

namespace Algorithms.AI
{
    /// <summary>
    /// =====================================================================
    ///  GOAP (Goal-Oriented Action Planning) — 목표 기반 자동 계획 AI
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// GOAP 는 *디자이너가 트리를 그리는* Behavior Tree 와 정반대 결의 AI 다.
    /// 디자이너가 만들어 두는 건 *행동들의 카탈로그* 뿐 — 각 행동의
    ///   - 전제조건(Preconditions): "이 행동이 가능하려면 세상이 어떤 상태여야 하는가"
    ///   - 효과(Effects)          : "이 행동을 끝내면 세상이 어떻게 바뀌는가"
    ///   - 비용(Cost)             : "이 행동을 수행하는 데 드는 추상적 비용"
    /// 만 정의하면 끝. *행동 사이의 순서는 정의하지 않는다.*
    ///
    /// 그러면 GOAP 는 *현재 세상 상태 → 목표 상태* 로 가는 행동 시퀀스를 매번 자동으로
    /// *계획* 해 낸다. 이는 정확히 A* 의 한 응용이다 — 단,
    /// "노드 = 격자의 칸" 이 아니라 "노드 = 가능한 세상 상태" 라는 점만 다르다.
    ///
    ///   GOAP = "**상태 공간(state space) 위의 A***"
    ///
    /// 결과로 나오는 행동 사슬은 디자이너가 미리 짜둔 게 아니다. 같은 행동 카탈로그 위에서
    /// *목표만 바꿔도* 전혀 다른 사슬이 만들어진다. F.E.A.R. (2005) 가 적 AI 에 적용해
    /// 유명해진 패턴이며, 모던 게임의 *자율적 NPC* 의 표준 기법 중 하나.
    ///
    /// 2. 동작 흐름 (Forward-chaining A* — 본 구현)
    /// ---------------------------------------------------------------------
    ///   ① 초기 상태 s0, 목표 부분상태 g, 행동 카탈로그 A 가 주어진다.
    ///   ② OpenSet = MinPriorityQueue. priority = f(s) = g(s) + h(s).
    ///        - g(s): s0 에서 s 까지 누적된 *행동 비용* 합
    ///        - h(s): s 에서 g 까지 *남은 목표 사실 수* (admissible — 각 사실당 최소 1 행동 필요)
    ///   ③ s0 을 OpenSet 에 push.
    ///   ④ OpenSet 이 빌 때까지 반복:
    ///        a. f 가 가장 작은 s 를 pop.
    ///        b. closed 면 skip (lazy deletion).
    ///        c. s 가 g 를 만족 → 경로 복원, 종료.
    ///        d. closed 에 추가.
    ///        e. 모든 행동 a ∈ A 에 대해:
    ///              - a.Pre 가 s 에서 만족되면
    ///                  s' = a 의 Effect 를 s 에 적용한 새 상태
    ///                  tentativeG = g(s) + a.Cost(s)
    ///                  더 짧으면 갱신 + 부모(이전 상태 + 적용 행동) 추적 + push
    ///   ⑤ OpenSet 이 비면 계획 불가능 (도달 가능한 시퀀스 없음).
    ///
    ///   ※ Forward 가 아닌 *Backward (regressive)* 방식도 있다 — F.E.A.R. 의 원본 방식.
    ///     "목표에서 거꾸로, *목표 사실을 만들어 주는 행동* 만 시도" → 더 효율적이지만 구현 복잡.
    ///     본 학습 구현은 forward 가 코드 흐름이 직관적이라 그쪽을 택했다.
    ///
    /// 3. 시간 / 공간 복잡도
    /// ---------------------------------------------------------------------
    ///   - 상태 공간 크기   : 사실(predicate) 개수 P 개의 boolean 이면 최대 2^P 개의 상태.
    ///   - 시간 (worst)     : O(N · (|A| + log N))   N = 탐색된 상태 수, |A| = 행동 카탈로그 크기.
    ///   - 공간             : O(N) — closed/open + gScore + parent map.
    ///
    ///   실제로는 *대부분의 상태가 도달 불가능* 이므로 N ≪ 2^P. 휴리스틱이 강할수록 N 이 급격히
    ///   줄어들며, 실전에서는 "relaxed planning graph" 등 더 강한 휴리스틱이 사용된다.
    ///
    /// 4. 사용 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   - WorldState (Dictionary&lt;string, bool&gt; 래퍼)
    ///       : 사실 기반 상태 표현. *같은 사실 집합 = 같은 상태* 가 되도록 Equals/HashCode 자체 구현.
    ///         이 구조적 동등성이 ClosedSet 정확도의 핵심.
    ///   - MinPriorityQueue&lt;WorldState&gt;
    ///       : OpenSet. f 가 가장 작은 상태를 O(log N) 에 꺼낸다.
    ///         Algorithms.Search 의 동일 자료구조를 재사용 → A* 와 토대가 같음을 코드 레벨로 드러낸다.
    ///   - HashSet&lt;WorldState&gt; (closed)
    ///       : 같은 상태를 다시 평가하지 않게. WorldState 의 GetHashCode 가 핵심.
    ///   - Dictionary&lt;WorldState, WorldState&gt; / Dictionary&lt;WorldState, GOAPAction&gt; (cameFrom)
    ///       : "이 상태에 도달하려고 어떤 행동을 했고 어디서 왔는가" → 경로 복원 (=행동 사슬 복원).
    ///   - GOAPAction
    ///       : Pre / Eff / Cost 의 데이터 묶음. Cost 는 정적/동적 모두 지원해 "현재 위치에서의 거리"
    ///         같은 *동적 비용* 을 반영할 수 있다 — 이게 GOAP 가 같은 카탈로그 위에서 상황별로 다른
    ///         계획을 짜는 비결.
    ///
    ///   ※ 자료구조 자체는 격자 A* 와 완전히 같다. *노드 타입이 좌표 → 세상 상태로 바뀌었을 뿐.*
    ///
    /// 5. 다른 의사결정 알고리즘과의 관계
    /// ---------------------------------------------------------------------
    ///   - Behavior Tree : 디자이너가 *결정 트리* 를 그림. 빠르고 예측 가능. 새 상황 = 트리 수정.
    ///   - GOAP          : 디자이너가 *행동만* 정의. 새 행동을 추가하면 AI 가 자동으로 활용.
    ///                     계획에 시간 소요 + 디버깅이 비교적 어려움 (왜 이 사슬이 나왔는가?).
    ///   - Minimax/MCTS  : *상대* 가 있는 게임에서 점수/통계로 최선 수 탐색. GOAP 는 단일 에이전트의
    ///                     *목표를 향한* 사슬을 짠다. 둘 다 트리 탐색이지만 결이 다름.
    ///   - HTN           : GOAP 의 상위 친척. "큰 작업 → 작은 작업" 의 분해 트리.
    ///                     GOAP 가 평면 탐색이라면 HTN 은 계층 탐색.
    ///
    /// ▶ 주요 변형 (관심 있다면 검색용 키워드)
    ///   - Backward-chaining (Regressive planning)  — F.E.A.R. 원본
    ///   - Dynamic replan                            — 실행 중 세상이 바뀌면 즉시 재계획
    ///   - Action cost dynamic                       — 위치 거리 등 실제 비용 반영 (본 데모도 사용)
    ///   - Hierarchical Task Network (HTN)           — GOAP 의 계층화 버전
    /// </summary>
    public sealed class WorldState
    {
        // [4-WS-1] 사실 집합 — key = 사실명, value = 진리값.
        //          이 데모는 모두 boolean 이지만, 일반화된 GOAP 구현은 정수/문자열 등 임의 값도 다룬다.
        private readonly Dictionary<string, bool> _facts;

        public WorldState() { _facts = new Dictionary<string, bool>(); }

        /// <summary>
        /// 복사 생성자. *상태는 항상 불변 객체처럼 다룬다* — Apply 등은 새 상태를 반환하지 원본을 변형하지 않는다.
        /// 이 패턴 덕에 closed/cameFrom 에 보관된 상태가 나중에 우연히 변형돼 해시가 깨지는 일이 없다.
        /// </summary>
        public WorldState(WorldState other)
        {
            _facts = new Dictionary<string, bool>(other._facts);
        }

        /// <summary>없는 키는 false 로 간주 — open-world 가정.</summary>
        public bool Get(string key) => _facts.TryGetValue(key, out var v) && v;

        /// <summary>한 사실만 바꾼 새 상태를 반환 (원본 불변).</summary>
        public WorldState With(string key, bool value)
        {
            var copy = new WorldState(this);
            copy._facts[key] = value;
            return copy;
        }

        /// <summary>
        /// 부분 상태 partial 의 모든 사실이 이 상태에서 성립하는가.
        /// goal 이 만족됐는지 / action.Pre 가 만족됐는지 검사할 때 둘 다 사용.
        /// </summary>
        public bool Satisfies(WorldState partial)
        {
            foreach (var kv in partial._facts)
            {
                if (Get(kv.Key) != kv.Value) return false;
            }
            return true;
        }

        /// <summary>
        /// partial 과 다른 사실의 개수 — admissible 휴리스틱으로 사용.
        /// 각 미충족 사실을 해결하려면 *최소 한 행동* 이 필요하다는 점에서 admissible.
        /// </summary>
        public int CountUnsatisfied(WorldState partial)
        {
            int n = 0;
            foreach (var kv in partial._facts)
            {
                if (Get(kv.Key) != kv.Value) n++;
            }
            return n;
        }

        public IEnumerable<KeyValuePair<string, bool>> Facts => _facts;

        // ─────────────────────────────────────────────────────────────
        //  Equality / Hash : ClosedSet 정확도의 핵심.
        //
        //  ▶ "의미적 동등성" 으로 비교한다 — 키 *집합* 이 달라도
        //     "없는 키 = false" 컨벤션 하에서 같은 사실을 표현하면 같은 상태로 본다.
        //     예) {hungry=true} 와 {hungry=true, at_axe=false} 는 같은 상태.
        //         (open-world / closed-world 양쪽에서 자연스럽게 작동.)
        //
        //  ▶ 해시는 *true 사실들만* 사용 — false 는 "없음" 과 같은 의미라 무시.
        //     이 규칙이 Equals 와 일관돼야 HashSet 이 정확히 작동한다.
        // ─────────────────────────────────────────────────────────────
        public override bool Equals(object obj)
        {
            if (obj is not WorldState s) return false;

            // 양쪽 사실의 합집합 키를 비교 — 한 쪽에 없는 키는 false 로 간주하고 비교.
            // 한 쪽에서 true 인데 다른 쪽이 false(= 없거나 false 명시) 면 불일치.
            foreach (var kv in _facts)
            {
                if (s.Get(kv.Key) != kv.Value) return false;
            }
            foreach (var kv in s._facts)
            {
                if (Get(kv.Key) != kv.Value) return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            // *true 사실만* 모아 정렬해서 결합. false 는 "없음" 과 동의어이므로 해시에서 제외.
            // 이 규칙이 Equals 의 의미적 동등성과 정확히 일관됨 → HashSet/Dictionary 에서 충돌 없음.
            int hash = 17;
            foreach (var kv in _facts.Where(p => p.Value).OrderBy(p => p.Key))
            {
                hash = hash * 31 + kv.Key.GetHashCode();
            }
            return hash;
        }

        public override string ToString()
        {
            // 디버그/UI 표시용. 사실명 알파벳 순으로 정렬해 출력 안정성 확보.
            return "{" + string.Join(", ",
                _facts.OrderBy(k => k.Key).Select(kv => $"{kv.Key}={(kv.Value ? "T" : "F")}")) + "}";
        }
    }

    /// <summary>
    /// 행동 정의. *Pre / Eff / Cost* 의 데이터 묶음.
    ///
    /// ▶ Pre / Eff 는 *부분 상태(WorldState)* 다.
    ///   - Pre 의 사실들이 *전부 성립* 해야 IsApplicable.
    ///   - Eff 의 사실들은 *현재 상태에 덮어쓰기* 로 적용된다 (Eff 에 없는 사실은 그대로).
    ///
    /// ▶ Cost 는 정적 / 동적 둘 다 지원
    ///   - CostStatic : 일반적인 행동 비용 (예: 1 = 한 단위 시간).
    ///   - CostDynamic: (현재 상태) → 비용. *위치 의존 비용* 을 반영할 때 결정적.
    ///                  예) "MoveToTree" 의 비용 = 현재 위치에서 나무까지의 격자 거리.
    ///                  같은 행동도 NPC 가 어디 있느냐에 따라 비용이 달라져 — 이게
    ///                  GOAP 가 같은 카탈로그 위에서 상황별로 다른 계획을 짜는 핵심.
    /// </summary>
    public sealed class GOAPAction
    {
        public string Name = "(noname)";
        public WorldState Pre = new WorldState();
        public WorldState Eff = new WorldState();
        public float CostStatic = 1f;
        public Func<WorldState, float> CostDynamic;

        public float Cost(WorldState s) => CostDynamic != null ? CostDynamic(s) : CostStatic;

        public bool IsApplicable(WorldState s) => s.Satisfies(Pre);

        /// <summary>이 행동을 s 에 적용한 새 상태 반환 (원본 불변).</summary>
        public WorldState Apply(WorldState s)
        {
            var next = new WorldState(s);
            foreach (var kv in Eff.Facts)
            {
                next = next.With(kv.Key, kv.Value);
            }
            return next;
        }
    }

    /// <summary>
    /// 계획 결과 + 디버깅용 통계.
    /// Found 가 false 면 도달 가능한 사슬이 없거나 maxIterations 초과로 포기한 것.
    /// </summary>
    public sealed class GOAPPlan
    {
        public List<GOAPAction> Actions = new();
        public bool Found;
        public int StatesExplored;     // closed 에 들어간 수
        public int StatesGenerated;    // open 에 push 한 수 (중복 포함)
        public float TotalCost;
    }

    /// <summary>
    /// GOAP 의 본체 — 상태 공간 위에서 A* 를 돌려 행동 시퀀스를 찾는다.
    /// 이 클래스는 *순수 알고리즘* (Unity 의존 0). 격자 시각화는 GOAPGridVisualizer 가 담당.
    /// </summary>
    public static class GOAPPlanner
    {
        public static GOAPPlan Plan(
            WorldState initial,
            WorldState goal,
            IReadOnlyList<GOAPAction> actions,
            int maxIterations = 5000)
        {
            var result = new GOAPPlan();

            // [1] 이미 목표 만족? 빈 계획 반환.
            if (initial.Satisfies(goal))
            {
                result.Found = true;
                return result;
            }

            // [2] 자료구조 준비 — A* 와 동형.
            //      open  : f = g + h 가 작은 상태부터 꺼냄.
            //      gScore: 시작 → 상태 누적 비용.
            //      cameFromState/Action: "이 상태에 도달한 직전 상태와 적용한 행동" — 경로 복원용.
            //      closed: 이미 평가된 상태.
            var open           = new MinPriorityQueue<WorldState>();
            var gScore         = new Dictionary<WorldState, float> { [initial] = 0f };
            var cameFromState  = new Dictionary<WorldState, WorldState>();
            var cameFromAction = new Dictionary<WorldState, GOAPAction>();
            var closed         = new HashSet<WorldState>();

            open.Enqueue(initial, Heuristic(initial, goal));
            result.StatesGenerated = 1;

            // [3] 메인 루프.
            int iter = 0;
            while (open.Count > 0 && iter++ < maxIterations)
            {
                // [4-a] f 가 가장 작은 상태를 꺼낸다.
                var current = open.Dequeue();

                // [4-b] 이미 처리된 상태면 skip — 같은 상태가 더 짧은 g 로 다시 push 됐을 수 있음 (lazy deletion).
                if (closed.Contains(current)) continue;

                // [4-c] 목표 도달? — current 가 goal 의 모든 사실을 포함하면 목표 만족.
                if (current.Satisfies(goal))
                {
                    Reconstruct(current, cameFromState, cameFromAction, result);
                    result.Found          = true;
                    result.StatesExplored = closed.Count;
                    result.TotalCost      = gScore[current];
                    return result;
                }

                closed.Add(current);

                // [4-e] 적용 가능한 모든 행동 시도 → 자식 상태 생성 + relax.
                foreach (var a in actions)
                {
                    if (!a.IsApplicable(current)) continue;

                    var next = a.Apply(current);
                    if (closed.Contains(next)) continue;

                    float tentativeG = gScore[current] + a.Cost(current);

                    // 이 next 에 대해 더 짧은 g 가 발견되면 갱신 + push.
                    if (gScore.TryGetValue(next, out var prevG) && tentativeG >= prevG) continue;

                    gScore[next]         = tentativeG;
                    cameFromState[next]  = current;
                    cameFromAction[next] = a;

                    float f = tentativeG + Heuristic(next, goal);
                    open.Enqueue(next, f);
                    result.StatesGenerated++;
                }
            }

            // [5] 목표를 못 만나고 끝났으면 계획 실패.
            result.Found          = false;
            result.StatesExplored = closed.Count;
            return result;
        }

        // [Heuristic] 미충족 목표 사실의 개수.
        // admissible — 각 미충족 사실을 해결하려면 적어도 1 행동이 필요하므로 *과대평가하지 않는다*.
        // 더 강한 휴리스틱(예: relaxed planning graph) 도 가능하지만 학습용으로는 이 단순한 카운트가 명확.
        private static float Heuristic(WorldState s, WorldState goal)
            => s.CountUnsatisfied(goal);

        // [Reconstruct] cameFrom 체인을 거꾸로 따라가며 행동 시퀀스 복원.
        private static void Reconstruct(
            WorldState end,
            Dictionary<WorldState, WorldState> fromState,
            Dictionary<WorldState, GOAPAction> fromAction,
            GOAPPlan result)
        {
            var stack = new Stack<GOAPAction>();
            var s = end;
            while (fromState.TryGetValue(s, out var prev))
            {
                stack.Push(fromAction[s]);
                s = prev;
            }
            while (stack.Count > 0) result.Actions.Add(stack.Pop());
        }
    }
}
