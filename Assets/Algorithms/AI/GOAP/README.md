# GOAP — 목표 기반 자동 계획 AI (Goal-Oriented Action Planning)

디자이너는 *행동 카탈로그* (전제조건 / 효과 / 비용) 만 정의한다. 행동 사이의 *순서* 는 어디에도 정의하지 않는다. 그러면 GOAP 가 *현재 세상 상태 → 목표 상태* 로 가는 행동 시퀀스를 매번 자동으로 *계획* 한다 — 사실상 **상태 공간 위의 A\***. F.E.A.R. (2005) 가 적 AI 에 적용해 유명해진 패턴이며, 모던 게임의 자율적 NPC 의 표준 기법 중 하나.

이 폴더는 격자 위 **나무꾼/사냥꾼 NPC** 데모 — 같은 행동 카탈로그 위에서 *목표만 바꾸면* 전혀 다른 행동 사슬이 만들어지는 모습을 직접 시각화한다.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `GOAPAlgorithm.cs` | 순수 GOAP 프레임워크. `WorldState` (사실 집합 + 구조적 동등성), `GOAPAction` (Pre/Eff/Cost), `GOAPPlanner.Plan` (forward-chaining A* over state space). `Algorithms.Search.MinPriorityQueue` 재사용 |
| `GOAPGridVisualizer.cs` | Unity MonoBehaviour. 격자 + Capsule NPC + 5 개 거점(도끼/나무/모닥불/음식/적). Move 행동이 `AStarAlgorithm.FindPath` 를 호출 → GOAP × A\* 협업. 우측 OnGUI 패널에 목표/계획 통계/현재 사실/행동 큐 |

> Search 카테고리의 `MinPriorityQueue` (planner 의 OpenSet 으로) 와 `AStarAlgorithm` + `WeightedGraph` (Move 행동 내부 격자 길찾기로) 를 그대로 import 해 쓴다 (`using Algorithms.Search;`). GOAP 의 토대가 격자 A\* 와 *완전히 같은 자료구조* 라는 것을 import 만 봐도 알 수 있게 했다.

## 학습 노트 (Notion)

다음 자료는 모두 Notion에 정리되어 있다.

- 알고리즘 개요 / 왜 "상태 공간 위의 A\*" 인가 / 동작 흐름
- WorldState 의 구조적 동등성이 ClosedSet 정확도의 핵심인 이유
- 행동 카탈로그를 정의할 때의 함정 (전제조건 누락 / 효과 누락 → 무한 루프 또는 도달 불가)
- 휴리스틱 (미충족 사실 카운트 vs relaxed planning graph) 의 트레이드오프
- 동적 비용 (현재 위치 거리) 이 같은 카탈로그 위에서 상황별로 다른 계획을 만드는 이유
- BT vs GOAP vs HTN vs Minimax/MCTS 비교 — 각각 어떤 게임에 맞는가
- F.E.A.R. 의 backward-chaining (regressive) 방식과 본 구현의 forward 방식 차이
- 직접 만져보며 확인하기 (목표 토글, 행동 비용 조절, 거점 좌표 변경, 행동 추가)

