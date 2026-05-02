# Utility AI — 점수 함수 기반 의사결정 (Score-based Decision Making)

디자이너는 *각 행동의 점수 함수* 만 정의한다. 매 tick 모든 행동의 점수를 계산해 *가장 높은* 점수의 행동을 선택한다 — 그게 전부. 점수는 보통 *고려사항(Consideration)* 들의 곱이며, 각 Consideration 은 정규화된 입력을 *Response Curve* 에 통과시켜 0~1 점수를 만든다. The Sims, RimWorld, Skyrim Radiant AI 같은 *욕구 / 상황 가중 NPC* 류의 사실상 표준.

이 폴더는 격자 위 **욕구 기반 NPC** 데모 — 시간이 지나며 변하는 4 개 욕구(`hunger`, `energy`, `social`, `safety`)와 주기적으로 다가오는 적 사이에서, NPC 가 *매 tick 처음부터 다시 결정* 해 행동을 전환하는 모습을 시각화한다.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `UtilityAIAlgorithm.cs` | 순수 UAI 프레임워크. `Consideration` (입력 + ResponseCurve), `UtilityAction` (Considerations 곱 + Weight + CompensationFactor), `UtilityAIBrain` (매 tick 평가 + Hysteresis 인터럽트 임계값). Unity 의존 0. |
| `UtilityAIGridVisualizer.cs` | Unity MonoBehaviour. 격자 + Capsule NPC + 4 거점(Food/Bed/Social/Haven) + Cube 적. 매 프레임 욕구 변화, 매 tick 결정, 한 칸씩 이동 사이에 *다시* 결정 → 환경 변화 즉시 반응. Move 단계는 `AStarAlgorithm.FindPath` 호출 → UAI × A\* 협업. 우측 OnGUI 패널에 욕구 바 + 행동 점수 막대 + 펼치면 Consideration 분해. |

> Search 카테고리의 `AStarAlgorithm` + `WeightedGraph` 를 그대로 import 해 쓴다 (`using Algorithms.Search;`). UAI 자체는 길찾기를 모르며, *행동 *내부* 의 이동* 만 A\* 에게 위임한다 — BT × A\*, GOAP × A\* 와 같은 협업 패턴.

## 학습 노트 (Notion)

다음 자료는 모두 Notion에 정리되어 있다.

- 알고리즘 개요 / 왜 "곱셈" 인가 / 동작 흐름
- Consideration 과 Response Curve 의 직관 (Linear / Quadratic / Logistic / Inverse 곡선이 각각 어떤 상황을 표현하나)
- Compensation Factor 가 필요한 이유 (Consideration 이 많을수록 곱이 0 으로 빨리 수렴)
- Hysteresis (인터럽트 임계값) 가 flip-flop 을 어떻게 막는가
- BT vs GOAP vs UAI vs FSM 비교 — 각각 어떤 게임에 맞는가
- IAUS (Infinite Axis Utility System) 의 표준 설계와 본 구현의 차이
- 직접 만져보며 확인하기 (욕구 변화 속도, Response Curve 변경, Weight 조절, 적 위협 반경)

