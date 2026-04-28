using System;
using System.Collections.Generic;

namespace Algorithms.AI
{
    /// <summary>
    /// =====================================================================
    ///  Behavior Tree (BT) — 모던 AAA 의 표준 NPC AI 패턴
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// AI 의 의사결정을 *트리 구조* 로 표현하고 매 tick 마다 루트부터 평가하는 기법.
    /// Halo 2 (2004) 가 대중화한 이후 모던 AAA NPC AI 의 사실상 표준이 됨 (Unreal 엔진 기본 기능).
    ///
    /// GOAP / Minimax 와의 결정적 차이:
    ///   - GOAP / Minimax : AI 가 *행동 시퀀스/점수* 를 알아서 계산.
    ///   - BT             : 디자이너가 *결정 규칙 자체* 를 트리로 그려둠. AI 는 그걸 따라 흐를 뿐.
    ///
    /// 즉, BT 의 "지능" 은 트리를 만든 *디자이너* 의 것이고, 알고리즘은 *그 트리를 평가하는 메커니즘* 일 뿐.
    /// 이 단순함이 BT 의 강점 (예측 가능 + 디버깅 용이 + 비주얼 에디터 친화적).
    ///
    /// 이 프로젝트의 다른 알고리즘과의 관계:
    ///   - 본질은 *트리 순회 + DFS* (Selector/Sequence 가 자식을 왼쪽부터 돌며 결과를 모음).
    ///   - 격자 위 NPC 가 "어디로 갈지" 는 BT 가 결정하지만, "어떻게 갈지" 는 A* 에 위임 가능.
    ///     → BT 와 A* 의 *협업* 이 BehaviorTreeVisualizer 에서 직접 시각화된다.
    ///
    /// 2. 동작 흐름
    /// ---------------------------------------------------------------------
    ///   ① 매 tick (= 정해진 주기, 보통 0.1~0.3 초) 마다 루트 노드에 Tick() 호출.
    ///   ② 각 노드는 자식을 평가 (또는 자기 행동 수행) 후 셋 중 하나를 반환:
    ///        Success — 성공 (목표 달성)
    ///        Failure — 실패 (못 함 / 조건 불일치)
    ///        Running — 진행 중 (여러 tick 에 걸침)
    ///   ③ Composite 노드 (Sequence / Selector) 가 자식 결과를 모아 자기 결과를 결정.
    ///   ④ 루트가 결과를 반환하면 한 tick 끝. 다음 tick 에 다시 루트부터.
    ///
    ///   ※ 매 tick 루트부터 다시 시작 = *반응성*. HP 가 갑자기 낮아지면 다음 tick 에서 즉시
    ///     높은 우선순위(Survive) 분기로 점프 → 게임 AI 의 자연스러운 반응이 자동으로 구현됨.
    ///
    /// 3. 노드 종류
    /// ---------------------------------------------------------------------
    ///   Composite (자식 여러 개)
    ///     - Sequence (→) : AND. 자식을 왼쪽부터 평가. 모두 Success 여야 Success, 하나라도 Failure 면 즉시 Failure.
    ///                      "조건 검사 → 그게 OK 면 행동 실행" 패턴의 표준.
    ///     - Selector (?) : OR.  자식을 왼쪽부터 평가. 하나라도 Success 면 즉시 Success, 모두 Failure 면 Failure.
    ///                      "우선순위 순으로 시도, 안 되면 다음" 패턴의 표준.
    ///   Decorator (자식 하나 변형)
    ///     - Inverter     : 자식의 Success ↔ Failure 뒤집기. Running 은 그대로.
    ///                      "조건의 부정" 을 표현할 때 유용.
    ///   Leaf (실제 일을 하는 노드)
    ///     - Condition    : 세상 상태를 검사 (IsHpLow? IsEnemyVisible?). Success / Failure 만 반환.
    ///     - Action       : 실제 행동 수행 (Move, Attack, Reload). 한 tick 안에 끝나면 Success/Failure,
    ///                      여러 tick 에 걸치면 Running 을 반환해 다음 tick 에 이어서 평가하게 한다.
    ///
    ///   Sequence + Selector + Action + Condition 만으로도 대부분의 NPC AI 가 표현 가능.
    ///   Inverter 등 Decorator 는 표현력 보강용.
    ///
    /// 4. 시간 / 공간 복잡도
    /// ---------------------------------------------------------------------
    ///   - 시간 (한 tick) : O(노드 수). 트리가 깊더라도 매 tick 평가 비용은 노드 개수에 비례.
    ///                      Composite 의 단락 평가 (Sequence 는 Failure 만나면 즉시 멈춤) 때문에
    ///                      *실제로는* 훨씬 적은 노드만 평가됨.
    ///   - 공간          : O(트리 노드 수). 트리는 정적 (실행 중 자라지 않음).
    ///                      호출 스택 깊이 = 트리 깊이.
    ///
    ///   Minimax / MCTS 와의 큰 차이: 트리 *탐색* 이 아니라 트리 *평가*. 가지가 폭발하지 않는다.
    ///
    /// 5. 사용한 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   - 노드 객체 (BTNode)        : 트리는 *디자이너가 만든 정적 구조* 라 객체 그래프가 가장 자연스러움.
    ///                                Visit 패턴 / Strategy 패턴이 자연스럽게 어울린다.
    ///   - List&lt;BTNode&gt; (자식 목록) : Composite 가 자식 순서를 보존하고 순차 접근하므로 List 가 적합.
    ///   - Func&lt;bool&gt; / Func&lt;NodeStatus&gt; (Condition/Action 의 콜백)
    ///                              : 노드 *클래스* 를 매번 새로 만들지 않고 *람다* 로 행동을 주입.
    ///                                트리 구축 시점에 *블랙보드(공유 상태)를 클로저로 캡처* 하면
    ///                                노드끼리 데이터를 주고받는 별도 메커니즘이 필요 없어진다.
    ///   - LastStatus / TickedThisFrame (시각화용 메타 필드)
    ///                              : 어느 노드가 활성됐고 결과가 무엇인지를 시각화 측이 즉시 읽을 수 있게 함.
    ///                                알고리즘 자체엔 영향 없는 *관찰자용* 필드.
    ///
    /// ▶ 실전 BT 라이브러리에는 더 많은 노드 (Parallel / Cooldown / Repeater / Subtree / Service 등)
    ///   가 있지만, 이 파일은 *학습용 최소 셋* 만 포함한다. 위 다섯 (Sequence, Selector, Inverter,
    ///   Condition, Action) 만으로도 의미 있는 NPC AI 를 만들 수 있다.
    /// </summary>
    public enum NodeStatus
    {
        /// <summary>이 노드가 자기 일을 끝까지 해냈다.</summary>
        Success,
        /// <summary>이 노드가 자기 일을 못 했다 / 조건이 안 맞다.</summary>
        Failure,
        /// <summary>이 노드가 여러 tick 에 걸쳐 진행 중. 다음 tick 에 이어서 평가될 것.</summary>
        Running
    }

