# BFS — Breadth-First Search

너비 우선 탐색. 시작 정점에서 가까운 정점부터 동심원처럼 퍼져 나가며 방문한다.
모든 간선의 가중치가 같을 때 **최단 경로(간선 수 기준)** 를 보장한다.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `Graph.cs` | 인접 리스트 그래프 자료구조. `Dictionary<T, List<T>>` 기반 |
| `BFSAlgorithm.cs` | 순수 BFS 로직. `Search` (전체 순회), `FindPath` (최단 경로 복원) |
| `BFSGridVisualizer.cs` | Unity 그리드 위에서 한 스텝씩 시각화하는 MonoBehaviour |

## 학습 노트 (Notion)

다음 자료는 모두 Notion에 정리되어 있다.

- 알고리즘 개요 / 동작 흐름 / 시간·공간 복잡도 / 게임 사용처
- 5가지 다이어그램
  1. 클래스 / 모듈 의존 구조
  2. `Search` 메서드 흐름도
  3. 자료구조 상태 변화 추적표
  4. 그래프 위 동심원 확장
  5. 런타임 데이터 흐름
- 학습 순서 (어디서부터 읽는 것이 효율적인가)
- 직접 만져보며 확인하기 (Queue→Stack 바꿔서 DFS 만들어 보기 등)

> **BFS 학습 노트**: [BFS — Notion](https://www.notion.so/34f5e18a57368191994ed675ceb4f846)

## 코드 안 학습 주석

각 `.cs` 파일 상단에는 한국어 헤더 블록이 있다.

- `Graph.cs` — 인접 행렬 vs 인접 리스트 비교, `Dictionary<T, List<T>>` 를 쓰는 이유
- `BFSAlgorithm.cs` — 5섹션 (개요 / 동작 흐름 / 복잡도 / 게임 사용처 / 자료구조 선택)
- `BFSGridVisualizer.cs` — 사용법, 색상 의미, 본 알고리즘과 시각화 코드의 관계

라인별 주석에는 `[1] 결과 컨테이너` `[6-1] 큐 앞에서 꺼내기` 처럼 단계 번호가 달려 있어,
헤더의 "동작 흐름" 단계와 1:1 로 매핑된다.

## 빠른 사용법

1. `SampleScene` 열기
2. 빈 GameObject 생성 → 이름 `BFSDemo`
3. `BFSGridVisualizer` 컴포넌트 부착
4. (선택) Cell Prefab 슬롯을 비워 두면 Cube 가 자동 생성됨
5. Play → 노란 Frontier 가 동심원으로 퍼지고, 목표 도달 후 분홍 최단 경로가 그려짐

## 색상 의미

| 색 | 의미 |
|----|------|
| 회색 | 미방문 |
| 노랑 | 큐에 들어감 (Frontier — 곧 방문 예정) |
| 파랑 | 방문 완료 |
| 초록 | 시작 정점 |
| 빨강 | 목표 정점 |
| 분홍 | 최단 경로 위의 칸 |
| 검정 | 벽 (이동 불가) |

## 변형 실험

`BFSGridVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- `Width` / `Height` — 그리드 크기
- `Wall Ratio` — 벽 비율 (0.4 이상이면 도달 불가 케이스를 만들기 쉬움)
- `Random Seed` — 같은 시드 = 같은 미로 (재현성)
- `Step Delay` — 0.3 초 정도로 올리면 큐에 들어가는 순간 ↔ 처리되는 순간 시간차가 잘 보임

`BFSAlgorithm.Search` 의 `Queue<T>` 를 `Stack<T>` 로 바꾸면 BFS 가 DFS 가 된다 (Frontier 가 동심원 대신 한 방향으로 깊게 파고듦). Notion 노트의 "직접 만져보며 확인" 섹션 참고.
