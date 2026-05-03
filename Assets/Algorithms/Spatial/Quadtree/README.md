# Quadtree — 사분 트리 (2D 공간 분할)

2D 공간을 *재귀적으로 4 등분* 해 점/객체를 정리하는 트리. 한 노드의 점이 capacity 를 초과하면 영역을 4 개 (NW · NE · SW · SE) 로 나누고 자식에 재분배한다. **range query** (영역 안의 점 찾기) 를 평균 `O(log N + K)` 로 가속 — Brute Force `O(N)` 대비 N 이 클수록 차이가 폭발적이다.

본질은 BST (이진 탐색 트리) 의 2D 일반화 — BST 가 *값* 을 반으로 가르듯, Quadtree 는 *공간* 을 4 등분으로 가른다. 3D 로 확장하면 8 등분 = **Octree**.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `QuadtreeAlgorithm.cs` | 순수 알고리즘. `QuadtreeBounds` (AABB), `QuadtreePoint<T>`, `Quadtree<T>` (Insert / Subdivide / Query), `BruteForceQuery` (비교용 baseline) |
| `QuadtreeVisualizer.cs` | Unity 시각화 MonoBehaviour. 2 페이즈 (Phase 1 삽입 애니메이션 + Phase 2 마우스 쿼리) |

> Quadtree 는 다른 탐색 알고리즘들과 자료구조를 공유하지 않는다 — `Graph<T>` / `WeightedGraph<T>` / `MinPriorityQueue<T>` 의존 없음. 트리 자체가 자료구조이며, 새 카테고리 `Algorithms.Spatial` 의 첫 알고리즘이다.

## 학습 노트 (Notion)

다음 자료는 모두 Notion에 정리되어 있다.

- 알고리즘 개요 / 동작 흐름 / 시간·공간 복잡도 / 게임 사용처
- 다이어그램
  1. 클래스 / 모듈 의존 구조 (Graph 의존 없음 — 새 카테고리)
  2. Insert / Subdivide 흐름도 (capacity 초과 → 분할 → 재분배)
  3. Query 흐름도 (pruning 가지치기로 자손 통째 스킵하는 구조)
  4. 자료구조 상태 변화 추적표 (점 N 개 삽입 동안 트리 깊이/노드 수 추이)
  5. Brute Force vs Quadtree 가속 비교 (N=100 / 500 / 2000 시점별)
- 응용 — *이 알고리즘이 ~~ 로 바뀌면 어떻게 될까* (Octree, Loose Quadtree, k-d tree)
- 직접 만져보며 확인하기 (Capacity·MaxDepth·PointCount 인스펙터 실험)

