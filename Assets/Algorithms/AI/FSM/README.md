# FSM — 유한 상태 기계 (Finite State Machine)

디자이너가 *상태 그래프* 를 직접 그린다 — 어느 상태에서 어느 상태로, 어떤 조건이 만족되면 갈지 *전부 명시적으로* 정의. AI 는 매 tick 현재 상태에서 나가는 전이만 검사하고, 조건이 맞는 첫 전이를 따라 상태를 *바꾼다*. 그게 전부. AI 의사결정의 가장 클래식한 형태이며, Pac-Man 의 유령부터 모던 게임의 *애니메이션 상태 머신* (Unity Animator) 까지 살아있는 패턴.

이 폴더는 격자 위 **경비병 NPC** 데모 — Player 가 WASD 로 NPC 의 시야에 직접 들어가/나오면서 *명시적 전이 조건* 을 트리거하는 모습을 시각화한다. 5 상태 (Patrol/Chase/Attack/Flee/Heal) + 7 전이 + 우측 패널의 *상태 다이어그램* 이 핵심.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `FSMAlgorithm.cs` | 순수 FSM 프레임워크. `FSMState` (Name + OnEnter/OnUpdate/OnExit), `FSMTransition` (From/To/Condition/Label), `FSMachine` (`Tick` + first-match-wins + LastTransition 추적). Unity 의존 0. |
| `FSMGridVisualizer.cs` | Unity MonoBehaviour. 격자 + Capsule NPC + Capsule Player(WASD) + Heal 거점. 5 상태 람다와 7 전이 조건이 한 메서드(`BuildFSM`)에 모여 위에서 아래로 읽힘. Chase / Flee 상태가 `AStarAlgorithm.FindPath` 호출 → FSM × A\* 협업. NPC 시야 반경을 격자에 옅은 노란색으로 표시 (Patrol/Chase 상태일 때만). 우측 OnGUI 패널에 *상태별 outgoing 전이 리스트* + 최근 전이 강조. |

> Search 카테고리의 `AStarAlgorithm` + `WeightedGraph` 를 그대로 import 해 쓴다 (`using Algorithms.Search;`). FSM 자체는 길찾기를 모르며, *Chase / Flee 상태의 OnUpdate 안에서만* 격자 A\* 를 호출 — BT × A\*, GOAP × A\*, UAI × A\* 와 같은 협업 패턴.

## 학습 노트 (Notion)

다음 자료는 모두 Notion에 정리되어 있다.

- 알고리즘 개요 / 왜 "명시적 그래프" 인가
- "first match wins" 규칙과 그것이 디자이너의 *우선순위 표현 도구* 인 이유
- FSM 폭발 (state explosion) 문제와 그 해결책 (Hierarchical FSM, Pushdown Automata)
- BT vs FSM vs UAI vs GOAP 비교 — 같은 NPC 행동을 4 방식으로 표현했을 때의 차이
- Animator (Unity 내장 애니메이션 상태 머신) 가 FSM 의 어떤 응용인지
- 직접 만져보며 확인하기 (시야 반경, 전투 비용, 회복 속도, patrolPoints 변경, 새 상태/전이 추가)

