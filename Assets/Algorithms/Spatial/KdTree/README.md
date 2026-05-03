# K-d tree — k-dimensional tree (점 집합 분할 트리, kNN 표준)

*공간을 한 축씩 번갈아 가르며* 만든 이진 트리. 모든 점이 자기만의 노드를 가지고, 각 노드는 *한 축의 한 좌표* 에서 공간을 *직각 반평면* 으로 가른다 (depth 0 = X 축, depth 1 = Y 축, depth 2 = X 축, …). **kNN (k-Nearest Neighbors) Query** 를 평균 `O(log N + k)` 로 가속 — 머신러닝 / AI / 입자 시뮬레이션의 사실상 표준.

> **Spatial 4 형제 안에서의 위치 — *Quadtree 의 공간 명확성 + BVH 의 데이터 적응성* 의 하이브리드**
> Quadtree (균등 4 등분, 데이터 무시) 와 BVH (객체 묶음, 형제 겹침) 의 중간. *공간* 을 가르되 *데이터 중앙값* 에서 가르므로 형제 영역은 안 겹치면서도 점 분포에 자연스럽게 적응한다. 점이 *편향 분포* 일 때 Quadtree 보다 균형 잡힌 트리를 만든다.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `KdTreeAlgorithm.cs` | 순수 알고리즘. `KdPoint<T>`, `KdNode<T>` (모든 점이 노드), `KdTree<T>` (top-down alternating-axis median split + KNearest with branch-and-bound), `BruteForceKNearest` (비교용 baseline) |
| `KdTreeVisualizer.cs` | Unity 시각화 MonoBehaviour. 2 페이즈 (Phase 1 BFS Mondrian Build + Phase 2 마우스 kNN with shrinking radius) |

> **공유 의존성** : `Algorithms.Physics.AABB` 를 *시각화 보조* 로 재사용 — 각 노드의 `CellBounds` (조상 분할 반평면 교집합) 표현. BVH 와 달리 알고리즘 본체는 AABB 연산을 사용하지 않고 *축별 좌표 비교* 만으로 동작. 카테고리 횡단 의존을 명시적으로 표현한다는 점에서 같은 패턴.

## 학습 노트 (Notion)

다음 자료는 모두 Notion 에 정리되어 있다.

- 알고리즘 개요 / 동작 흐름 / 시간·공간 복잡도 / 게임 사용처
- 다이어그램
  1. Quadtree / Spatial Hashing / BVH / K-d tree 4 형제 비교 (분할 단위 / 분할 기준 / 형제 겹침 / 킬러 쿼리)
  2. Build 흐름도 (alternating-axis median split 으로 Mondrian 패턴 형성)
  3. kNN 흐름도 (branch-and-bound — near 먼저, far 는 분할 반평면 거리 검사 후)
  4. 가지치기 시각화 — *분할 반평면까지 거리 vs 현재 best-k 의 worst 거리* 비교 한 줄
  5. Brute Force vs K-d tree 가속 비교 (점 N 별 kNN 비용)
- 응용 — *이 알고리즘이 ~~ 로 바뀌면 어떻게 될까* (Ball Tree, M-tree, Annoy / HNSW 같은 ANN, Cover Tree)
- 직접 만져보며 확인하기 (PointCount · k · BuildStepDelay 인스펙터 실험)