> **Quadtree 학습 노트**: [Quadtree — Notion](https://www.notion.so/3555e18a57368183a860d9e87c8b1b35)

## 코드 안 학습 주석

각 `.cs` 파일 상단에는 한국어 헤더 블록이 있다.

- `QuadtreeAlgorithm.cs` — 5섹션 (개요 / 동작 흐름 / 복잡도 / 게임 사용처 / 자료구조 선택). BFS 와의 결정적 차이 (visited 집합 불필요 — 트리는 사이클이 없으므로) 명시.
- `QuadtreeVisualizer.cs` — 사용법, 색상 의미, 2 페이즈 구조, 본 알고리즘과의 동등성 (시각화의 *교차 검사* 가 알고리즘의 *pruning 검사* 와 정확히 같음).

라인별 주석에는 `[1] 영역 밖이면 거부`, `[3] internal 노드 — 자식에 위임` 처럼 단계 번호가 달려 있어, 헤더의 "동작 흐름" 단계와 1:1 매핑된다.

## 빠른 사용법

1. `Assets/Scenes/Spatial/Quadtree.unity` 열기
2. 빈 GameObject 생성 → 이름 `QuadtreeDemo`
3. `QuadtreeVisualizer` 컴포넌트 부착
4. **카메라 셋업** — 위에서 내려다보는 각도 필수
   - Position: `(0, 25, 0)` 부근
   - Rotation: `(90, 0, 0)` (정확히 아래 방향)
   - Projection: `Orthographic` 권장, Size `12`
   - 또는 Perspective 라면 FOV 60, 거리 25 정도
5. (선택) Restart 버튼 셋업: Canvas + Button + `AlgorithmDemoUI` 컴포넌트 (`Assets/Algorithms/Common/`)
6. Play
   - **Phase 1**: 점들이 한 개씩 노란 플래시와 함께 삽입되며, capacity 초과 시점마다 사각형 라인이 4 분할
   - **Phase 2**: 삽입 종료 후 마우스를 움직이면 빨간 쿼리 영역이 따라다니고, 교차하는 노드만 노란색 + 결과 점이 분홍색으로 강조됨

## 색상 의미

| 색 | 의미 |
|----|------|
| 라인 회색 | 미방문 노드 (또는 쿼리와 교차하지 않는 = 통째로 스킵된 노드) |
| 라인 노랑 | 방문 노드 (쿼리와 교차) — *알고리즘이 실제로 들어간* 노드 |
| 라인 빨강 | 마우스 쿼리 영역 |
| 점 회색 | 일반 객체 |
| 점 노랑 | 방금 삽입됨 (Phase 1 의 플래시) |
| 점 분홍 | 쿼리 결과 |

> BFS / DFS 의 "노랑 = frontier", "분홍 = 결과", "빨강 = 목표" 컨벤션을 그대로 계승했다.
> Quadtree 에는 *시작 정점 (초록)* / *벽 (검정)* 이 없다 — 알고리즘 성격이 *탐색* 이 아닌 *공간 인덱싱* 이라 두 색이 자연스럽게 빠진다.

## 변형 실험

`QuadtreeVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- **Capacity** (1 ~ 16) — 작을수록 빨리 분할, 트리가 깊어진다. 1 로 두면 거의 모든 분할이 일어나는 극단 시각화.
- **Max Depth** (3 ~ 8) — 분할 한계. 점이 한 점에 몰릴 때 깊이 폭발을 막는 안전장치.
- **Point Count** (50 ~ 2000) — 점이 많을수록 가속 효과가 체감된다. OnGUI 카운터의 "검사한 점 vs Brute Force" 비율로 확인.
- **Query Half Size** — 쿼리 영역 크기. 작게 두면 방문 노드 수가 극도로 줄어든다.
- **Insert Step Delay** — 0.1 초 이상으로 올리면 분할 순간이 한 단계씩 또렷이 보인다.
- **Random Seed** — 같은 시드 = 같은 점 분포 (재현성).

### 사고 실험

- **Quadtree 의 Queue 를 Stack 으로 바꾸면?** — 의미가 없다. Quadtree 는 BFS/DFS 같은 *탐색* 이 아니라 *재귀 트리 위 공간 분할* 이라 Queue / Stack 자료구조가 등장하지 않는다 (호출 스택이 곧 재귀 흐름).
- **점이 한 점에 1000 개 몰리면?** — Max Depth 까지 분할되다가 멈추고 그 leaf 가 1000 개를 모두 보관 (overflow 허용). Max Depth 안전장치가 없으면 무한 재귀 → 스택 폭발.
- **Octree 로 확장하면?** — 자식이 4 → 8, `QuadtreeBounds` 에 Z 축 한 줄 추가, `Subdivide` 에서 8 개 자식 생성. 알고리즘 골격은 그대로.
- **k-d tree 와 어떻게 다른가?** — Quadtree 는 *공간을 균등 분할* (모든 자식이 같은 크기), k-d tree 는 *데이터를 반으로 분할* (median 점에서 가름). 점이 균등 분포면 둘 다 비슷, 편향 분포면 k-d tree 가 더 균형 잡힌 트리를 만든다.

## BFS / DFS 와의 비교 한눈에

| 항목 | BFS / DFS | Quadtree |
|------|----------|----------|
| 카테고리 | 탐색 (Search) | 공간 분할 (Spatial) |
| 입력 | 미리 만들어진 `Graph<T>` | 2D 점 집합 (좌표 + 데이터) |
| 자료구조 | Queue (BFS) / Stack (DFS) | 재귀 트리 (호출 스택이 곧 진행 흐름) |
| `visited` 집합 | 필요 (사이클 방지) | 불필요 — 트리는 사이클이 없음 |
| 일반적 목적 | 도달성 / 최단 경로 / 순회 | "이 영역 안의 점은?" 같은 *공간 쿼리* |
| 가속 효과 | 그래프 구조 자체로 한 번씩만 방문 | 영역 교차 안 하면 자손 *통째로 스킵* |

이 표가 "Quadtree 는 *탐색이 아니라 인덱싱이다*" 라는 한 줄 요약이다.
