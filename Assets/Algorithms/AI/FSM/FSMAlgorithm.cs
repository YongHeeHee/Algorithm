using System;
using System.Collections.Generic;

namespace Algorithms.AI
{
    /// <summary>
    /// =====================================================================
    ///  FSM (Finite State Machine) — 유한 상태 기계
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// FSM 은 *AI 의사결정의 클래식* 이자 가장 단순한 형태다. 디자이너가 직접
    ///   - **상태(State)** : NPC 가 *지금 무엇을 하고 있는지*
    ///   - **전이(Transition)** : *어느 상태에서 어느 상태로* 갈지 + *언제* 갈지의 조건
    /// 두 가지를 *전부 명시적으로* 그려둔다. AI 는 매 tick 현재 상태에서 나가는 전이만 검사하고,
    /// 조건을 만족하는 전이가 있으면 다른 상태로 *바뀐다*. 그게 전부.
    ///
    ///   "BT/UAI 가 *매 tick 다시 결정* 한다면, FSM 은 *결정한 상태를 *지속* 한다*"
    ///
    /// FSM 의 본질은 **명시성**. 디자이너가 그래프로 그릴 수 있고, QA 가 모든 가능 상태와 전이를
    /// 표로 적을 수 있다. 클래식 NPC (Pac-Man 의 유령, 슈퍼마리오의 적, 대부분의 레트로 게임)
    /// 의 표준 패턴이며, 모던 게임에서도 *애니메이션 상태 머신* (Unity Animator) 으로 살아있다.
    ///
    /// 2. 동작 흐름 (매 tick)
    /// ---------------------------------------------------------------------
    ///   ① 현재 상태 Current 에서 *나가는 전이* 들을 등록 순서대로 검사.
    ///   ② 첫 번째로 조건이 true 인 전이를 발견하면:
    ///        a. Current.OnExit() 호출.
    ///        b. LastTransition 에 기록 (시각화용).
    ///        c. Current = transition.To.
    ///        d. Current.OnEnter() 호출.
    ///   ③ Current.OnUpdate() 호출 (전이 후의 *새* 상태일 수 있음).
    ///
    ///   ※ "first match wins" — 같은 상태에서 여러 전이 조건이 동시에 true 면 *먼저 등록된* 전이가
    ///     이긴다. 이게 디자이너의 *우선순위 표현 도구* 다 (예: 위급한 Flee 전이를 Chase 전이보다
    ///     먼저 등록).
    ///
    ///   ※ 다른 변형:
    ///     - **Hierarchical FSM** : 상태 안에 또 FSM. 큰 상태 ("Combat") 안에 작은 상태들 ("Aim",
    ///        "Shoot", "Reload"). 본 구현은 평면 FSM.
    ///     - **Pushdown Automata** : 상태 *스택*. "이 상태 끝나면 직전 상태로 복귀" 가 자연스러움.
    ///
    /// 3. 시간 / 공간 복잡도
    /// ---------------------------------------------------------------------
    ///   - 시간 (per tick) : O(T_out) — Current 에서 나가는 전이 수만큼만 검사. 보통 1~5.
    ///   - 공간            : O(S + T) — 상태 수 S, 전이 수 T.
    ///
    ///   사실상 *공짜에 가깝다*. UAI 의 O(A·C), GOAP 의 A* 와 비교하면 압도적. 그래서 매 프레임
    ///   tick 해도 부담 없음. 그러나 표현력이 가장 약하다 — 모든 가능한 상황을 *디자이너가 직접*
    ///   상태/전이로 그려야 하므로, 상태가 많아지면 빠르게 폭발 ("FSM 폭발 문제").
    ///
    /// 4. 사용 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   - FSMState : Name + OnEnter/OnUpdate/OnExit 의 데이터 묶음.
    ///       *상태별 로직은 visualizer 에서 람다로 주입* 한다. 이게 학습 친화적인 이유는
    ///       "Patrol 이 무엇을 하는지" 가 알고리즘 파일이 아닌 *visualizer 한 곳* 에 모여 있어,
    ///       파일 하나만 위에서 아래로 읽으면 NPC 의 모든 행동이 이해되도록.
    ///   - FSMTransition : From/To/Condition/Label.
    ///       Condition 은 단순 `Func<bool>` — 디자이너가 매 전이마다 람다 한 줄로 정의.
    ///   - FSMachine : List&lt;FSMState&gt; + List&lt;FSMTransition&gt;.
    ///       *List 인 이유* — 등록 순서가 우선순위 (first match wins). HashSet/Dictionary 는
    ///       순서가 없어 부적절.
    ///   - LastTransition (시각화용)
    ///       : 방금 발생한 전이를 시간과 함께 보관 → 우측 패널에서 "1.4 초 전: Patrol → Chase" 같은
    ///         최근 전이 강조에 사용.
    ///
    /// 5. 다른 의사결정 알고리즘과의 관계
    /// ---------------------------------------------------------------------
    ///   - Behavior Tree : *매 tick 트리 재평가* — 우선순위가 트리 구조에 내장.
    ///                     FSM 보다 분기 추가가 쉽고 유지보수가 좋음 (대신 약간 더 무거움).
    ///   - Utility AI    : *매 tick 점수 비교* — 결정이 부드럽고 환경 변화에 즉각.
    ///                     FSM 보다 우선순위 미세 조정이 쉽지만 *flip-flop* 위험.
    ///   - GOAP          : *사슬을 자동 계획* — FSM 의 정반대 결.
    ///                     디자이너가 *순서를 안 짜도 되는* 게 강점이지만 디버깅 어려움.
    ///   - Animator      : Unity 의 애니메이션 상태 머신. 본질은 같은 FSM 이지만 transition 의
    ///                     duration/interrupt 같은 시간 축이 추가됨.
    ///   - HSM (Hierarchical) — FSM 폭발 문제의 해결책. 상태 안에 FSM 을 *재귀* 로.
    /// </summary>
    public sealed class FSMState
    {
        public string Name;
        // ※ System.Action 으로 명시 — 같은 Algorithms.AI 네임스페이스 안에 BT 의 'Action' 클래스가
        //    있어서 (`BehaviorTreeAlgorithm.cs`), 단순히 'Action' 이라고 쓰면 BT 의 Action 노드로
        //    해석되어 버린다. C# 은 same-namespace 타입을 using 으로 들여온 타입보다 먼저 찾기 때문.
        /// <summary>이 상태로 진입할 때 1 회 호출. 자원 잡기, 색 바꾸기 등.</summary>
        public System.Action OnEnter;
        /// <summary>이 상태에 머무는 동안 매 tick 호출. 핵심 행동 로직이 여기 들어간다.</summary>
        public System.Action OnUpdate;
        /// <summary>이 상태에서 나갈 때 1 회 호출. 자원 풀기, 정리 등.</summary>
        public System.Action OnExit;

