# Flood Fill — 영역 채우기

격자 위 시작 셀과 *같은 값(색)* 을 가진 4-연결 인접 셀들을 모두 새로운 값으로 교체한다.
본질은 BFS / DFS 의 **그리드 응용판** — 그래프가 따로 없고, "이웃의 값이 같은가?" 라는
*값 매칭 조건* 이 즉석에서 간선 역할을 한다.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `FloodFillAlgorithm.cs` | 순수 Flood Fill 로직. `Fill` (반복, 권장), `FillRecursive` (재귀, 학습용). 격자(`T[,]`) 와 시작 좌표만 받는다 |
| `FloodFillGridVisualizer.cs` | Unity 격자 위에서 한 스텝씩 시각화하는 MonoBehaviour. Voronoi 로 색 영역을 만들어 같은 색만 채우는 모습을 보여준다 |

> Flood Fill 은 그래프(`Graph<T>`) 가 필요 없으므로 BFS / DFS 와 달리 `Search/Graph.cs` 에 의존하지 않는다.
> 격자 자체가 자료구조이며, 셀 값이 곧 '연결' 정보를 결정한다.

## 학습 노트 (Notion)

다음 자료는 모두 Notion에 정리되어 있다.

- 알고리즘 개요 / 동작 흐름 / 시간·공간 복잡도 / 게임 사용처
- BFS vs Flood Fill 비교표 (그래프 사전 구축 vs 동적 값 매칭, visited 집합 필요 여부)
- 다이어그램
  1. 클래스 / 모듈 의존 구조 (Graph 의존 없음 — BFS / DFS 와의 차이)
  2. `Fill` 메서드 흐름도 (Queue 기반 반복)
  3. 자료구조 상태 변화 추적표 (Queue + 격자 in-place 교체)
  4. 격자 위 영역 확산 패턴 (Voronoi 색 영역에서 한 영역만 채워지는 모습)
- 반복 vs 재귀 트레이드오프 — 큰 영역에서의 StackOverflow 위험
- 직접 만져보며 확인하기 (Queue→Stack 으로 바꿔 DFS 기반 Flood Fill 만들기, regionCount 변화 실험)

> **Flood Fill 학습 노트**: [Flood Fill — Notion](https://www.notion.so/3505e18a57368126b302f8e575a3fddf)

## 코드 안 학습 주석

각 `.cs` 파일 상단에는 한국어 헤더 블록이 있다.

- `FloodFillAlgorithm.cs` — 5섹션 (개요 / 동작 흐름 / 복잡도 / 게임 사용처 / 자료구조 선택). BFS·DFS 와의 차이를 매 섹션마다 명시.
- `FloodFillGridVisualizer.cs` — 사용법, 색상 의미, 본 알고리즘과 시각화 코드의 관계, BFS 시각화와의 차이.

라인별 주석에는 `[1] 결과 컨테이너` `[8-1] 큐 앞에서 꺼내기` 처럼 단계 번호가 달려 있어,
헤더의 "동작 흐름" 단계와 1:1 로 매핑된다. BFS 의 같은 번호와 옆에 두고 보면
"그래프 의존이 사라지고 값 매칭이 그 자리를 차지한 것" 이 한눈에 보인다.

## 빠른 사용법

1. `Flood_Fill.unity` 열기 (없으면 `BFS.unity` 를 Duplicate → 이름 변경 → 컴포넌트 교체)
2. 빈 GameObject 생성 → 이름 `FloodFill`
3. `FloodFillGridVisualizer` 컴포넌트 부착
4. (선택) Cell Prefab 슬롯을 비워 두면 Cube 가 자동 생성됨
5. (선택) Restart 버튼 셋업: Canvas + Button + `AlgorithmDemoUI` 컴포넌트 (`Assets/Algorithms/Common/`)
6. Play → 무작위로 만들어진 색 패치들 중에서, 시작 셀(초록) 이 속한 영역만 *동심원 모양으로* 분홍으로 채워짐

## 색상 의미

| 색 | 의미 |
|----|------|
| 영역 베이스 색 (회색·파랑·주황·연두·…) | 각 Voronoi 영역. 같은 색이 곧 같은 영역(= 채울 후보) |
| 노랑 | 큐에 들어감 (Frontier — 곧 채워질 예정) |
| 분홍 | 채움 완료 (newValue 로 교체된 셀) |
| 초록 | 시작 셀 |

> BFS / DFS 시각화의 색상 체계와 일관되게 유지했다. 단, Flood Fill 에는 **목표 셀(빨강)** 과 **벽(검정)** 이 없다 — 대신 *다른 색 영역이 자연스럽게 경계 역할* 을 한다.

## 변형 실험

`FloodFillGridVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- `Width` / `Height` — 격자 크기. 클수록 영역도 커진다.
- `Region Count` — Voronoi 시드 개수. 2~3 이면 큰 영역, 6~8 이면 작은 패치.
  값을 늘릴수록 시작 셀이 속한 영역도 작아져 채워지는 셀 수가 줄어든다.
- `Random Seed` — 같은 시드 = 같은 영역 배치 (재현성).
- `Step Delay` — 0.1 초 이상으로 올리면 큐 FIFO 동작이 잘 보인다 (가까운 셀부터 동심원처럼 퍼짐).
- `Start Coord` — 시작점을 옮겨가며 같은 격자에서도 어느 영역을 골랐냐에 따라
  채워지는 모양이 완전히 달라지는 것을 관찰할 수 있다.

`FloodFillAlgorithm.Fill` 의 `Queue<T>` 를 `Stack<T>` 로 바꾸면 Flood Fill 이 *DFS 기반* 으로 바뀐다 (한 방향으로 깊게 들어가는 모양). **결과(채워진 영역)는 같지만 순서가 다르다.** BFS / DFS 와 정확히 같은 자료구조 한 줄 차이다 — 학습용으로 강력한 대비.

## BFS / DFS 와의 비교 한눈에

| 항목 | BFS / DFS | Flood Fill |
|------|----------|------------|
| 입력 | 미리 만들어진 `Graph<T>` (인접 리스트) | 2D 격자 `T[,]` |
| 간선 결정 시점 | 사전 (그래프 구축 단계) | 즉석 (이웃의 값 == 시작 셀 값) |
| `visited` 집합 | 필요 | 불필요 — 격자 in-place 교체가 방문 표시 역할 |
| 일반적인 목적 | 도달성 / 최단 경로 / 순회 순서 | 연결된 같은-값 영역 식별 + 일괄 변경 |
| 자료구조 핵심 | Queue (BFS) / Stack (DFS) | 둘 다 가능. 결과는 같고 순서만 다름 |

이 표가 "Flood Fill 은 BFS / DFS 의 *그리드 응용판* 이다" 라는 한 줄 요약이다.
