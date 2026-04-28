# Behavior Tree — 모던 NPC AI 의 표준 패턴

AI 의 의사결정을 *트리 구조* 로 표현하고 매 tick 마다 루트부터 평가하는 기법.
Halo 2 (2004) 가 대중화한 이후 모던 AAA NPC AI 의 사실상 표준 (Unreal 엔진 기본 기능).

이 폴더는 격자 위 **NPC Capsule** 이 BT 결정에 따라 행동하는 데모 — 같은 격자 위에서 **A\* 와 협업** 하는 모습 ("BT 가 *어디로* 갈지 결정 + A\* 가 *어떻게* 갈지 계산") 을 직접 시각화한다.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `BehaviorTreeAlgorithm.cs` | 순수 BT 프레임워크. `NodeStatus`, `BTNode` 베이스 + Composite (Sequence/Selector) + Decorator (Inverter) + Leaf (Condition/Action). 람다 기반 콜백으로 *블랙보드를 클로저로 캡처* 하는 패턴 |
| `BehaviorTreeVisualizer.cs` | Unity MonoBehaviour. 격자 + Capsule NPC/Player + Patrol 포인트 + 우측 OnGUI 트리 패널. `Chase` 액션이 `AStarAlgorithm.FindPath` 를 호출 → BT × A\* 협업 시각화 |

> Search 카테고리의 `AStarAlgorithm` 과 `WeightedGraph` 를 그대로 import 해 사용한다 (`using Algorithms.Search;`). 이 *cross-category 의존* 자체가 학습 포인트 — "각 알고리즘은 단독이 아니라 *조합* 되어 게임 AI 가 된다" 는 것을 코드 레벨에서 보여준다.

## 학습 노트 (Notion)

다음 자료는 모두 Notion에 정리되어 있다.

- 알고리즘 개요 / 4 종류 노드 (Sequence/Selector/Decorator/Leaf) / 동작 흐름 / 시간·공간 복잡도
- BT vs GOAP / Minimax / FSM / Utility AI 비교 (각 기법의 트레이드오프)
- BT × A\* 협업 다이어그램 ("BT 가 결정 + A\* 가 실행" 의 표준 분업)
- Sequence/Selector 의 *단락 평가* 가 만들어내는 우선순위 표
- Running 상태가 여러 tick 에 걸친 행동을 어떻게 표현하는지
- 직접 만져보며 확인하기 (HP 임계값 조절 → 도주 빈도, sightRange 조절 → 추격 시작 거리, 패트롤 포인트 추가/제거)

