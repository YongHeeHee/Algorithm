# A\* — A-Star Pathfinding

가중치 그래프에서 시작점 → 목표점 최단 경로를 찾는 *목표 지향적* 알고리즘.
Dijkstra + 휴리스틱 으로 "목표 쪽으로 더 그럴듯한 정점을 먼저 탐색" 하도록 가속한 버전이다.

**Dijkstra 와의 코드 차이는 단 한 줄** — 우선순위 큐의 priority 를 `g(n)` 에서 `g(n) + h(n)` 으로 바꾸기.
이 한 줄이 탐색 패턴을 동심원에서 화살표 모양으로 바꾼다.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `AStarAlgorithm.cs` | 순수 A\* 로직. `FindPath` 한 메서드. 휴리스틱은 `Func<T, T, float>` 로 주입 |
| `AStarGridVisualizer.cs` | Unity 그리드 시각화 (가중치 = 셀 높이, Manhattan 휴리스틱) |

> 공유 의존성 (한 단계 위 `Assets/Algorithms/Search/`):
> - `WeightedGraph.cs` — Dijkstra 와 공유
> - `MinPriorityQueue.cs` — Dijkstra 와 공유 (priority 기준만 다르게 사용)

## 학습 노트 (Notion)

다음 자료는 모두 Notion에 정리되어 있다.

- 알고리즘 개요 / 동작 흐름 / 시간·공간 복잡도 / 게임 사용처
- BFS vs Dijkstra vs A\* 가족 관계표 (PQ priority 기준의 세 가지 변형)
- 휴리스틱의 두 가지 성질 (admissibility, consistency) 와 그 의미
- 5가지 다이어그램
  1. 클래스 / 모듈 의존 구조 (`WeightedGraph`, `MinPriorityQueue` 공유 표시)
  2. `FindPath` 흐름도 (g, h, f 계산 위치)
  3. 우선순위 큐 상태 변화 (priority = f 가 만들어내는 차이)
  4. 그리드 위 화살표 모양 탐색 (Dijkstra 동심원과의 시각적 대비)
  5. 런타임 데이터 흐름
- Manhattan vs Euclidean vs Octile 휴리스틱 비교
- "한 줄 차이" 의 시각적 증거 — Dijkstra 코드와 옆에 두고 비교

> **A\* 학습 노트**: [A\* — Notion](https://www.notion.so/34f5e18a573681b08b79cb481fd52238)

## 코드 안 학습 주석

각 `.cs` 파일 상단에 한국어 헤더 블록이 있다.

- `AStarAlgorithm.cs` — 6섹션 (개요 / 휴리스틱 성질 / 동작 흐름 / 복잡도 / 게임 사용처 / 자료구조). Dijkstra 와의 한 줄 차이를 명시적으로 강조.
- `AStarGridVisualizer.cs` — 사용법, 시각적 차이 (동심원 → 화살표), Manhattan 휴리스틱이 admissible 한 이유.

라인별 주석에는 Dijkstra Visualizer 와 같은 `[1] 결과 컨테이너` `[6-1] f 가 가장 작은 정점 꺼내기` 식 단계 번호가 달려 있어, 두 파일을 옆에 두고 비교하면 *어디만 다른지* 한눈에 보인다.

## 빠른 사용법

1. `AStar.unity` 열기 (없으면 `Dijkstra.unity` 또는 `BFS.unity` 를 Duplicate → 컴포넌트 교체)
2. 빈 GameObject 생성 → 이름 `AStarDemo`
3. `AStarGridVisualizer` 컴포넌트 부착
4. (선택) Cell Prefab 슬롯을 비워 두면 Cube 가 자동 생성됨
5. (권장) 카메라를 비스듬한 내려각으로 — 가중치 높이가 잘 보이게
6. (선택) Restart 버튼 셋업: Canvas + Button + `AlgorithmDemoUI` 컴포넌트 (`Assets/Algorithms/Common/`)
7. Play → 노란 Frontier 가 *목표 방향으로 길게* 뻗어나가며 분홍 최단 경로가 그려짐

## 색상 의미

(BFS / DFS / Dijkstra 와 동일)

| 색 | 의미 |
|----|------|
| 회색 | 미방문 |
| 노랑 | PQ 에 들어감 또는 g 가 갱신됨 (Frontier) |
| 파랑 | settled — 최단 거리 확정 |
| 초록 | 시작 정점 |
| 빨강 | 목표 정점 |
| 분홍 | 최단 경로 위의 칸 (가중치 합 기준) |
| 검정 | 벽 (이동 불가, 짧고 검은 trench) |

> **셀의 높이** = 그 셀의 이동 비용. Dijkstra 와 동일.

## 변형 실험

`AStarGridVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- `Width` / `Height` — 그리드 크기
- `Wall Ratio` — 벽 비율
- `Min Weight` / `Max Weight` — 가중치 범위. 둘 다 1로 두면 BFS 와 같은 분홍 경로
- `Random Seed` — 같은 시드 = 같은 미로 + 같은 가중치. **BFS / DFS / Dijkstra 와 같은 시드로 맞추면 네 알고리즘의 동작을 직접 비교 가능**
- `Step Delay` — 0.05 ~ 0.1 추천. 화살표 모양 탐색이 잘 보임

### 핵심 비교 실험

**Dijkstra 데모와 같은 randomSeed 로 둘을 함께 돌려 보자**:

| 관찰 항목 | Dijkstra | A\* |
|---------|---------|-----|
| 분홍 경로 | 가중치 합 최단 | 같음 (admissible 휴리스틱이라 보장) |
| 노란/파란 셀 수 | 많음 (모든 방향 탐색) | **훨씬 적음** (목표 방향으로 집중) |
| 탐색 모양 | 동심원 (cost 기반) | 화살표 (목표 지향) |

이 차이가 "휴리스틱이 주는 가속 효과" 의 시각적 증거. 같은 답을 더 적은 탐색으로 찾아낸다는 게 A\* 의 가치.

`AStarAlgorithm.FindPath` 를 호출할 때 `heuristic` 인자로 `(a, b) => 0f` 를 넘기면 **A\* 가 그대로 Dijkstra 가 된다** — 이게 "h(n)=0 이면 Dijkstra" 라는 사실의 직접 증명. 두 알고리즘이 한 가족이라는 결정적 증거.
