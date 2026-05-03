# Spatial Hashing — 균등 격자 공간 인덱싱

2D 공간을 *균등한 격자 셀* 로 나누고, 각 셀이 그 안의 점 리스트를 보유한다. 점을 셀에 넣을 때는 좌표를 셀 크기로 나눠 *셀 좌표* 를 구하고 (= 해시), 그 셀의 리스트에 추가. 쿼리 영역이 덮는 셀들만 순회하며 그 안의 점만 정밀 검사.

**구현 30 줄짜리 자료구조** 가 N 이 클수록 Brute Force 대비 수십~수백 배 가속을 만든다. Quadtree 보다 단순하지만 *동적 객체* (탄막, 입자) 에 압도적으로 강해 슈팅 게임 / Boids / MMO AoI 의 사실상 표준.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `SpatialHashAlgorithm.cs` | 순수 알고리즘. `SpatialHashBounds` (AABB), `SpatialHashPoint<T>`, `SpatialHash<T>` (Insert / Query / Clear), `BruteForceQuery` (비교용 baseline) |
| `SpatialHashVisualizer.cs` | Unity 시각화 MonoBehaviour. 2 페이즈 (Phase 1 격자 + 점 삽입 / Phase 2 마우스 쿼리). 점 색상 3 종 (idle / candidate / result) 으로 broad↔narrow 차이 표현 |

> Spatial Hashing 은 다른 탐색 알고리즘들과 자료구조를 공유하지 않는다 — `Graph<T>` / `WeightedGraph<T>` / `MinPriorityQueue<T>` 의존 없음. 같은 카테고리 `Algorithms.Spatial` 의 Quadtree 와도 별도 구조체 (`SpatialHashBounds`, `SpatialHashPoint`) 를 보유 — 학습 시 다른 파일을 열지 않아도 자기 알고리즘만 읽으면 끝.

## 학습 노트 (Notion)

다음 자료는 모두 Notion 에 정리되어 있다.

- 알고리즘 개요 / 동작 흐름 / 시간·공간 복잡도 / 게임 사용처
- 다이어그램
  1. Insert / Query 흐름도 (broad-phase ↔ narrow-phase 분리)
  2. 셀 크기 튜닝 가이드 (너무 크면 / 너무 작으면)
  3. Quadtree vs Spatial Hashing 직접 비교표
- 응용 — Multi-cell 등록 (큰 객체), Loose 격자, 동적 셀 크기
- 직접 만져보며 확인하기 (CellSize / PointCount 인스펙터 실험)