> **Utility AI 학습 노트**: [Utility AI — Notion](https://www.notion.so/3545e18a573681679f4bc7f67cdec3a1)

## 코드 안 학습 주석

각 `.cs` 파일 상단에는 한국어 헤더 블록이 있다.

- `UtilityAIAlgorithm.cs` — 5 섹션 헤더 (개요 / 동작 흐름 / 복잡도 / 사용 자료구조 / 다른 알고리즘과의 관계). 라인 주석에 `[1-a] [1-b] [1-c] [1-d] [1-e] [1-f] [2] [3]` 처럼 헤더의 동작 흐름과 1:1 매핑되는 단계 마커.
- `UtilityAIGridVisualizer.cs` — 데모 시나리오, 핵심 메시지, UAI × A\* 협업 설명, 색상표, 우측 OnGUI 패널 사용법.

## 빠른 사용법

1. 새 씬 만들기 — Project 창에서 `Assets/Scenes/GOAP.unity` 를 `Ctrl+D` 로 복제 → `UtilityAI.unity` 로 이름 변경
2. 씬 안 GameObject 의 기존 `GOAPGridVisualizer` 컴포넌트 제거 → `UtilityAIGridVisualizer` 추가
3. `AlgorithmDemoUI` 의 Demo Component 슬롯을 그 GameObject 로 다시 연결
4. 카메라 위치 권장: 격자 가운데 위에서 내려다보기 (예: position y=14, rotation x=90°)
5. Build Settings → Scenes In Build 에 `UtilityAI.unity` 등록
6. Play → NPC(파랑) 가 욕구에 따라 Food/Bed/Social 거점을 오가며 욕구를 채운다
7. 적(빨강)이 NPC 에 가까워지면 → safety 점수 폭락 → *진행 중인 행동을 인터럽트* 하고 Haven(흰색) 으로 도망 가는 모습이 핵심 데모

## 색상 의미

| 대상 | 색 | 의미 |
|------|----|------|
| 셀 | 옅은 회색 | 빈 칸 |
| 셀 | 검정 | 벽 (이동 불가) |
| 셀 | 분홍 | Move 한 칸 직전의 격자 A\* 경로 |
| 거점 | 주황 | Food — Eat 행동의 목표 |
| 거점 | 파랑 | Bed — Sleep 행동의 목표 |
| 거점 | 초록 | Social — Socialize 행동의 목표 |
| 거점 | 흰색 | Haven (안전지대) — FleeToSafety 행동의 목표 |
| 적 | 빨강 | 주기적으로 NPC 쪽으로 다가옴 (NPC 가 Haven 에 있으면 후퇴) |
| NPC | 파랑 | 이동 / 대기 |
| NPC | 노랑 | 거점에서 욕구 충족 행동 수행 중 |

## 우측 패널 읽는 법

```
Utility AI — 점수 함수 의사결정
현재 행동: Eat   ← interrupted!
NPC (4,5)  /  Enemy (3,4)

─── 현재 욕구 ───
hunger ↑   [████████████░░░] 78
energy ↓   [██████████░░░░░] 64
social ↓   [█████░░░░░░░░░░] 32
safety ↓   [██░░░░░░░░░░░░░] 12

─── 행동 점수 (클릭하면 펼침) ───
   Eat            [██████░░░░░░░░░] 0.42
▶  FleeToSafety   [█████████░░░░░░] 0.88
   Sleep          [████░░░░░░░░░░░] 0.31
   Socialize      [███░░░░░░░░░░░░] 0.22
   Wander         [█░░░░░░░░░░░░░░] 0.15
      · 낮은 safety   raw   12.0  →  norm 0.12  →  0.77
      · 거리(haven)   raw    8.0  →  norm 0.30  →  0.70
      곱=0.54  보정=0.62  ×Weight 1.4  =  0.88
```

- **현재 행동** — 이번 tick 채택된 행동. `← interrupted!` 가 보이면 직전 행동이 다른 것이었음을 표시 (인터럽트 발생).
- **욕구 바** — `hunger ↑` 는 *클수록 결핍* (오를수록 빨강), `energy ↓` 등은 *작을수록 결핍* (떨어지면 빨강). NPC 의 *지금 상태* 를 한 줄에.
- **행동 점수** — 모든 행동의 최종 점수가 막대로. `▶` 가 선택된 행동. 막대 위치만 보면 *다음 tick 에 무엇이 채택될 가능성이 높은지* 가 한눈에.
- **펼친 행동** — 행동 이름을 클릭하면 그 행동의 *Consideration 별 분해* 가 보인다. raw → normalized → curve 출력의 흐름 + 곱 → 보정 → ×Weight 의 단계가 모두 노출 → "왜 이 점수인지" 가 100 % 보임.

## 변형 실험

`UtilityAIGridVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- **`Hunger Rate` / `Energy Rate` / `Social Rate`** — 욕구 변화 속도. 한 욕구만 빠르게 올리면 그 행동이 *지배적으로* 선택되는지 확인. 모두 똑같이 빠르게 올리면 *flip-flop* 이 일어나는지 (그래서 `Interrupt Threshold` 가 왜 필요한지) 직접 체감.
- **`Interrupt Threshold`** — 0 으로 만들어 보면 매 tick 점수가 살짝 더 높은 행동으로 바로 전환되는 *flip-flop* 을 볼 수 있다. 0.05 ~ 0.1 이 적절한 범위.
- **`Enemy Step Interval` / `Enemy Danger Range`** — 위협 빈도와 반경. 반경을 5 로 늘리면 NPC 가 *훨씬 빨리* 도망친다 (safety 가 빨리 떨어지므로).
- **Response Curve 변경** (`BuildBrain()` 안 직접 수정) :
  - Eat 의 hunger Consideration 을 `Linear` → `Logistic` 으로 바꾸면 NPC 가 hunger 50 정도까지는 *전혀* 먹지 않다가 임계 통과 시 갑자기 먹기 시작하는 모습. **임계 통과 행동** 의 직관.
  - FleeToSafety 의 safety Consideration 을 `InverseQuadratic` → `Inverse` 로 바꾸면 도망 결정이 *둔해진다* — 안전이 80 정도일 때도 약간씩 도망 점수가 있다.
- **`Weight` 조절** (`UtilityAction` 의 `Weight = 1.4f` 등) :
  - Sleep 의 Weight 를 2.0 으로 올리면 energy 가 조금만 떨어져도 자러 가는 *우선순위 NPC* 가 된다.
- **새 행동 추가** (`BuildBrain()` 에 한 덩어리 추가) :
  - 예: "Drink" — 별도 욕구 `thirst` 와 음수대 거점 추가. *기존 행동들을 건드리지 않아도* 새 욕구가 생기는 즉시 NPC 가 알아서 활용. **Utility AI 의 강점**.

## 다른 알고리즘과의 비교

| 항목 | Utility AI (이 폴더) | Behavior Tree | GOAP | Minimax / MCTS |
|------|---|---|---|---|
| 의사결정 단위 | 점수 비교 (매 tick) | 트리 평가 (단락) | 상태 공간 A\* (사슬 계획) | 게임 트리 탐색 |
| 디자이너의 일 | Consideration / Response Curve | 결정 트리 | 행동 카탈로그 | 평가 함수 / 시뮬레이션 |
| 환경 변화 반응 | ✅ 매 tick 즉각 | 트리에 분기 추가 | ⚠️ 사슬 중엔 둔감 (replan 필요) | 다음 탐색에서 반영 |
| 멀리 보기 | ❌ (다음 1 행동만) | 디자이너가 그린 만큼 | ✅ 사슬을 계획 | ✅ 깊이만큼 |
| flip-flop 위험 | ⚠️ Hysteresis 필요 | ❌ | ❌ | ❌ |
| 새 행동 추가 | 행동 1 개 추가 → 자동 활용 | 트리 수정 필수 | 행동 추가 → 자동 활용 | 적용 외부 게임 |
| 매번 비용 | O(A·C) — 매우 가벼움 | O(노드 수) | A\* over states | O(b^d) / iterations |
| 적합한 상황 | 욕구 / 상황 가중 NPC | 상태 머신 류 NPC | 자율 NPC, F.E.A.R. 류 | 보드게임 / 대전 |
| 디버깅 | ✅ 점수 표 그대로 보기 | ✅ 트리 보면 답 | ⚠️ 창발적 사슬 | 점수 추적 |
| 외부 알고리즘 | Move 행동 안에 A\* | Action 안에 무엇이든 | Move 행동 안에 A\* | 일반적으로 단독 |

> **이 폴더의 핵심 메시지** : Behavior Tree 가 "디자이너가 *어떻게 결정할지* 를 트리로 그려두고", GOAP 가 "디자이너가 *무엇이 가능한지* 만 적어두면 AI 가 *사슬을 자동 계획*" 한다면, Utility AI 는 "디자이너가 *각 행동의 점수 함수* 만 정의하면 AI 가 매 tick *그 자리에서 비교* 해 결정" 한다. 같은 격자·같은 NPC 인데 BT / GOAP / UAI 세 방식의 *결정 메커니즘이 어떻게 다른지* 한눈에 들어오도록 같은 데모 골격(격자 + Capsule NPC + A\* 협업)을 공유했다.