        public override string ToString() => Name ?? "(noname)";
    }

    /// <summary>
    /// 전이 정의. *조건 람다 한 줄* 로 "어느 상태에서 어느 상태로 언제 갈지" 가 끝난다.
    /// Label 은 시각화 패널에서 화살표 옆에 표시되는 한국어 라벨 (예: "시야 진입").
    /// </summary>
    public sealed class FSMTransition
    {
        public FSMState From;
        public FSMState To;
        public Func<bool> Condition;
        public string Label;
    }

    /// <summary>
    /// FSM 의 본체. *순수 알고리즘* (Unity 의존 0).
    ///
    /// 사용 흐름:
    ///   1) AddState 로 상태들 등록 (OnEnter/OnUpdate/OnExit 람다 포함).
    ///   2) AddTransition 으로 전이 등록 (등록 순서 = 우선순위).
    ///   3) SetInitial 로 시작 상태 지정.
    ///   4) 매 tick Tick(now) 호출.
    ///
    /// "first match wins" 우선순위 규칙 덕분에, *위험한 전이를 먼저 등록* 하는 것만으로도
    /// 자연스럽게 안전 우선 행동을 만들 수 있다 (예: HP 낮으면 Flee → 전투 진행보다 우선).
    /// </summary>
    public sealed class FSMachine
    {
        public FSMState Current { get; private set; }

        /// <summary>방금 발생한 전이 (시각화 강조용). null 이면 아직 한 번도 전이 없음.</summary>
        public FSMTransition LastTransition { get; private set; }

        /// <summary>LastTransition 이 발생한 시각 (Time.time). 패널이 "N 초 전" 표시에 사용.</summary>
        public float LastTransitionTime { get; private set; } = float.NegativeInfinity;

        private readonly List<FSMState>      _states      = new();
        private readonly List<FSMTransition> _transitions = new();

        public IReadOnlyList<FSMState>      States      => _states;
        public IReadOnlyList<FSMTransition> Transitions => _transitions;

        public void AddState(FSMState s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            _states.Add(s);
        }

        public void AddTransition(FSMState from, FSMState to, Func<bool> condition, string label = null)
        {
            _transitions.Add(new FSMTransition
            {
                From      = from,
                To        = to,
                Condition = condition,
                Label     = label,
            });
        }

        /// <summary>시작 상태 설정. OnEnter 호출됨.</summary>
        public void SetInitial(FSMState s)
        {
            Current = s;
            Current?.OnEnter?.Invoke();
        }

        /// <summary>
        /// [①②③] 한 tick 진행 — 전이 검사 후 OnUpdate.
        /// now 는 Time.time — LastTransitionTime 기록에만 사용. 알고리즘 자체는 시간 모름.
        /// </summary>
        public void Tick(float now)
        {
            if (Current == null) return;

            // [①②] 현재 상태에서 나가는 전이를 등록 순서대로 검사. 첫 매치가 이김.
            foreach (var t in _transitions)
            {
                if (t.From != Current) continue;
                if (t.Condition == null || !t.Condition()) continue;

                // 전이 발생 — Exit → 기록 → State 변경 → Enter.
                Current.OnExit?.Invoke();
                LastTransition     = t;
                LastTransitionTime = now;
                Current            = t.To;
                Current.OnEnter?.Invoke();
                break;   // first match wins — 다른 전이는 다음 tick 까지 보지 않는다.
            }

            // [③] 현재 상태(=전이 후 일 수도 있음) Update.
            Current?.OnUpdate?.Invoke();
        }

        /// <summary>특정 상태에서 나가는 전이만 추출 — 시각화 패널이 상태별로 출력할 때 사용.</summary>
        public IEnumerable<FSMTransition> OutgoingFrom(FSMState s)
        {
            foreach (var t in _transitions)
            {
                if (t.From == s) yield return t;
            }
        }
    }
}