> **BT 학습 노트**: [Behavior Tree — Notion](https://www.notion.so/3505e18a573681d9b5d6edac313c44c8)

## 코드 안 학습 주석

각 `.cs` 파일 상단에는 한국어 헤더 블록이 있다.

- `BehaviorTreeAlgorithm.cs` — 5 섹션 헤더 + 노드 종류별 헤더 주석. 각 Composite 의 *단락 평가 규칙* 을 라인 주석에 명시 (`Sequence` 는 Failure 만나면 멈춤, `Selector` 는 Success 만나면 멈춤).
- `BehaviorTreeVisualizer.cs` — 데모 시나리오, BT 트리 다이어그램, BT × A\* 협업 설명, 우측 OnGUI 패널 사용법.

## 빠른 사용법

1. 새 씬 만들기 — Project 창에서 `Assets/Scenes/MCTS.unity` (또는 다른 씬) 을 `Ctrl+D` 로 복제 → `BehaviorTree.unity` 로 이름 변경
2. 씬 안 GameObject 의 기존 Visualizer 컴포넌트 제거 → `BehaviorTreeVisualizer` 추가
3. (선택) `AlgorithmDemoUI` 의 Demo Component 슬롯을 새 GameObject 로 다시 연결
4. 카메라 위치 권장: 격자 가운데 위에서 내려다보기 (예: position y=14, rotation x=90°)
5. Build Settings → Scenes In Build 에 `BehaviorTree.unity` 등록
6. Play → Player(파랑) 를 WASD/방향키로 이동 → NPC(회색) 가 시야에 들어가면 노란색으로 추격, 인접하면 빨강 깜빡 공격, HP 낮아지면 청록색으로 도주

## 색상 의미

| 대상 | 색 | 의미 |
|------|----|------|
| 셀 | 옅은 회색 | 빈 칸 |
| 셀 | 검정 | 벽 (이동 불가) |
| 셀 | 옅은 청록 | 패트롤 포인트 |
| Capsule | 파랑 | Player (사용자 조작) |
| Capsule | 회색 | NPC — Idle / Patrol |
| Capsule | 노랑 | NPC — Chase (적 발견, A\* 추격 중) |
| Capsule | 빨강 | NPC — Attack (인접 공격 중) |
| Capsule | 청록 | NPC — Flee (HP 낮아 도주) |

## 우측 트리 패널 읽는 법

매 tick 의 BT 평가 결과가 *트리 구조 그대로* 텍스트로 갱신된다:

```
[●] [Sel] Root  ✓
   [○] [Seq] Survive  ✗      ← HP 충분 → Survive 실패 → 다음 가지
      [○] [Cond] IsHpLow  ✗
      [ ] [Act] Flee           ← Survive 가 짧게 끝나서 평가 안 됨
   [○] [Seq] Combat  ✗        ← 적 인접 X → Combat 실패 → 다음 가지
      [○] [Cond] IsInAttackRange  ✗
      [ ] [Act] Attack
   [●] [Seq] Chase  ✓         ← 적 보임 + Chase 한 칸 성공 → 활성!
      [●] [Cond] IsPlayerVisible  ✓
      [●] [Act] Chase (A*)  ✓
   [ ] [Act] Patrol            ← Chase 가 성공해서 Patrol 은 평가 안 됨
```

- `●` = 이번 tick 에 평가된 *활성 경로* 노드
- `○` = 평가됐으나 Failure 라 단락 평가로 다음 가지로 넘어간 노드
- `공란` = 단락 평가 덕에 *평가 자체를 안 한* 노드 (BT 의 효율성)
- `✓ ✗ …` = 마지막 결과 (Success / Failure / Running)

## 변형 실험

`BehaviorTreeVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- **`Tick Interval`** — 0.05 로 줄이면 NPC 가 매우 빠르게 결정. 0.5 로 늘리면 사람이 트리 평가를 천천히 따라갈 수 있다 (학습용).
- **`Hp Low Threshold`** — 50 으로 올리면 NPC 가 *훨씬 자주* 도주 → Survive 분기가 자주 활성. BT 의 *반응성* 이 분명히 드러남.
- **`Sight Range`** — 1 로 줄이면 거의 Patrol 만, 큰 값으로 늘리면 Player 가 어디 있든 Chase.
- **`Wall Ratio`** — 0.3 이상이면 미로처럼 변해 A\* 의 우회 경로 계산이 분명히 보임 (BT × A\* 협업의 진가).
- **`Patrol Points`** 배열 변경 — 패트롤 패턴이 트리 *수정 없이도* 바뀐다 (블랙보드 데이터만 변경).
- **트리 구조 자체 변경** — `BuildBT()` 에서 Survive ↔ Combat 순서를 바꿔 보면 우선순위 변화가 즉시 행동 변화로 드러난다 (예: HP 낮아도 인접한 적은 일단 공격 → 자살돌격 NPC).

## 다른 알고리즘과의 비교

| 항목 | BT (이 폴더) | Minimax / MCTS | GOAP |
|------|---|---|---|
| 의사결정 주체 | 디자이너 (트리를 그림) | AI (트리 탐색) | AI (행동 사슬 자동 계획) |
| 매 tick 비용 | O(노드 수) | O(b^d) / O(iterations) | O(A* on action space) |
| 예측 가능성 | ✅ 높음 | ⚠️ Minimax 결정적 / MCTS 비결정적 | ⚠️ 창발적 |
| 디버깅 | ✅ 트리 보면 즉답 | ⚠️ 점수/통계 추적 | ⚠️ 사슬 추적 |
| 외부 알고리즘과의 협업 | ✅ Action 안에 무엇이든 호출 가능 (이 데모: A\*) | 일반적으로 단독 | A\* 자체 |
| 적합한 상황 | NPC 일반 행동 (대부분의 적/동료) | 보드게임 / 대전 | 자율 NPC, F.E.A.R. 류 |

> **이 폴더의 핵심 메시지**: BT 는 *알고리즘 자체* 보다 *다른 알고리즘들을 조합하는 컨테이너* 로서의 가치가 크다. Action 노드 안에 BFS 든 A\* 든 무엇이든 들어갈 수 있고, BT 는 그것들이 *언제* 호출될지를 결정한다. 이 데모의 `Chase` 액션이 그 살아 있는 예시다.
