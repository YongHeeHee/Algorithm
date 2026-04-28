# MCTS — Monte Carlo Tree Search

게임 트리를 *완전 탐색* 하는 Minimax 와 달리, **통계적으로 샘플링** 해서 최선의 수를 찾는다.
4 단계 (**Selection → Expansion → Simulation → Backpropagation**) 를 시간 한도 내에서 반복.
평가 함수가 필요 없다는 점 — *random rollout 의 진짜 승/패만으로 의사결정* — 이 핵심.
바둑처럼 분기가 폭발하는 게임에서 Minimax 가 무력화될 때 등장하는 알고리즘이며, AlphaGo 의 뼈대.

이 폴더는 **틱택토(3×3)** 를 데모 게임으로 사용한다 — 같은 게임을 Minimax/MCTS 두 알고리즘으로 풀어 *수렴 속도와 행동 양상* 을 직접 비교하기 위함.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `MCTSAlgorithm.cs` | 순수 MCTS 로직. `Searcher` 클래스 (chunk 단위 점진 탐색 지원) + 정적 `FindBestMove` (Minimax 와 같은 시그니처) |
| `MCTSVisualizer.cs` | Unity MonoBehaviour. iteration 을 chunk 단위로 돌리며 셀에 visit 비율을 노란색 강도로 표시 |

> 게임 국면 표현은 sibling 폴더 `AI/TicTacToe/TicTacToeBoard.cs` 를 그대로 공유한다 (같은 namespace `Algorithms.AI`). Minimax 와 MCTS 가 *완전히 같은 보드* 위에서 다른 방식으로 사고하는 것이 핵심 학습 포인트.

## 학습 노트 (Notion)

다음 자료는 모두 Notion에 정리되어 있다.

- 알고리즘 개요 / 4 단계 흐름도 / 시간·공간 복잡도 / 게임 사용처
- Minimax vs MCTS 비교표 (완전 탐색 vs 샘플링, 평가 함수 필요 vs 불필요)
- UCB1 공식 해설 (활용 vs 탐험의 균형)
- 다이어그램
  1. 한 iteration 의 4 단계 시각화
  2. 트리가 *비대칭으로* 자라는 모습 (유망한 가지는 깊게, 비유망한 가지는 얕게)
  3. iteration 수 증가에 따른 답 수렴 그래프
- 직접 만져보며 확인하기 (iterations 100→5000, AI vs AI 무승부, Minimax 와 같은 답에 다른 길)

> **MCTS 학습 노트**: [MCTS — Notion](https://www.notion.so/3505e18a5736816cb85afb7f0b523ed9)

## 코드 안 학습 주석

각 `.cs` 파일 상단에는 한국어 헤더 블록이 있다.

- `MCTSAlgorithm.cs` — 6 섹션 헤더 + 4 단계 흐름을 본문 코드의 `[1] Selection`, `[2] Expansion`, `[3] Simulation`, `[4] Backpropagation` 마커와 1:1 매핑. UCB1 공식은 별도 메서드로 분리해 가독성 우선.
- `MCTSVisualizer.cs` — Minimax 시각화와의 차이 ("후보 차례차례 깜빡" 이 아닌 "전체 진행도") 를 헤더에 명시.

## 빠른 사용법

1. 새 씬 만들기 — Unity 의 Project 창에서 `Assets/Scenes/Minimax.unity` 를 `Ctrl+D` 로 복제 → `MCTS.unity` 로 이름 변경
2. 씬 안의 GameObject 에서 `TicTacToeVisualizer` 컴포넌트 제거 → `MCTSVisualizer` 컴포넌트 추가
3. (선택) `AlgorithmDemoUI` 의 Demo Component 슬롯을 새 GameObject 로 다시 연결
4. Build Settings → Scenes In Build 에 `MCTS.unity` 등록
5. Play → 빈 칸을 좌클릭 → X(파랑) → AI 가 셀들을 *동시에* 노랗게 익히다가 한 칸이 진해지면 → O(빨강)

## 색상 의미

| 색 | 의미 |
|----|------|
| 옅은 회색 | 빈 칸 |
| 파랑 | X (사람) |
| 빨강 | O (AI, MCTS) |
| 노랑 (강도 가변) | MCTS 가 *지금까지* 그 칸을 살펴본 비율 (visits / max visits) |
| 초록 깜빡 | 게임 종료 시 승리 라인 |

> Minimax 시각화의 "후보 차례차례 깜빡" 과 비교해 보면 두 알고리즘의 사고 방식 차이가 시각적으로 분명하다. Minimax 는 결정적·완전 평가, MCTS 는 통계적·점진적.

## 변형 실험

`MCTSVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- **`Iterations`** — 100 → 1000 → 5000 으로 올려보며 *답이 수렴하는지* 확인. 적은 iteration 에서는 후보 1·2 위가 자주 뒤바뀐다. 많이 돌릴수록 한 칸이 *압도적으로* 진해진다.
- **`Random Seed`** — 0 이 아닌 값으로 고정하면 같은 답이 재현된다. 같은 시드 + 같은 iteration → 같은 답.
- **`Visualization Steps`** — chunk 분할 수. 50 으로 올리면 부드러운 애니메이션, 1 로 두면 즉시 결과만 (애니메이션 없음).
- **`Ai Vs Ai`** — 양쪽이 모두 MCTS → 보통 무승부. (Minimax 의 AI vs AI 와 같은 정리지만 *길이 다름*.)
- **`Iterations` 를 10 정도로 줄여보기** — MCTS 가 멍청한 수를 두는 모습이 보임. *왜* 멍청한지: 통계가 충분히 안 쌓여 무작위 시뮬레이션의 운에 휘둘림.

## Minimax / MCTS 한눈에 비교

| 항목 | Minimax + α-β | MCTS |
|------|---|---|
| 트리 생성 | 깊이 우선으로 *전부* 펼침 | 유망한 가지만 *점진적* 으로 자람 |
| 평가 방법 | 평가 함수 (도메인 지식 필요) | random rollout 의 진짜 승/패 |
| 가지치기 | Alpha-Beta 로 명시적 가지치기 | UCB1 이 자연스럽게 가지치기 |
| 정답 보장 | ✅ (트리 다 보면 정확) | ❌ (통계적 근사) |
| 분기 폭발에 강한가 | ❌ (체스 한계) | ✅ (바둑 가능) |
| 시간 비례 효과 | 깊이 +1 단계적 | 시간 늘수록 *부드럽게* 좋아짐 |
| 결정성 | 결정적 (같은 입력 → 같은 답) | 비결정적 (시드에 따라 답 다름) |
| 종료 조건 | 트리 다 봐야 끝 | *언제든 멈춰도* OK (anytime) |

> 같은 입력 (게임 상태) 에 같은 출력 (다음 수) 을 내지만 **서로 보완하는 두 도구**. 이 프로젝트의 Minimax 와 MCTS 폴더를 나란히 두고 보면 그 보완 관계가 코드로 드러난다.