    // ─────────────────────────────────────────────────────────────────────
    // BTNode — 모든 노드의 베이스 클래스
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 모든 BT 노드의 베이스. 추상 Tick() 하나만 강제하면 어떤 노드든 만들 수 있다.
    ///
    /// LastStatus / TickedThisFrame 은 알고리즘에는 무관한 *시각화용* 필드.
    /// (실전 라이브러리에선 디버그 빌드에서만 살리는 #if 분기로 처리하기도 한다.)
    /// </summary>
    public abstract class BTNode
    {
        /// <summary>UI 트리 출력에 쓰는 노드 이름. "Survive", "IsHpLow" 등.</summary>
        public string Name;

        /// <summary>가장 최근 Tick 의 결과. 시각화에서 ✓/✗/… 마크 표시에 사용.</summary>
        public NodeStatus LastStatus = NodeStatus.Failure;

        /// <summary>이번 프레임에 평가됐는가 (= 활성 경로에 속하는가). 시각화 강조용.</summary>
        public bool TickedThisFrame;

        /// <summary>이 노드를 평가하고 결과를 반환한다. 각 서브클래스가 정의.</summary>
        public abstract NodeStatus Tick();

        /// <summary>
        /// 매 tick 시작 시 *전체 트리* 의 TickedThisFrame 플래그를 리셋한다.
        /// Composite/Decorator 는 자식들에게 재귀 전파하도록 override.
        /// </summary>
        public virtual void ResetTickFlag()
        {
            TickedThisFrame = false;
            foreach (var c in EnumerateChildren()) c.ResetTickFlag();
        }

        /// <summary>자식 순회. UI 측이 트리 구조를 그릴 때 사용.</summary>
        public virtual IEnumerable<BTNode> EnumerateChildren() { yield break; }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Composite 노드 — 자식 여러 개를 가진다
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>자식 여러 개를 갖는 노드의 공통 베이스. List&lt;BTNode&gt; 보유.</summary>
    public abstract class CompositeNode : BTNode
    {
        public readonly List<BTNode> ChildList = new();

        public override IEnumerable<BTNode> EnumerateChildren()
        {
            foreach (var c in ChildList) yield return c;
        }

        /// <summary>편의 빌더: 트리 구축을 체이닝 스타일로 작성할 수 있게 해 준다.</summary>
        public CompositeNode Add(BTNode child) { ChildList.Add(child); return this; }
    }

    /// <summary>
    /// Sequence (→) : AND.
    /// 자식을 왼쪽부터 평가. 하나라도 Failure 면 즉시 Failure 반환 (단락 평가).
    /// Running 을 만나면 진행 중이므로 거기서 멈추고 Running 반환.
    /// 모두 Success 여야 Success.
    ///
    /// 사용 예: "조건 → 행동" 패턴.
    ///   Sequence("Combat") {
    ///     Condition("IsEnemyVisible"),
    ///     Action("Attack")
    ///   }
    /// 적이 보일 때만 공격 실행. 조건이 거짓이면 Action 은 시도조차 안 됨.
    /// </summary>
    public class Sequence : CompositeNode
    {
        public override NodeStatus Tick()
        {
            TickedThisFrame = true;

            foreach (var child in ChildList)
            {
                var status = child.Tick();
                // Failure 또는 Running 을 만나면 그 결과를 그대로 위로 전달.
                // Success 만이 다음 자식으로 넘어가는 신호다.
                if (status != NodeStatus.Success)
                {
                    LastStatus = status;
                    return status;
                }
            }

            // 모든 자식이 Success → Sequence 도 Success.
            LastStatus = NodeStatus.Success;
            return NodeStatus.Success;
        }
    }