> **K-d tree 학습 노트**: [K-d tree — Notion](https://www.notion.so/3555e18a573681a3a84ace545cd6c3f9)

## 코드 안 학습 주석

각 `.cs` 파일 상단에는 한국어 헤더 블록이 있다.

- `KdTreeAlgorithm.cs` — 5섹션 (개요 / 동작 흐름 / 복잡도 / 게임 사용처 / 자료구조 선택). Quadtree / BVH 와의 결정적 차이 (*한 축씩 번갈아 가르며 데이터 중앙값에서 분할*) 명시. 가지치기 한 줄 (*분할 반평면 거리 ≥ worst-k 거리이면 far 통째 스킵*) 을 헤더에서 강조.
- `KdTreeVisualizer.cs` — 사용법, 색상 의미, 2 페이즈 구조, 본 알고리즘과의 동등성 (시각화의 *near/far + pruning 검사* 가 알고리즘의 그것과 1:1 일치).

라인별 주석에는 `[1] best-k 후보 추가`, `[4] 알고리즘의 핵심 한 줄` 처럼 단계 번호가 달려 있어, 헤더의 "동작 흐름" 단계와 1:1 매핑된다.

## 빠른 사용법

1. `Assets/Scenes/Spatial/KdTree.unity` 새로 생성 (`Quadtree.unity` 를 Duplicate 후 컴포넌트만 교체가 가장 빠름)
2. 빈 GameObject 생성 → 이름 `KdTreeDemo`
3. `KdTreeVisualizer` 컴포넌트 부착
4. **카메라 셋업** — 위에서 내려다보는 각도 필수
   - Position: `(0, 25, 0)` 부근
   - Rotation: `(90, 0, 0)`
   - Projection: `Orthographic` 권장, Size `12` (Quadtree / BVH 와 동일)
5. (선택) Restart 버튼 셋업: Canvas + Button + `AlgorithmDemoUI` 컴포넌트 (`Assets/Algorithms/Common/`)
6. Play
   - **Phase 1**: 점들이 회색으로 등장 후, BFS 순서로 분할선이 한 개씩 페이드인. *세로선 (X 축 분할) ↔ 가로선 (Y 축 분할)* 이 번갈아 등장하며 *Mondrian 그림* 패턴이 완성된다.
   - **Phase 2**: Build 종료 후 마우스를 움직이면 파란 쿼리 점이 따라다니고, 주변에 *현재 best-k 의 worst 거리* 만큼 노란 원이 그려진다. 점이 모인 곳에 마우스를 가져가면 원이 *작아지고*, 외딴 곳에 가져가면 원이 *커진다*. 분홍 = top-k 결과, 주황 = visited but not topK, 회색 = pruned.

## 색상 의미

| 색 | 의미 |
|----|------|
| 분할선 옅은 파랑 | X 축 분할선 (idle) — 세로선 |
| 분할선 옅은 분홍 | Y 축 분할선 (idle) — 가로선 |
| 분할선 주황 | 알고리즘이 *실제로 들어간* 노드 (visited) |
| 분할선 회색 (반투명) | 가지치기된 서브트리 |
| 점 회색 | pruned (검사 자체 안 됨) |
| 점 주황 | visited but not in top-k |
| 점 분홍 | top-k 결과 (가장 가까운 k 개) |
| 노란 원 | 현재 best-k 의 *worst 거리* = 반경 |
| 파란 점 | 쿼리 점 (마우스 위치) |

> Quadtree / BVH 의 "주황 = 방문 / 분홍 = 결과" 컨벤션 그대로 계승. K-d tree 는 *번갈아 가르는* 축 패턴이 핵심이라 분할선 idle 색을 *축별로 다르게* 두어 한눈에 보이게 했다 (X = 파랑 세로 / Y = 분홍 가로).

## 변형 실험

`KdTreeVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- **Point Count** (10 ~ 500) — 점이 많을수록 분할이 깊고, 가속 효과가 체감된다. OnGUI 의 "방문 노드 vs Brute Force = N" 비율로 확인.
- **k** (1 ~ 20) — 가장 가까운 몇 개를 찾을지. k=1 이면 NN (single nearest), k=5 이면 kNN. *k 가 클수록 노란 원이 커지고 가지치기가 줄어든다* (worst 거리가 멀어지므로).
- **Build Step Delay** — 0.2 초 이상으로 올리면 분할선이 한 개씩 등장하는 *Mondrian* 형성 과정이 또렷이 보인다.
- **Random Seed** — 같은 시드 = 같은 점 분포 (재현성).

### 사고 실험

- **점이 한 직선 위에 정렬되면?** — 깊이가 깊어지고 트리가 사실상 linked list 가 된다. kNN 최악 성능 O(N). 실제로는 거의 발생 안 하지만 K-d tree 의 약점.
- **고차원 (k ≥ 10) 으로 가면?** — *차원의 저주*. 차원이 늘수록 \"가까운 점\" 의 의미가 흐려져 가지치기 효과가 급격히 떨어짐. 100 차원 이상에서는 K-d tree 가 brute force 와 거의 같은 비용. → **HNSW / Annoy / FAISS** 같은 ANN (Approximate Nearest Neighbor) 알고리즘이 모던 벡터 검색의 표준이 된 이유.
- **Median split 대신 mean split 을 쓰면?** — 빌드는 빠르지만 (정렬 불필요) 트리 균형이 깨지기 쉽다. 점이 비균일 분포일 때 한쪽으로 기울어진 트리가 됨.
- **점이 자주 추가/삭제되면?** — K-d tree 는 *정적 자료구조*. 동적 변경이 잦으면 R-tree / Ball Tree / Cover Tree 같은 *동적 균형 트리* 가 더 적합. Box2D / Bullet 의 broad-phase 가 BVH 를 쓰는 이유와 같음.
- **3D 로 확장하면?** — 자식 수는 그대로 2 (binary tree). 분할 축만 X / Y / Z 3 축을 번갈아 (depth % 3) — 알고리즘 골격 완전 동일. 4D 이상도 같은 방식.

## Spatial 가족 한눈에

| 항목 | Quadtree | Spatial Hashing | BVH | **K-d tree** |
|------|----------|-----------------|-----|--------------|
| 분할 단위 | 공간 (4 등분) | 공간 (균등 격자) | 객체 (가까운 둘 묶음) | **공간 (반평면)** |
| 분할 기준 | 균등 분할 | 균등 분할 | 객체 중심 median | **데이터 중앙값 (한 축씩)** |
| 형제 영역 겹침 | ❌ | ❌ | ✅ (정의) | **❌** |
| 데이터 적응 | ❌ | ❌ | ✅ | **✅ (median 기반)** |
| 자식 수 | 4 | (해시) | 2 | **2** |
| 킬러 쿼리 | 영역 (range) | 영역 (range, 동적) | 광선 (ray) | **kNN (최근접)** |
| 동적 객체 | △ | ⭕ | △ (Refit) | △ (정적 권장) |

이 표가 "K-d tree 는 *kNN 에 특화된, 공간을 한 축씩 번갈아 가르는* 트리" 라는 한 줄 요약. 머신러닝 KNN 분류기 / 벡터 유사도 검색 / AI 근접 적 탐색 / SPH 입자 시뮬레이션의 표준 자료구조.
