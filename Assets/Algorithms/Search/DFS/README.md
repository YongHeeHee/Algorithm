# DFS — Depth-First Search

깊이 우선 탐색. 시작 정점에서 한 방향으로 갈 수 있는 데까지 깊게 파고든 뒤,
막히면 마지막 분기 지점으로 되돌아와(백트래킹) 다른 가지를 탐색한다.
**최단 경로는 보장하지 않는다** — BFS 와의 가장 큰 차이.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `DFSAlgorithm.cs` | 순수 DFS 로직. `Search` (반복, 권장), `SearchRecursive` (재귀, 학습용), `FindPath` (어떤 경로 복원) |
| `DFSGridVisualizer.cs` | Unity 그리드 위에서 한 스텝씩 시각화하는 MonoBehaviour |

> `Graph.cs` 는 BFS / DFS / 향후 Dijkstra·A* 가 공유하는 자료구조라 한 단계 위 `Assets/Algorithms/Search/Graph.cs` 에 위치한다.

## 학습 노트 (Notion)

다음 자료는 모두 Notion에 정리되어 있다.

- 알고리즘 개요 / 동작 흐름 / 시간·공간 복잡도 / 게임 사용처
- BFS vs DFS 한눈에 비교표 (자료구조 / 탐색 모양 / 최단 경로 보장 / 시·공간 복잡도)
- 5가지 다이어그램
  1. 클래스 / 모듈 의존 구조 (`Graph.cs` 공유 표시)
  2. `Search` 메서드 흐름도 (Stack 기반 반복)
  3. 자료구조 상태 변화 추적표 (Stack LIFO 가 만들어내는 깊이 우선)
  4. 그래프 위 깊이 탐색 진행 (BFS 동심원과의 시각적 대비)
  5. 런타임 데이터 흐름
- 반복 버전 vs 재귀 버전 코드 비교 — 어느 쪽을 언제 쓸까
- `FindPath` 가 최단을 보장하지 않는 이유 (vs `BFSAlgorithm.FindPath`)
- 직접 만져보며 확인하기 (Stack→Queue 바꿔서 BFS 만들어 보기, 같은 시드로 두 데모 비교)

> **DFS 학습 노트**: [DFS — Notion](https://www.notion.so/34f5e18a573681a0b707c342c39148da)

## 코드 안 학습 주석

각 `.cs` 파일 상단에는 한국어 헤더 블록이 있다.

- `DFSAlgorithm.cs` — 5섹션 (개요 / 동작 흐름 / 복잡도 / 게임 사용처 / 자료구조 선택). 매 섹션마다 BFS 와의 차이를 명시적으로 짚는다.
- `DFSGridVisualizer.cs` — 사용법, 색상 의미, BFS 시각화와의 코드 차이 ("`Queue` → `Stack` 한 줄만 다르다").

라인별 주석에는 `[1] 결과 컨테이너` `[6-1] 스택 위에서 꺼내기` 처럼 단계 번호가 달려 있어,
헤더의 "동작 흐름" 단계와 1:1 로 매핑된다. BFS 의 같은 번호와 옆에 두고 보면 두 알고리즘이 얼마나 닮았는지 한눈에 보인다.

## 빠른 사용법

1. `DFS.unity` 열기 (없으면 `BFS.unity` 를 Duplicate → 이름 변경 → 컴포넌트 교체)
2. 빈 GameObject 생성 → 이름 `DFSDemo`
3. `DFSGridVisualizer` 컴포넌트 부착
4. (선택) Cell Prefab 슬롯을 비워 두면 Cube 가 자동 생성됨
5. (선택) Restart 버튼 셋업: Canvas + Button + `AlgorithmDemoUI` 컴포넌트 (`Assets/Algorithms/Common/`)
6. Play → 노란 Frontier 가 *뱀처럼* 한 방향으로 깊게 파고들고, 목표 도달 후 분홍 경로가 그려짐 (※ 최단 보장 X)

## 색상 의미

| 색 | 의미 |
|----|------|
| 회색 | 미방문 |
| 노랑 | 스택에 들어감 (Frontier — 곧 방문 예정) |
| 파랑 | 방문 완료 |
| 초록 | 시작 정점 |
| 빨강 | 목표 정점 |
| 분홍 | 발견된 경로 위의 칸 (※ 최단 보장 X) |
| 검정 | 벽 (이동 불가) |

> BFS 시각화와 색상 의미는 동일하다. 다만 분홍색의 의미가 BFS 에서는 "최단 경로", DFS 에서는 "발견된 어떤 경로" 라는 점만 다르다.

## 변형 실험

`DFSGridVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- `Width` / `Height` — 그리드 크기
- `Wall Ratio` — 벽 비율 (0.4 이상이면 DFS 가 우회하는 모습이 더 잘 보임)
- `Random Seed` — 같은 시드 = 같은 미로. **BFS 데모와 같은 시드로 맞추면 두 알고리즘의 탐색 패턴을 직접 비교 가능** (학습용 핵심 실험)
- `Step Delay` — 0.1 초 이상으로 올리면 스택의 LIFO 동작이 잘 보임 (방금 들어간 셀이 즉시 처리됨)

`DFSAlgorithm.Search` 의 `Stack<T>` 를 `Queue<T>` 로, `Push`/`Pop` 을 `Enqueue`/`Dequeue` 로 바꾸면 DFS 가 BFS 가 된다 (Frontier 가 한 방향 대신 동심원으로 퍼짐). 두 알고리즘이 **자료구조 한 줄 차이**로 나뉜다는 사실을 직접 확인할 수 있다. Notion 노트의 "직접 만져보며 확인" 섹션 참고.