> **Spatial Hashing 학습 노트**: [Spatial Hashing — Notion](https://www.notion.so/3555e18a573681fe9b13cd4f6938fb5c)

## 코드 안 학습 주석

각 `.cs` 파일 상단에는 한국어 헤더 블록이 있다.

- `SpatialHashAlgorithm.cs` — 5섹션 (개요 / 동작 흐름 / 복잡도 / 게임 사용처 / 자료구조 선택). Quadtree 와의 차이를 매 섹션마다 명시.
- `SpatialHashVisualizer.cs` — 2 페이즈 구조, 색상 3 종의 의미 (broad ↔ narrow), Quadtree 데모와의 시각적 차이.

라인별 주석에는 `[1] 셀 좌표 변환`, `[3] 점 추가` 처럼 단계 번호가 달려 있어 헤더의 "동작 흐름" 단계와 1:1 매핑된다.

## 빠른 사용법

1. `Assets/Scenes/0.TempScene.unity` 를 복사 → `Assets/Scenes/Physics/SpatialHashing.unity` 로 이름 변경
2. 빈 GameObject 생성 → 이름 `SpatialHashDemo` → `SpatialHashVisualizer` 컴포넌트 부착
3. **카메라 셋업** — 위에서 내려다보는 각도 필수
   - Position `(0, 25, 0)` / Rotation `(90, 0, 0)`
   - Projection `Orthographic`, Size `12`
4. (선택) Restart 버튼 셋업: Canvas + Button + `AlgorithmDemoUI` 컴포넌트 (`Assets/Algorithms/Common/`)
5. Play
   - **Phase 1**: 점들이 한 개씩 노란 플래시와 함께 삽입되며, 그 점이 들어간 셀 라인이 잠깐 깜빡인다 (= 그 셀에 등록되었음)
   - **Phase 2**: 삽입 종료 후 마우스를 따라 빨간 쿼리 영역이 따라다니고, 덮는 셀 = 노랑, 그 셀 안의 점 = 주황(후보), 쿼리 영역 안 = 분홍(결과)

## 색상 의미

| 색 | 의미 |
|----|------|
| 라인 회색 | 미방문 셀 (또는 쿼리와 교차하지 않는 = 통째로 스킵) |
| 라인 노랑 | 방문 셀 (쿼리가 덮음) — broad-phase 후보 |
| 라인 빨강 | 마우스 쿼리 영역 |
| 점 회색 | 일반 객체 (덮이지 않은 셀에 있음) |
| 점 노랑 | 방금 삽입됨 (Phase 1 플래시) |
| 점 **주황** | **후보** (broad 통과, narrow 탈락) — 셀은 덮였지만 정밀 검사에서 제외 |
| 점 **분홍** | **결과** (narrow 통과) |

> **주황 ↔ 분홍의 차이가 학습 핵심** — Spatial Hashing 의 가속은 *2 단계* 다: ① 셀로 후보 추리기 (broad), ② 후보 점에 대해 Contains 정밀 검사 (narrow). 주황색 점들은 "셀이 쿼리를 덮어서 검사는 했지만 실제 영역 밖이라 탈락" 한 점들 — 이 비율이 cellSize 튜닝의 척도다.

## 변형 실험

`SpatialHashVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- **Cell Size** — 이 알고리즘의 *유일하지만 결정적인* 튜닝 포인트
  - **너무 크게** (예: worldSize 와 비슷) → 한 셀에 점이 몰려 사실상 Brute Force
  - **너무 작게** (예: 0.3) → 빈 셀이 폭증, 큰 객체가 여러 셀에 걸침
  - 경험칙: `cellSize ≈ queryHalfSize × 2` 정도가 균형
- **Point Count** (50 \~ 2000) — 점이 많을수록 가속 효과 체감. OnGUI 카운터로 확인.
- **Query Half Size** — 쿼리 영역 크기. 작을수록 덮은 셀 수가 극도로 줄어든다.
- **Insert Step Delay** — 0.1 초 이상으로 올리면 점이 어느 셀에 들어가는지 한 단계씩 또렷이 보임.
- **Random Seed** — 같은 시드 = 같은 점 분포.

### 사고 실험

- **점이 한 점에 1000 개 몰리면?** — 한 셀의 List 가 1000 개 길이가 됨. 그 셀이 덮이면 1000 개 모두 narrow-phase 검사 → 가속 효과가 *그 셀 안에서는* 사라진다. → Quadtree 였다면 maxDepth 까지 분할되었을 것.
- **모든 점이 균등 분포면?** — 각 셀이 평균 N/M 개. 쿼리가 덮은 셀 수 × N/M ≪ N → 가속이 최대로 발휘.
- **객체가 매 프레임 움직이면?** — Quadtree 는 트리 전체 재구축이 비싸고, Spatial Hashing 은 셀 좌표만 다시 계산해 다른 셀로 옮기면 끝. → **동적 객체** 의 결정적 우위.
- **3D 로 확장하면?** — 셀 좌표가 (cx, cy, cz) 정수 3 개로 늘어날 뿐 알고리즘 골격은 동일. Octree 와 짝을 이룬다.

## Quadtree 와의 직접 비교

| 항목 | Quadtree | Spatial Hashing |
|---|---|---|
| 분할 방식 | *적응적* — 점 많은 곳만 깊이 분할 | *균등* — 모든 셀 같은 크기 |
| 점 균등 분포 | 좋음 | **최고** |
| 점 편향 분포 | 좋음 (트리가 자연스럽게 적응) | 나쁨 (한 셀에 몰림) |
| 동적 객체 | 매 프레임 재구축 비용 큼 | **셀 좌표 갱신만** — 빠름 |
| 메모리 | 트리 노드 오버헤드 | 빈 셀 = 키 없음 → 적음 |
| 튜닝 | capacity / maxDepth | **cellSize** (이게 전부) |
| 구현 난이도 | 중간 (재귀 + 재분배) | 매우 쉬움 |
| 자료구조 | 재귀 트리 (`Quadtree<T>[4]`) | `Dictionary<(int, int), List<Point>>` |

이 표가 "두 자료구조가 모두 살아남는 이유" 의 한 줄 요약이다 — *분포가 균등 + 객체가 정적* 이면 어느 쪽이든 좋고, *분포 편향* 이면 Quadtree, *객체 동적* 이면 Spatial Hashing.

## BFS / DFS 와의 비교 한눈에

| 항목 | BFS / DFS | Spatial Hashing |
|------|----------|-----------------|
| 카테고리 | 탐색 (Search) | 공간 분할 (Spatial) |
| 입력 | 미리 만들어진 `Graph<T>` | 2D 점 집합 (좌표 + 데이터) |
| 자료구조 | Queue (BFS) / Stack (DFS) | `Dictionary<(int,int), List<Point>>` |
| `visited` 집합 | 필요 (사이클 방지) | 불필요 — 점 하나는 정확히 한 셀에만 들어감 |
| 일반적 목적 | 도달성 / 최단 경로 / 순회 | "이 영역 안의 점은?" *공간 쿼리* |
| 가속 효과 | 그래프 구조 자체로 한 번씩만 방문 | 균등 격자로 *덮인 셀만* 검사 |

이 표가 "Spatial Hashing 도 *탐색이 아니라 인덱싱이다*" 라는 한 줄 요약이다 (Quadtree 와 같은 카테고리).