> **GOAP 학습 노트**: [GOAP — Notion](https://www.notion.so/3515e18a57368129a014df84f372de2e)

## 코드 안 학습 주석

각 `.cs` 파일 상단에는 한국어 헤더 블록이 있다.

- `GOAPAlgorithm.cs` — 5 섹션 헤더 (개요 / 동작 흐름 / 복잡도 / 사용 자료구조 / 다른 알고리즘과의 관계). 라인 주석에 `[4-a] [4-b] [4-c] [4-e]` 처럼 헤더의 동작 흐름과 1:1 매핑되는 단계 마커.
- `GOAPGridVisualizer.cs` — 데모 시나리오, 핵심 메시지, GOAP × A\* 협업 설명, 색상표, 우측 OnGUI 패널 사용법.

## 빠른 사용법

1. 새 씬 만들기 — Project 창에서 `Assets/Scenes/BehaviorTree.unity` 를 `Ctrl+D` 로 복제 → `GOAP.unity` 로 이름 변경
2. 씬 안 GameObject 의 기존 `BehaviorTreeVisualizer` 컴포넌트 제거 → `GOAPGridVisualizer` 추가
3. `AlgorithmDemoUI` 의 Demo Component 슬롯을 그 GameObject 로 다시 연결
4. 카메라 위치 권장: 격자 가운데 위에서 내려다보기 (예: position y=14, rotation x=90°)
5. Build Settings → Scenes In Build 에 `GOAP.unity` 등록
6. Play → NPC(파랑) 가 도끼를 줍고 → 나무를 베고 → 불을 피우고 → 음식을 가져와 조리해 먹는다
7. Inspector 의 `Goal` 을 `KillEnemy` 로 바꾸고 Restart → *4 단계짜리 짧은 계획* 으로 적을 처치하는 모습으로 변함

## 색상 의미

| 대상 | 색 | 의미 |
|------|----|------|
| 셀 | 옅은 회색 | 빈 칸 |
| 셀 | 검정 | 벽 (이동 불가) |
| 셀 | 분홍 | Move 행동 중 — 격자 A\* 가 만든 경로 |
| 거점 | 노랑 | 도끼 (PickAxe 후 사라짐) |
| 거점 | 초록 | 나무 (ChopWood 후 흐려짐) |
| 거점 | 주황 | 모닥불 — 꺼짐 |
| 거점 | 빨강 | 모닥불 — 점화됨 (LightFire 후) |
| 거점 | 베이지 | 음식 (PickFood 후 사라짐) |
| 거점 | 진빨강 | 적 — 살아 있음 |
| 거점 | 회색 | 적 — 처치됨 (AttackEnemy 후) |
| NPC | 파랑 | 이동/대기 |
| NPC | 노랑 | 행동 수행 중 |

## 우측 패널 읽는 법

```
GOAP — 목표 기반 자동 계획
목표: Eat → hungry = false
NPC 위치: (4,1)

─── 계획 통계 ───
계획 성공 · 행동 10 개 · 총비용 24.0
탐색한 상태: 187  /  생성된 상태: 412

─── 현재 세상 상태 ───
  ✓  at_tree
  ✗  at_axe
  ✗  at_fire
  ...
  ✓  has_axe
  ✗  has_wood
  ✗  fire_lit
  ✓  hungry

─── 행동 큐 (▶ 진행 중) ───
   ✓  1. Move(axe)        (c=7.0)
   ✓  2. PickAxe          (c=1.0)
   ▶  3. Move(tree)       (c=6.0)   ← 지금 실행 중
      4. ChopWood         (c=2.0)
      5. Move(fire)       (c=4.0)
      ...
```

- **계획 통계** : `탐색한 상태` = closed 에 들어간 수, `생성된 상태` = open 에 push 한 수. 두 값의 차이가 *휴리스틱이 얼마나 잘 작동했는지* 를 보여준다 (큰 차이 = 휴리스틱이 가지치기를 잘 함).
- **현재 세상 상태** : 행동 1 개가 끝날 때마다 갱신. *행동 효과가 한 번에 여러 사실을 바꾸는 모습* (예: LightFire 는 `fire_lit=true` 와 `has_wood=false` 를 동시에) 이 눈에 띈다.
- **행동 큐** : 계획된 전체 시퀀스. ✓ 완료 / ▶ 현재 / 공란 = 대기. 옆의 `(c=...)` 는 그 행동의 비용 — Move 만 거리에 따라 다르다.

## 변형 실험

`GOAPGridVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- **`Goal`** — `Eat` ↔ `KillEnemy`. 같은 카탈로그·세상에서 *전혀 다른 사슬* 이 나오는지 확인. **GOAP 의 핵심 데모.**
- **거점 좌표** — Axe Spot 을 적 옆으로 옮기면 KillEnemy 계획이 더 짧아진다. 동적 비용 덕에 GOAP 가 거리 변화를 *자동으로* 반영.
- **`Wall Ratio`** — 0.2 이상이면 NPC 의 격자 A\* 가 우회 경로를 만든다. (단, 거점이 벽으로 격리되면 Move 가 실패 → "no path!" 표시.)
- **`Step Interval` / `Action Pause`** — 0.05 로 줄이면 빠른 시연, 0.5 로 늘리면 계획 진행을 천천히 따라가며 학습 가능.
- **카탈로그 변경** (`BuildCatalog()` 직접 수정) :
  - `AttackEnemy` 의 `CostStatic` 을 1 로 낮추면 Eat 목표일 때도 적을 베어 가는 사슬이 나올 수 있을지 (안 나옴 — 효과가 hungry 와 무관하므로). 비용·전제조건이 사슬 형성에 어떻게 영향을 미치는지 직접 확인.
  - 새 행동 추가 — 예: "DrinkWater" (`at_water`, `has_water` 도입). 카탈로그에 한 줄 추가하면 GOAP 가 자동으로 활용. **트리를 수정하지 않아도 새 능력이 생긴다** 는 GOAP 의 강점.

## 다른 알고리즘과의 비교

| 항목 | GOAP (이 폴더) | Behavior Tree | Minimax / MCTS |
|------|---|---|---|
| 의사결정 주체 | AI 가 사슬을 자동 계획 | 디자이너가 트리를 그림 | AI 가 게임트리를 탐색 |
| 디자이너의 일 | 행동 카탈로그 정의 | 결정 트리 작성 | 평가 함수 / 시뮬레이션 작성 |
| 새 능력 추가 | 행동 1 개 추가 → 자동 활용 | 트리 수정 필수 | 적용 외부 게임 |
| 매번 비용 | A\* over states | O(노드 수) | O(b^d) / O(iterations) |
| 적합한 상황 | 자율 NPC, F.E.A.R. 류 | NPC 일반 행동 (대부분) | 보드게임 / 대전 |
| 디버깅 | 어려움 (창발적 사슬) | 쉬움 (트리 보면 답) | 점수 추적 |
| 예측 가능성 | ⚠️ 창발적 | ✅ 높음 | Minimax ✅ / MCTS ⚠️ |
| 외부 알고리즘 | Move 행동 내부에 A\* 호출 (이 데모) | Action 안에 무엇이든 (BT × A\*) | 일반적으로 단독 |

> **이 폴더의 핵심 메시지** : Behavior Tree 가 "디자이너가 *어떻게 결정할지* 를 트리로 그려두는" 방식이라면, GOAP 는 "디자이너가 *무엇이 가능한지* 만 적어두면 AI 가 *어떻게* 는 알아서 계획" 하는 방식이다. 같은 격자 데모 두 개를 비교해서 보면, 같은 NPC 가 *어디로 갈지* 결정하는 방식이 트리 평가 (BT) 와 상태 공간 탐색 (GOAP) 으로 완전히 다름이 한눈에 들어온다.