> **FSM 학습 노트**: [FSM — Notion](https://www.notion.so/3545e18a57368122a69de02130727cf1)

## 코드 안 학습 주석

- `FSMAlgorithm.cs` — 5 섹션 헤더 (개요 / 동작 흐름 / 복잡도 / 사용 자료구조 / 다른 알고리즘과의 관계). 라인 주석에 `[①②③]` 단계 마커.
- `FSMGridVisualizer.cs` — 데모 시나리오, 5 상태 / 7 전이 표, FSM × A\* 협업 설명, 색상표, 우측 OnGUI 패널 사용법.

## 빠른 사용법

1. 새 씬 만들기 — Project 창에서 `Assets/Scenes/BehaviorTree.unity` 를 `Ctrl+D` 로 복제 → `FSM.unity` 로 이름 변경
2. 씬 안 GameObject 의 기존 `BehaviorTreeVisualizer` 컴포넌트 제거 → `FSMGridVisualizer` 추가
3. `AlgorithmDemoUI` 의 Demo Component 슬롯을 그 GameObject 로 다시 연결
4. 카메라 위치 권장: 격자 가운데 위에서 내려다보기 (예: position y=14, rotation x=90°)
5. Build Settings → Scenes In Build 에 `FSM.unity` 등록
6. Play → NPC(회색) 가 4 코너 patrolPoints 를 순회. WASD 로 Player(파랑) 를 NPC 시야(노랑 셀) 안으로 이동시키면 → NPC 가 노랑(Chase) 으로 변하고 추격
7. 인접 칸까지 다가가면 → NPC 가 빨강(Attack) 으로. NPC HP 가 30 미만으로 떨어지면 → 청록(Flee) 으로 Heal 거점으로 도망. 거점 도착 시 → 초록(Heal) 로 회복. HP 100 회복하면 → 다시 Patrol 로 복귀

## 색상 의미

| 대상 | 색 | 의미 |
|------|----|------|
| 셀 | 옅은 회색 | 빈 칸 |
| 셀 | 검정 | 벽 (이동 불가) |
| 셀 | 옅은 청록 | 패트롤 포인트 |
| 셀 | 옅은 노랑 | NPC 시야 반경 (Patrol/Chase 일 때만) |
| 셀 | 초록 | Heal 거점 |
| Capsule | 파랑 | Player |
| Capsule | 회색 | NPC — Patrol |
| Capsule | 노랑 | NPC — Chase |
| Capsule | 빨강 | NPC — Attack |
| Capsule | 청록 | NPC — Flee |
| Capsule | 초록 | NPC — Heal |

## 우측 패널 읽는 법

```
FSM — 유한 상태 기계
현재 상태: Chase  /  HP: 78/100
NPC (4,5)  /  Player (6,5)

NPC HP   [██████████████░░░░░] (초록/노랑/빨강)
WASD / 화살표 — Player 한 칸 이동

─── 상태 머신 (등록 순서 = 우선순위) ───
   Patrol
      → Chase   :  시야 진입
▶  Chase
      → Attack  :  인접 (공격 범위)
      → Patrol  :  시야 잃음
   Attack
      ✦ Flee    :  HP < threshold
      → Chase   :  인접 벗어남
   Flee
      → Heal    :  Heal 거점 도착
   Heal
      → Patrol  :  HP == 최대

─── 최근 전이 ───
  ✦ 0.4초 전: Patrol → Chase  (시야 진입)
```

- **현재 상태** — `▶` 와 노란 강조로 표시. 색상도 NPC 캡슐 색과 동기화.
- **상태 머신 다이어그램** — 각 상태 노드 아래에 *그 상태에서 나가는 전이* 들이 들여쓰기. 디자이너가 짠 *전이 표 그대로*.
- **`✦` 흰색 표시** — 방금 발생한 전이 (1.2 초 안). 그래프의 어느 화살표가 빛났는지 *즉시* 보인다.
- **최근 전이 줄** — "N.N 초 전" 으로 마지막 전이를 별도 강조. 디버깅 / 학습 시 "방금 무슨 일이 일어났는지" 한 눈.

## 변형 실험

`FSMGridVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- **`Sight Range`** — 시야 반경. 7 로 늘리면 NPC 가 *훨씬 일찍* Chase 로 전환 → 시야의 노란색 셀 영역도 크게.
- **`Hp Low Threshold`** — Flee 발동 임계값. 80 으로 올리면 NPC 가 *조금만 다쳐도* 도망. 10 으로 낮추면 죽을 때까지 안 도망.
- **`Hp Damage Per Attack`** — 전투 1 tick 당 NPC 가 입는 피해. 30 으로 올리면 *거의 한 방* 에 Flee 로.
- **`Tick Interval`** — FSM 한 tick 의 주기. 0.05 로 낮추면 빠른 시연, 0.6 으로 늘리면 *전이 순간* 을 천천히 따라가며 학습 가능.
- **`Patrol Points`** — patrol 경로 자체를 바꿔보기. 점 1 개만 두면 NPC 가 그 위치에서 정지 (patrol 의 약한 형태).
- **새 상태 추가** (`BuildFSM()` 직접 수정) :
  - 예: `Search` 상태 — Chase 에서 시야를 잃었을 때 마지막으로 봤던 위치로 가서 *몇 tick 둘러본 뒤* Patrol 로 복귀. Chase → Patrol 의 직접 전이를 Chase → Search → Patrol 로 분해.
  - 새 상태 1 개 추가 = 노드 1 개 + 전이 2~3 개 추가. **이게 FSM 폭발 문제의 시작점** — 상태가 많아질수록 전이가 *제곱으로* 늘어남.
- **전이 등록 순서 바꾸기** — `Attack → Flee` 를 *맨 뒤* 로 옮기면, NPC 가 HP 가 낮아도 Attack → Chase 가 먼저 매칭되어 *Flee 가 영영 안 발화* 하는 모습. **first-match-wins** 의 직관.

## 다른 알고리즘과의 비교

| 항목 | FSM (이 폴더) | Behavior Tree | Utility AI | GOAP |
|------|---|---|---|---|
| 결정 단위 | 상태 + 전이 | 트리 평가 (단락) | 점수 비교 (매 tick) | 상태 공간 A\* |
| 디자이너의 일 | 상태 + 전이 표 | 결정 트리 | Consideration / Curve | 행동 카탈로그 |
| 명시성 | ✅ 가장 높음 | ✅ 트리 자체 | ⚠️ 점수 함수의 합 | ⚠️ 창발적 |
| 새 상황 추가 | 상태/전이 추가 → *제곱 폭발* 위험 | 트리 분기 추가 | 행동 추가 → 자동 활용 | 행동 추가 → 자동 활용 |
| 환경 변화 반응 | 전이 조건이 즉시 매칭 | 매 tick 재평가 | 매 tick 재평가 | ⚠️ 사슬 중엔 둔감 |
| 디버깅 | ✅ 그래프 그대로 보면 끝 | ✅ 트리 보면 답 | ✅ 점수 표 | ⚠️ 창발적 사슬 |
| 매번 비용 | O(나가는 전이 수) — 거의 공짜 | O(노드 수) | O(A·C) | A\* over states |
| 적합한 상황 | 상태 \< 10 의 클래식 NPC, 애니메이션 | 상태 머신 류 NPC 일반 | 욕구/상황 가중 NPC | 자율 NPC, F.E.A.R. 류 |
| 외부 알고리즘 | OnUpdate 안에 무엇이든 | Action 안에 무엇이든 | Move 행동 안에 A\* | Move 행동 안에 A\* |

> **이 폴더의 핵심 메시지** : FSM 은 **명시성** 의 극단 — 디자이너가 상태와 전이를 *전부 손으로 그린다*. Utility AI / GOAP 가 디자이너의 일을 줄이는 방향이라면, FSM 은 *전부 수동으로 통제* 하는 방향. 그래서 디버깅과 QA 가 가장 쉬우며, 상태가 적을 때는 가장 빠르고 가벼운 선택. 단점은 "FSM 폭발" — 상태가 많아지면 전이가 제곱으로 늘어나 관리 불가능. 그래서 모던 게임은 보통 *애니메이션은 FSM (Animator), 의사결정은 BT/UAI/GOAP* 로 책임을 분리해 쓴다.
