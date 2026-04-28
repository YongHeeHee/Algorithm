# Minimax + Alpha-Beta — 게임 트리 탐색

2 인 제로섬 완전정보 게임 (체스, 체커, 오목, 틱택토 …) 에서 "최선의 수" 를 찾는 알고리즘.
본질은 **DFS 의 게임 트리 응용판** — 노드 = 국면, 간선 = 한 수, 잎 = 게임 종료.
Alpha-Beta 가지치기로 *결과에 영향 없이* 의미 없는 가지를 잘라 O(b^d) → 최선 O(b^(d/2)) 로 가속한다.

이 폴더는 **틱택토(3×3)** 를 데모 게임으로 사용한다 — 분기 ≤ 9, 깊이 ≤ 9 라 완전 탐색이 가능하고 학습 효과가 가장 분명하기 때문.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `TicTacToeBoard.cs` | 게임 국면 표현 (Make/Undo 패턴, 합법수 열거, 결과 판정). *게임 고유 부분* — 다른 게임으로 바꾸려면 이 파일만 갈아끼우면 됨 |
| `MinimaxAlgorithm.cs` | 순수 Minimax + Alpha-Beta 로직. `FindBestMove` (루트 진입점), `Search` (재귀 본체), `Evaluate` (잎 점수). *게임에 무관한 부분* |
| `TicTacToeVisualizer.cs` | Unity MonoBehaviour. 마우스 클릭 입력 + AI 차례 시각화 + 승리 라인 강조 |

> Minimax 는 그래프(`Graph<T>`) 도 격자(`T[,]`) 도 쓰지 않는다 — 게임 트리는 *수를 펼쳐가며 즉석에서 생성* 된다. 그래서 `Search/` 가 아닌 새 카테고리 `AI/` 에 둔다.

## 학습 노트 (Notion)

다음 자료는 모두 Notion에 정리되어 있다.

- 알고리즘 개요 / 동작 흐름 / 시간·공간 복잡도 / 게임 사용처
- BFS / DFS / Minimax 비교표 (모두 DFS 골격이지만 *반환값* 과 *노드 의미* 가 다른 점)
- 다이어그램
  1. 게임 트리에서 MAX / MIN 이 번갈아 나오는 모습
  2. Alpha-Beta 의 가지치기가 일어나는 순간 (β-cutoff / α-cutoff)
  3. 클래스 / 모듈 의존 구조 (Board ↔ Algorithm ↔ Visualizer 분리)
  4. depth 보정이 만들어내는 "빠른 승 / 늦은 패" 선호 효과
- 직접 만져보며 확인하기 (Alpha-Beta on/off, AI vs AI = 항상 무승부, 사람이 X 로 시작 = 못 이김)

> **Minimax 학습 노트**: [Minimax — Notion](https://www.notion.so/3505e18a5736816b935eec489638c4cd)

## 코드 안 학습 주석

각 `.cs` 파일 상단에는 한국어 헤더 블록이 있다.

- `TicTacToeBoard.cs` — Make/Undo 패턴을 왜 쓰는지 (Clone vs in-place 의 트레이드오프).
- `MinimaxAlgorithm.cs` — 6 섹션 헤더 (개요 / 동작 흐름 / Alpha-Beta / 복잡도 / 게임 사용처 / 자료구조 선택). MAX/MIN 의 대칭성, β-cutoff 와 α-cutoff 의 의미를 명시.
- `TicTacToeVisualizer.cs` — 사용법, 색상 의미, 핵심 학습 포인트 (Alpha-Beta on/off 비교, AI vs AI 무승부 정리).

라인별 주석에는 `[1] 종료 검사` `[2-d] β-cutoff` 처럼 단계 번호가 달려 있어, 헤더의 "동작 흐름" 단계와 1:1 로 매핑된다.

## 빠른 사용법

1. `Assets/Scenes/Minimax.unity` 열기 (없으면 `BFS.unity` 를 Duplicate → 이름 변경 후 사용)
2. 빈 GameObject 생성 → 이름 `TicTacToe`
3. `TicTacToeVisualizer` 컴포넌트 부착
4. (선택) Cell Prefab 슬롯을 비워 두면 Cube 가 자동 생성됨
5. (선택) Restart 버튼 셋업: Canvas + Button + `AlgorithmDemoUI` 컴포넌트 (`Assets/Algorithms/Common/`)
6. 카메라는 위에서 내려다보는 각도가 좋다 (예: position y=8, rotation x=90°)
7. Play → 빈 칸을 좌클릭하면 X(파랑) 가 놓이고, AI 가 잠시 노란 깜빡임으로 후보를 평가한 뒤 O(빨강) 를 둔다

## 색상 의미

| 색 | 의미 |
|----|------|
| 옅은 회색 | 빈 칸 |
| 파랑 | X (사람) |
| 빨강 | O (AI) |
| 노랑 깜빡 | AI 가 *지금 평가 중인* 후보 칸 (Minimax thinking) |
| 초록 깜빡 | 게임 종료 시 승리 라인 |

## 변형 실험

`TicTacToeVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- **`Use Alpha Beta`** — 끄고 켜며 콘솔의 `Nodes=` 값 비교. 빈 보드 첫 수에서:
  - OFF (순수 Minimax): 약 549,946 노드
  - ON  (Alpha-Beta, 정렬 안 함): 보통 30,000~60,000 노드
  → *결과는 같지만* 노드 수가 한 자릿수 단위로 줄어드는 것을 직접 확인할 수 있음.
- **`Ai Plays First`** — 켜면 AI 가 X 로 먼저 시작. 첫 수가 가운데(1,1)로 가는지(가장 강한 수) 관찰.
- **`Ai Vs Ai`** — 양쪽이 모두 Minimax → **항상 무승부**. 틱택토의 유명한 정리("최선 vs 최선 = 무승부") 시각적 확인.
- **`Think Step Delay`** — 0 으로 두면 AI 가 즉시 둠. 0.2~0.4 로 올리면 후보 평가가 잘 보임.

## BFS / DFS / Minimax 한눈에 비교

| 항목 | BFS / DFS | Minimax |
|------|-----------|---------|
| 본질 | 그래프 위 탐색 | 게임 트리 위 탐색 |
| 노드 의미 | 정점(좌표) | 게임 국면(board state) |
| 간선 의미 | 인접 / 연결 | 한 수(move) |
| 입력 | 미리 만들어진 `Graph<T>` | 보드와 합법수 함수 (트리는 *즉석 생성*) |
| 반환값 | 방문 순서 / 경로 | **점수**, 그리고 그 점수를 만든 *수* |
| 자료구조 | Queue (BFS) / Stack (DFS) | 호출 스택 (재귀 DFS) |
| 가지치기 | 없음 | **Alpha-Beta** — DFS 변형의 핵심 가속 |

> Minimax 는 DFS 본체에 **MAX/MIN 교대** 와 **평가 함수** 만 추가한 형태다. 이 폴더의 코드를 BFS/DFS 폴더의 코드와 나란히 두고 비교하면 그 사실이 한눈에 드러난다.
