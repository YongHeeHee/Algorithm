# Dijkstra — Shortest Path (다익스트라 최단 경로)

가중치가 있는 그래프에서 시작 정점에서 *모든* 정점까지의 최단 비용을 찾는다.
음수 간선이 없는 경우에 한해 동작 (음수 간선은 Bellman-Ford / SPFA).

**BFS = 모든 가중치가 1인 Dijkstra의 특수 케이스.**
가중치가 들쭉날쭉하면 BFS 가 찾은 경로와 Dijkstra 가 찾은 경로가 달라진다 — 후자가 진짜 최단이다.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `DijkstraAlgorithm.cs` | 순수 Dijkstra 로직. `Search` (모든 정점 거리), `FindPath` (목표까지 최단 경로) |
| `DijkstraGridVisualizer.cs` | Unity 그리드 시각화 (가중치 = 셀 높이) |

> 공유 의존성 (한 단계 위 `Assets/Algorithms/Search/`):
> - `WeightedGraph.cs` — 가중치 인접 리스트 그래프 (Dijkstra / A* 공유)
> - `MinPriorityQueue.cs` — 이진 힙 기반 최소 우선순위 큐 (Unity 의 .NET Standard 2.1 환경에 BCL `PriorityQueue` 가 없어 직접 구현)

## 학습 노트 (Notion)

다음 자료는 모두 Notion에 정리되어 있다.

- 알고리즘 개요 / 동작 흐름 / 시간·공간 복잡도 / 게임 사용처
- BFS vs Dijkstra 한눈에 비교표 (자료구조 / 최단 기준 / 탐색 모양 / 복잡도)
- 5가지 다이어그램
  1. 클래스 / 모듈 의존 구조 (`WeightedGraph`, `MinPriorityQueue` 공유 표시)
  2. `Search` 메서드 흐름도 (PQ + relaxation)
  3. 우선순위 큐 상태 변화 추적 (이진 힙)
  4. 그리드 위 비용 누적 진행 (BFS 동심원과의 시각적 대비)
  5. 런타임 데이터 흐름
- "BFS 가 잘못된 답을 주는 케이스" 예제 (절벽 경로 vs 평지 우회)
- `FindPath` 의 "settled 시점이 곧 최단" 원칙 (자주 실수하는 포인트)

> **Dijkstra 학습 노트**: [Dijkstra — Notion](https://www.notion.so/34f5e18a5736815898f9ccc8f3e977cb)

## 코드 안 학습 주석

각 `.cs` 파일 상단에는 한국어 헤더 블록이 있다.

- `DijkstraAlgorithm.cs` — 5섹션 (개요 / 동작 흐름 / 복잡도 / 게임 사용처 / 자료구조 선택). 매 섹션마다 BFS 와의 차이를 명시.
- `DijkstraGridVisualizer.cs` — 사용법, 가중치 시각화 (높이) 의미, BFS / DFS 시각화와의 비교법.
- `WeightedGraph.cs` — `Graph<T>` 와의 차이, ValueTuple `(T, float)` 을 쓰는 이유.
- `MinPriorityQueue.cs` — 이진 힙 동작 원리 (Sift Up / Sift Down), lazy deletion 패턴.

라인별 주석에는 `[1] 결과 컨테이너` `[6-1] 가장 짧은 정점 꺼내기` 처럼 단계 번호가 달려 있어,
헤더의 "동작 흐름" 단계와 1:1 로 매핑된다. BFS / DFS 의 같은 번호 단계와 옆에 두고 보면 세 알고리즘이 같은 골격에서 *어디만 다른지* 한눈에 보인다.

## 빠른 사용법

1. `Dijkstra.unity` 열기 (없으면 `BFS.unity` 를 Duplicate → 컴포넌트 교체)
2. 빈 GameObject 생성 → 이름 `DijkstraDemo`
3. `DijkstraGridVisualizer` 컴포넌트 부착
4. (선택) Cell Prefab 슬롯을 비워 두면 Cube 가 자동 생성됨
5. (권장) 카메라를 비스듬한 내려각으로 배치 — 가중치 높이가 잘 보이게
6. (선택) Restart 버튼 셋업: Canvas + Button + `AlgorithmDemoUI` 컴포넌트 (`Assets/Algorithms/Common/`)
7. Play → 가중치만큼 솟은 셀 → 노란 Frontier → 파란 Settled → 분홍 최단 경로 (가중치 합 기준)

## 색상 의미

(BFS / DFS 와 동일하지만 의미가 약간 다르다)

| 색 | 의미 |
|----|------|
| 회색 | 미방문 |
| 노랑 | PQ 에 들어가거나 거리가 갱신됨 (Frontier) |
| 파랑 | settled — 최단 거리 *확정* |
| 초록 | 시작 정점 |
| 빨강 | 목표 정점 |
| 분홍 | 최단 경로 위의 칸 (가중치 합 기준) |
| 검정 | 벽 (이동 불가, 짧고 검은 trench) |

> **셀의 높이** = 그 셀의 이동 비용. 높을수록 비싸다. 시각적으로 "산맥" 처럼 보이며, Dijkstra 가 산을 우회하는 모습이 핵심 관찰 포인트.

## 변형 실험

`DijkstraGridVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- `Width` / `Height` — 그리드 크기
- `Wall Ratio` — 벽 비율 (낮게 두어야 가중치 우회 효과가 잘 보임. 0.15 이하 추천)
- `Min Weight` / `Max Weight` — 가중치 범위. **둘 다 1 로 두면 Dijkstra = BFS** (직접 검증 가능)
- `Random Seed` — 같은 시드 = 같은 미로 + 같은 가중치 패턴. **BFS / DFS 데모와 같은 시드로 맞추면 세 알고리즘의 분홍 경로를 직접 비교 가능**
- `Step Delay` — 0.1 초 이상이면 PQ 가 거리 작은 셀부터 꺼내는 모습이 잘 보임

### 핵심 비교 실험

같은 시드로 BFS / DFS / Dijkstra 세 데모를 돌려 분홍 경로를 비교해 보자:

- **BFS** : 간선 수 기준 최단 — 지형 비용을 무시하고 비싼 칸 위로 그냥 직진
- **DFS** : 그냥 어떤 경로 — 우회해서 길게 나올 때가 많음 (최단 X)
- **Dijkstra** : 가중치 합 기준 최단 — 비싼 칸을 *피해* 돌아가는 모습이 관찰됨

이 세 경로의 차이가 곧 *세 알고리즘의 철학 차이*. `MinWeight = MaxWeight = 1` 로 두면 Dijkstra 의 결과가 BFS 와 같아진다 — 가중치가 모두 같으면 두 알고리즘이 동치라는 사실의 직접 증명이다.

`DijkstraAlgorithm.Search` 의 `MinPriorityQueue` 를 `Queue` 로 바꾸면 (Push 시 priority 무시) BFS 가 된다. 자료구조 한 줄 차이로 알고리즘이 바뀌는 가족 관계: BFS / DFS / Dijkstra.