    /// <summary>
    /// Selector (?) : OR.
    /// 자식을 왼쪽부터 평가. 하나라도 Success 면 즉시 Success 반환 (단락 평가).
    /// Running 을 만나도 거기서 멈추고 Running 반환 (그 자식이 진행 중이므로).
    /// 모두 Failure 여야 Failure.
    ///
    /// 사용 예: 우선순위 분기.
    ///   Selector("Root") {
    ///     Sequence("Survive"),  // 1순위: HP 낮으면 도망
    ///     Sequence("Combat"),   // 2순위: 적 보이면 싸움
    ///     Action("Patrol")      // 기본: 순찰
    ///   }
    /// 위쪽 가지가 Failure 일 때만 다음 가지를 시도 → 트리 구조 자체가 우선순위 표.
    /// </summary>
    public class Selector : CompositeNode
    {
        public override NodeStatus Tick()
        {
            TickedThisFrame = true;

            foreach (var child in ChildList)
            {
                var status = child.Tick();
                // Success 또는 Running 을 만나면 그 결과 그대로 위로 전달.
                // Failure 만이 다음 자식을 시도하라는 신호다.
                if (status != NodeStatus.Failure)
                {
                    LastStatus = status;
                    return status;
                }
            }

            // 모든 자식이 Failure → Selector 도 Failure.
            LastStatus = NodeStatus.Failure;
            return NodeStatus.Failure;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Decorator 노드 — 자식 하나를 감싸 결과를 변형한다
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>자식 하나만 갖는 노드의 공통 베이스.</summary>
    public abstract class DecoratorNode : BTNode
    {
        public BTNode Child;

        public override IEnumerable<BTNode> EnumerateChildren()
        {
            if (Child != null) yield return Child;
        }
    }

    /// <summary>
    /// Inverter : Success ↔ Failure 뒤집기. Running 은 그대로 통과.
    /// "조건의 부정" 을 표현할 때 유용 (e.g., "적이 *안* 보일 때만").
    /// </summary>
    public class Inverter : DecoratorNode
    {
        public override NodeStatus Tick()
        {
            TickedThisFrame = true;

            if (Child == null)
            {
                LastStatus = NodeStatus.Failure;
                return LastStatus;
            }

            var s = Child.Tick();
            LastStatus = s switch
            {
                NodeStatus.Success => NodeStatus.Failure,
                NodeStatus.Failure => NodeStatus.Success,
                _                  => NodeStatus.Running, // Running 은 통과
            };
            return LastStatus;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Leaf 노드 — 실제 검사 / 행동을 수행한다
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Condition : 세상 상태를 검사. Success 또는 Failure 만 반환 (Running 없음).
    ///
    /// Predicate 는 람다로 받는다. 트리 구축 시점에 *블랙보드(공유 상태)* 를
    /// 클로저로 캡처해 두면 노드 간 데이터 전달이 자연스럽게 해결된다:
    ///
    ///   var bb = new NPCBlackboard();
    ///   new Condition { Name="IsHpLow", Predicate = () => bb.Health &lt; 30 }
    /// </summary>
    public class Condition : BTNode
    {
        public Func<bool> Predicate;

        public override NodeStatus Tick()
        {
            TickedThisFrame = true;
            // Predicate 가 null 이면 안전하게 Failure 처리 (학습용 — 실전이면 throw 가 더 명확).
            bool ok = Predicate != null && Predicate();
            LastStatus = ok ? NodeStatus.Success : NodeStatus.Failure;
            return LastStatus;
        }
    }

    /// <summary>
    /// Action : 실제 행동 수행. Success / Failure / Running 모두 반환 가능.
    ///
    /// 한 tick 안에 끝나는 행동 (e.g., "Attack 한 번") → Success.
    /// 여러 tick 에 걸치는 행동 (e.g., "그 칸까지 걷기") → 진행 중엔 Running, 도착하면 Success.
    /// 실패하는 행동 (e.g., "경로 없음") → Failure.
    ///
    /// Behavior 도 람다로 받는다. 블랙보드를 클로저로 캡처하는 패턴이 표준.
    /// </summary>
    public class Action : BTNode
    {
        public Func<NodeStatus> Behavior;

        public override NodeStatus Tick()
        {
            TickedThisFrame = true;
            // Behavior 가 null 이면 Failure (학습용 안전 처리).
            LastStatus = Behavior != null ? Behavior() : NodeStatus.Failure;
            return LastStatus;
        }
    }
}
