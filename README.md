# Algorithm Study

게임에서 사용되는 알고리즘을 Unity 위에서 학습하고 시각적으로 확인하는 프로젝트.

각 알고리즘은 다음 세 층으로 구성된다:

- **코드** — C# 본체 + 한국어 학습 주석 (헤더 블록 + 라인별 단계 주석)
- **시각화** — Unity 그리드 위에서 한 스텝씩 진행되는 데모 (MonoBehaviour + 코루틴)
- **학습 노트** — 다이어그램·비교표·학습 순서를 Notion에 정리

## 환경

- Unity **6000.3.10f1** (Unity 6)
- 외부 패키지 의존성 없음 (셀은 기본 Cube 프리미티브로 자동 생성)

## 실행 방법

1. Unity Hub에서 이 프로젝트 폴더 열기
2. `Assets/Scenes/` 의 알고리즘별 씬을 연다
3. 씬 안에 Visualizer 가 붙은 GameObject 가 없으면:
   - 빈 GameObject 생성 → 원하는 알고리즘의 Visualizer 컴포넌트 부착
4. 카메라 배치 권장:
   - BFS / DFS / Flood Fill / Minimax / MCTS / Behavior Tree / GOAP / Utility AI / FSM / Quadtree / Spatial Hashing / AABB / BVH : 위에서 내려다보는 각도 — 그리드/보드가 평면
   - Dijkstra / A\* : 비스듬한 내려각 — 셀 높이 (= 가중치) 가 잘 보이도록
5. (선택) Restart 버튼 셋업: Canvas + Button + `AlgorithmDemoUI` 컴포넌트 (`Assets/Algorithms/Common/`)
6. Play → 색상 변화로 탐색 진행 관찰

자세한 워크플로(새 알고리즘 추가 절차 등)는 [`CLAUDE.md`](CLAUDE.md) 참고.

## 학습 노트 (Notion)

알고리즘 개요, 다이어그램, 비교표, 학습 순서 같은 시각 자료는 Notion 학습 허브에 정리되어 있다.

> **Notion 학습 허브**: [📚 알고리즘 정리](https://www.notion.so/34f5e18a5736819e9a6bc804cbe12a42)

## 알고리즘 인덱스

| # | 알고리즘 | 카테고리 | 코드 | Notion 노트 | 씬 |
|---|----------|----------|------|-------------|---|
| 01 | BFS (Breadth-First Search) | Search | [`Search/BFS`](Assets/Algorithms/Search/BFS) | [BFS 노트](https://www.notion.so/34f5e18a57368191994ed675ceb4f846) | `Assets/Scenes/BFS.unity` |
| 02 | DFS (Depth-First Search) | Search | [`Search/DFS`](Assets/Algorithms/Search/DFS) | [DFS 노트](https://www.notion.so/34f5e18a573681a0b707c342c39148da) | `Assets/Scenes/DFS.unity` |
| 03 | Dijkstra (다익스트라 최단 경로) | Search | [`Search/Dijkstra`](Assets/Algorithms/Search/Dijkstra) | [Dijkstra 노트](https://www.notion.so/34f5e18a5736815898f9ccc8f3e977cb) | `Assets/Scenes/Dijkstra.unity` |
| 04 | A\* (A-Star Pathfinding) | Search | [`Search/AStar`](Assets/Algorithms/Search/AStar) | [A\* 노트](https://www.notion.so/34f5e18a573681b08b79cb481fd52238) | `Assets/Scenes/AStar.unity` |
| 05 | Flood Fill (영역 채우기) | Search | [`Search/FloodFill`](Assets/Algorithms/Search/FloodFill) | [Flood Fill 노트](https://www.notion.so/3505e18a57368126b302f8e575a3fddf) | `Assets/Scenes/Flood_Fill.unity` |
| 06 | Minimax + Alpha-Beta (게임 트리 탐색) | AI | [`AI/TicTacToe`](Assets/Algorithms/AI/TicTacToe) | [Minimax 노트](https://www.notion.so/3505e18a5736816b935eec489638c4cd) | `Assets/Scenes/Minimax.unity` |
| 07 | MCTS (Monte Carlo Tree Search) | AI | [`AI/MCTS`](Assets/Algorithms/AI/MCTS) | [MCTS 노트](https://www.notion.so/3505e18a5736816cb85afb7f0b523ed9) | `Assets/Scenes/MCTS.unity` |
| 08 | Behavior Tree (NPC 의사결정) | AI | [`AI/BehaviorTree`](Assets/Algorithms/AI/BehaviorTree) | [BT 노트](https://www.notion.so/3505e18a573681d9b5d6edac313c44c8) | `Assets/Scenes/BehaviorTree.unity` |
| 09 | GOAP (목표 기반 자동 계획) | AI | [`AI/GOAP`](Assets/Algorithms/AI/GOAP) | [GOAP 노트](https://www.notion.so/3515e18a57368129a014df84f372de2e) | `Assets/Scenes/GOAP.unity` |
| 10 | Utility AI (점수 함수 의사결정) | AI | [`AI/UtilityAI`](Assets/Algorithms/AI/UtilityAI) | [Utility AI 노트](https://www.notion.so/3545e18a573681679f4bc7f67cdec3a1) | `Assets/Scenes/UtilityAI.unity` |
| 11 | FSM (유한 상태 기계) | AI | [`AI/FSM`](Assets/Algorithms/AI/FSM) | [FSM 노트](https://www.notion.so/3545e18a57368122a69de02130727cf1) | `Assets/Scenes/FSM.unity` |
| 12 | Quadtree (사분 트리, 2D 공간 분할) | Spatial | [`Spatial/Quadtree`](Assets/Algorithms/Spatial/Quadtree) | [Quadtree 노트](https://www.notion.so/3555e18a57368183a860d9e87c8b1b35) | `Assets/Scenes/Spatial/Quadtree.unity` |
| 13 | Spatial Hashing (균등 격자 공간 인덱싱) | Spatial | [`Spatial/SpatialHashing`](Assets/Algorithms/Spatial/SpatialHashing) | [Spatial Hashing 노트](https://www.notion.so/3555e18a573681fe9b13cd4f6938fb5c) | `Assets/Scenes/Spatial/SpatialHashing.unity` |
| 14 | AABB (충돌 검사의 원자 단위, 3 연산) | Physics | [`Physics/AABB`](Assets/Algorithms/Physics/AABB) | [AABB 노트](https://www.notion.so/3555e18a573681c9b988fcf703a37dbd) | `Assets/Scenes/Physics/AABB.unity` |
| 15 | BVH (Bounding Volume Hierarchy, 객체 기반 공간 분할) | Spatial | [`Spatial/BVH`](Assets/Algorithms/Spatial/BVH) | [BVH 노트](https://www.notion.so/3555e18a57368180a42ec03735328a38) | `Assets/Scenes/Spatial/BVH.unity` |

## 알고리즘 비교

다섯 탐색 알고리즘 (BFS, DFS, Dijkstra, A\*, Flood Fill) 은 모두 *그래프/격자 위 탐색* 카테고리.
Minimax 는 다른 카테고리(AI / 게임 트리) 지만 본질은 *DFS 의 응용* — 노드가 격자 셀 대신 게임 국면이 되고, 반환값이 경로 대신 점수로 바뀐다:

| 알고리즘 | 자료구조 | Priority / 진행 기준 | 입력 | 가중치 | 휴리스틱 |
|---------|----------|---------|------|:--:|:--:|
| **BFS** | `Queue<T>` (FIFO) | 입력 순서 (먼저 들어온 것 먼저) | `Graph<T>` | ❌ | ❌ |
| **DFS** | `Stack<T>` (LIFO) | 입력 순서 (나중에 들어온 것 먼저) | `Graph<T>` | ❌ | ❌ |
| **Dijkstra** | `MinPriorityQueue<T>` | `g(n)` — 누적 비용 | `WeightedGraph<T>` | ✅ | ❌ |
| **A\*** | `MinPriorityQueue<T>` | `g(n) + h(n)` — 누적 + 예상 | `WeightedGraph<T>` | ✅ | ✅ |
| **Flood Fill** | `Queue<T>` (FIFO) | 입력 순서 + *값 매칭* | `T[,]` (격자) | ❌ | ❌ |
| **Minimax** (AI) | 호출 스택 (재귀 DFS) | MAX/MIN 교대 (점수) | 게임 트리 (즉석 생성) | ❌ | ❌ † |
| **MCTS** (AI) | 트리 노드 + UCB1 | 통계적 샘플링 (visits/winRate) | 게임 트리 (선택적 확장) | ❌ | ❌ ‡ |
| **Behavior Tree** (AI) | 정적 트리 + 매 tick 평가 | Sequence/Selector 단락 평가 | 디자이너가 그린 트리 | ❌ | ❌ § |
| **GOAP** (AI) | `MinPriorityQueue<WorldState>` + `HashSet<WorldState>` | `f(s) = g(s) + h(s)` — A\* over **state space** | 행동 카탈로그 (Pre/Eff/Cost) | ✅ (행동 비용) | ✅ (미충족 사실 수) ¶ |
| **Utility AI** (AI) | 행동 카탈로그 (`UtilityAction[]`) | 매 tick *점수 비교* (Considerations 곱 × Weight) | 행동 + Consideration + Response Curve | ❌ (Weight 로 행동 우선순위) | ❌ (Response Curve 가 점수 곡선) ※ |
| **FSM** (AI) | 상태 + 전이 리스트 (`List<FSMState>` + `List<FSMTransition>`) | 현재 상태에서 *나가는 전이* 검사 (first match wins) | 상태 그래프 (디자이너 직접 작성) | ❌ | ❌ (전이 등록 순서가 우선순위) ★ |
| **Quadtree** (Spatial) | 재귀 트리 (`Quadtree<T>[4]` 자식, leaf 만 `List<Point>`) | 영역 *교차 검사* (`Bounds.Intersects(query)`) — 안 겹치면 자손 통째로 스킵 | 2D 점 집합 + 쿼리 영역 (AABB) | ❌ | ❌ (pruning 이 그 자리) ◇ |
| **Spatial Hashing** (Spatial) | `Dictionary<(int,int), List<Point>>` — 균등 격자 | 셀 좌표 *해시* (`Floor(x/cellSize)`) — 덮은 셀만 순회 + Contains 정밀 검사 | 2D 점 집합 + 쿼리 영역 (AABB) | ❌ | ❌ (cellSize 가 유일 튜닝) ◆ |
| **AABB** (Physics) | `readonly struct` (4 float) — 자료구조 없음, 연산이 자체 | Contains (4 비교) / Overlaps (분리 축) / Slab Raycast (슬랩 교집합) | 박스 + 점 / 다른 박스 / 광선 | ❌ | ❌ (수학 연산만) ▲ |
| **BVH** (Spatial) | 재귀 트리 (binary, leaf 만 `List<int>` 객체 인덱스) — 노드 / 객체 박스 모두 `Algorithms.Physics.AABB` *재사용* | 광선 ↔ 노드 AABB *Slab Raycast* — 안 맞으면 서브트리 통째 스킵 | 객체 (AABB+데이터) 집합 + 광선 (origin + dir) | ❌ | ❌ (median split 이 그 자리 / SAH 가 응용) ▼ |

† 평가 함수는 비-잎(non-terminal) 노드에서 탐색을 중단할 때 쓰는 휴리스틱이지만, 틱택토는 완전 탐색이 가능하므로 사용하지 않는다. 대신 **Alpha-Beta 가지치기** 로 노드 수를 O(b^d) → 최선 O(b^(d/2)) 로 줄인다.

‡ 평가 함수가 아예 *불필요* 한 것이 MCTS 의 가장 큰 차별점. 게임이 끝날 때까지 *random rollout* 을 돌려 진짜 승/패만 본다 — 도메인 지식이 없는 게임(바둑, Hex)에서 Minimax 의 대안으로 쓰인다. 시간이 많을수록 부드럽게 좋아지는 *anytime* 알고리즘.

§ BT 는 다른 알고리즘들과 결이 다르다 — *탐색* 이 아니라 *디자이너가 그린 트리를 매 tick 평가* 하는 메커니즘. 본질은 BT 가 다른 알고리즘들을 *조합* 하는 컨테이너 — 이 프로젝트의 BT 데모는 `Chase` 액션 안에서 **A\* 를 직접 호출** 해 "BT 가 *어디로* 갈지 결정 + A\* 가 *어떻게* 갈지 계산" 의 협업을 보여준다.

¶ **GOAP 는 사실상 A\* 의 한 응용** — 노드 타입이 *격자 좌표 → 세상 상태* 로 바뀌었을 뿐 자료구조와 골격이 완전히 같다. 그래서 GOAP 데모도 BT 데모처럼 `Move` 행동 *내부* 에서 격자 A\* 를 그대로 호출한다. BT × A\* 는 "디자이너가 그린 트리 위에서 결정", GOAP × A\* 는 "행동 카탈로그 위에서 자동 계획" — 같은 격자·같은 NPC 인데 *결정 메커니즘만* 바뀐다.

※ **Utility AI 는 *탐색* 이 아니다** — 사슬을 *계획* 하지 않고 *매 tick* 모든 행동의 점수를 그 자리에서 비교한다. GOAP 처럼 멀리 보지는 못하지만, 환경 변화에 *즉시 반응* 하는 게 강점 (적이 가까워지면 식사 중에도 도망). 행동 *내부* 의 이동은 BT/GOAP 와 같은 협업 패턴으로 격자 A\* 호출.

★ **FSM 은 명시성의 극단** — 디자이너가 모든 상태 / 전이 / 조건을 *직접* 그려둔다. AI 는 매 tick 현재 상태의 outgoing 전이만 검사 → 거의 공짜에 가까운 비용. 단, 상태가 많아지면 전이가 제곱으로 폭발 ("FSM 폭발 문제"). 모던 게임은 *애니메이션은 FSM (Unity Animator), 의사결정은 BT/UAI/GOAP* 로 책임을 분리.

◇ **Quadtree 는 *탐색이 아니다*** — 경로를 찾거나 노드를 순회하는 게 아니라, 2D 공간을 *재귀적으로 인덱싱* 해 "이 영역 안의 점들" 같은 *공간 쿼리* 를 가속한다. BFS 의 `visited` 집합 같은 게 없는 것은 트리에 사이클이 없기 때문 (그래프가 아니라 트리). 가족 관계로 보면 BST 의 2D 일반화 — BST 가 *값* 을 반으로 가르듯 Quadtree 는 *공간* 을 4 등분으로 가른다. 3D 로 가면 자식 4 → 8 = Octree.

◆ **Spatial Hashing 은 Quadtree 의 균등판** — 같은 카테고리 (공간 인덱싱) 지만 분할 전략이 다르다. Quadtree 는 *적응형* (점 많은 곳만 깊이 분할), Spatial Hashing 은 *균등형* (모든 셀 같은 크기). 자료구조도 트리 vs 해시맵으로 정반대. 결정적 차이는 **동적 객체** — Quadtree 는 매 프레임 재구축 비용이 크지만 Spatial Hashing 은 셀 좌표만 다시 계산하면 끝이라 슈팅 / 입자 / Boids / MMO AoI 의 사실상 표준. 가속은 *2 단계* (broad: 셀 추리기 → narrow: Contains) — 시각화의 주황(후보) ↔ 분홍(결과) 색상 차이가 곧 두 단계의 구분이다.

▲ **AABB 는 *자료구조가 아니라 연산*** — Spatial / Physics 어디서나 *원자 단위* 로 등장. Quadtree 의 `QuadtreeBounds`, Spatial Hashing 의 `SpatialHashBounds` 가 자료구조 *내부 부품* 으로 AABB 를 이미 쓰고 있지만, 이 페이지의 AABB 는 *연산이 주제* 다. 3 핵심 연산 — Contains (점이 박스 안인가, 4 비교) / Overlaps (분리 축 정리의 단순형) / Slab Raycast (광선 ↔ 박스, *각 슬랩 진입/이탈 구간의 교집합* 이 핵심) — 이 세 가지가 모든 충돌 시스템의 1 차 필터이자 narrow-phase (SAT, GJK) 의 디딤돌. 시각화는 한 화면 가로 배치된 3 데모로 마우스 한 번에 세 결과를 동시에 보여준다.

▼ **BVH 는 *공간이 아니라 객체를 분할*** — Quadtree (공간 4 등분) / Spatial Hashing (균등 격자) 와 같은 Spatial 카테고리지만 분할 전략이 정반대. 객체를 가까운 둘로 묶으므로 *형제 AABB 가 겹쳐도 됨* (= 버그가 아니라 정의). 비균일 분포 / 다양한 크기 객체에 강해 **레이트레이싱 (NVIDIA RTX / Unity DXR) / 물리 broad-phase (Dynamic AABBTree — Box2D / Bullet / PhysX) 의 사실상 표준**. AABB 와의 관계는 **AABB (원자) → BVH (분자)** — `QuadtreeBounds` / `SpatialHashBounds` 같은 자체 struct 를 만들지 *않고* `Algorithms.Physics.AABB` 를 *직접* import 해 노드 박스 / 광선 검사 (`AABB.Raycast`, Slab method) 모두 그대로 재사용. AI 카테고리가 `Search/A*` 를 부품으로 호출하는 협업 패턴과 같은 결. 시각화는 2 페이즈 (Phase 1 BFS Build 애니메이션 + Phase 2 마우스 광선) — 광선이 1 차원 직선이라 *대부분의 서브트리가 회색* 으로 가지치기되어 가속 효과가 가장 극적으로 보인다.

각 Visualizer 의 `randomSeed` 를 동일하게 맞추면 같은 좌표계에서 다섯 탐색 알고리즘의 패턴 차이가 시각적으로 드러난다 (BFS 동심원 vs DFS 뱀 vs Dijkstra 비용 등고선 vs A\* 화살표 vs Flood Fill 색 영역 채움). Minimax 는 좌표가 아닌 게임 국면 위에서 동작하므로 별도 데모 (틱택토) 로 따로 비교한다. NPC 의사결정 4 종 (BT / GOAP / Utility AI / FSM) 은 격자 + Capsule NPC + A\* 협업이라는 같은 골격을 공유하므로, 네 씬을 번갈아 켜보면 *결정 메커니즘만* 어떻게 달라지는지 한눈에 들어온다.

## 프로젝트 구조

```
Assets/
├── Algorithms/
│   ├── Common/                              # 공유 UI / 인터페이스
│   │   ├── IAlgorithmDemo.cs                #   Restart() 한 메서드 contract
│   │   └── AlgorithmDemoUI.cs               #   UI Button ↔ Visualizer 연결
│   ├── AI/                                  # AI / 의사결정 카테고리
│   │   ├── TicTacToe/                       #   Minimax + Alpha-Beta 데모 게임
│   │   │   ├── TicTacToeBoard.cs            #     게임 국면 (Make/Undo + Clone 패턴) — MCTS 와 공유
│   │   │   ├── MinimaxAlgorithm.cs          #     순수 Minimax + Alpha-Beta 로직
│   │   │   ├── TicTacToeVisualizer.cs       #     Unity 시각화 (마우스 클릭 입력)
│   │   │   └── README.md
│   │   ├── MCTS/                            #   Monte Carlo Tree Search (Minimax 의 통계적 대안)
│   │   │   ├── MCTSAlgorithm.cs             #     Searcher 클래스 + 4 단계 (Selection/Expansion/Simulation/Backprop)
│   │   │   ├── MCTSVisualizer.cs            #     Unity 시각화 (chunk 단위 점진적 visit 비율 표시)
│   │   │   └── README.md
│   │   ├── BehaviorTree/                    #   모던 NPC AI 의 표준 패턴 (Halo 이후)
│   │   │   ├── BehaviorTreeAlgorithm.cs     #     Sequence/Selector/Inverter/Condition/Action 노드
│   │   │   ├── BehaviorTreeVisualizer.cs    #     Capsule NPC + Player + 우측 OnGUI 트리 패널 + A* 협업
│   │   │   └── README.md
│   │   ├── GOAP/                            #   목표 기반 자동 계획 — F.E.A.R. 류 자율 NPC AI
│   │   │   ├── GOAPAlgorithm.cs             #     WorldState + GOAPAction + GOAPPlanner (state space A*)
│   │   │   ├── GOAPGridVisualizer.cs        #     5거점 격자 + Capsule NPC + 우측 OnGUI 패널 + A* 협업
│   │   │   └── README.md
│   │   ├── UtilityAI/                       #   점수 함수 의사결정 — The Sims / RimWorld 류 욕구 NPC
│   │   │   ├── UtilityAIAlgorithm.cs        #     Consideration + ResponseCurve + UtilityAction + UtilityAIBrain
│   │   │   ├── UtilityAIGridVisualizer.cs   #     4거점 격자 + Capsule NPC + 적 + 욕구 바·점수 막대 + A* 협업
│   │   │   └── README.md
│   │   └── FSM/                             #   유한 상태 기계 — 클래식 NPC / Animator 의 표준 패턴
│   │       ├── FSMAlgorithm.cs              #     FSMState + FSMTransition + FSMachine (first-match-wins)
│   │       ├── FSMGridVisualizer.cs         #     5상태 + 7전이 + 시야 셀 + Player(WASD) + 상태 다이어그램
│   │       └── README.md
│   ├── Spatial/                             # 공간 분할 카테고리 (broad-phase)
│   │   ├── Quadtree/                        #   2D 공간 4 분할 인덱싱 — 충돌 / 근접 검색 가속
│   │   │   ├── QuadtreeAlgorithm.cs         #     QuadtreeBounds + QuadtreePoint + Quadtree<T> + BruteForceQuery
│   │   │   ├── QuadtreeVisualizer.cs        #     2 페이즈 (삽입 애니메이션 + 마우스 쿼리) + LineRenderer 사각형 + OnGUI 카운터
│   │   │   └── README.md
│   │   ├── SpatialHashing/                  #   균등 격자 + Dictionary 기반 인덱싱 — 동적 객체에 강함
│   │   │   ├── SpatialHashAlgorithm.cs      #     SpatialHashBounds + SpatialHashPoint + SpatialHash<T> + BruteForceQuery
│   │   │   ├── SpatialHashVisualizer.cs     #     2 페이즈 (격자 + 점 삽입 / 마우스 쿼리) + 점 색상 3종 (idle/candidate/result)
│   │   │   └── README.md
│   │   └── BVH/                             #   객체 기반 binary 트리 — 레이트레이싱 / 물리 broad-phase 표준
│   │       ├── BVHAlgorithm.cs              #     BVHObject + BVHNode + BVH<T> (top-down longest-axis median split + Raycast) + BruteForceRaycast
│   │       │                                #     ※ Algorithms.Physics.AABB *재사용* — 노드/객체 박스 + Slab Raycast
│   │       ├── BVHVisualizer.cs             #     2 페이즈 (BFS Build 애니메이션 / 마우스 광선) + LineRenderer 외곽선 + OnGUI 카운터
│   │       └── README.md
│   ├── Physics/                             # 물리 / 충돌 카테고리 (narrow-phase + 시뮬)
│   │   └── AABB/                            #   축에 평행한 경계 박스 — 충돌 검사의 원자 단위
│   │       ├── AABBAlgorithm.cs             #     AABB struct + Contains + Overlaps + GetSeparatingAxis + Raycast(Slab) + AABBRaycastResult
│   │       ├── AABBVisualizer.cs            #     3 데모 한 화면 가로 배치 (Contains / Overlap / Slab Raycast) + OnGUI t 값 + 부등식
│   │       └── README.md
│   └── Search/                              # 탐색 카테고리
│       ├── Graph.cs                         #   비가중 인접 리스트 (BFS / DFS)
│       ├── WeightedGraph.cs                 #   가중치 인접 리스트 (Dijkstra / A*)
│       ├── MinPriorityQueue.cs              #   이진 힙 우선순위 큐 (Dijkstra / A*)
│       ├── BFS/
│       │   ├── BFSAlgorithm.cs
│       │   ├── BFSGridVisualizer.cs
│       │   └── README.md
│       ├── DFS/
│       │   ├── DFSAlgorithm.cs
│       │   ├── DFSGridVisualizer.cs
│       │   └── README.md
│       ├── Dijkstra/
│       │   ├── DijkstraAlgorithm.cs
│       │   ├── DijkstraGridVisualizer.cs
│       │   └── README.md
│       ├── AStar/
│       │   ├── AStarAlgorithm.cs
│       │   ├── AStarGridVisualizer.cs
│       │   └── README.md
│       └── FloodFill/                       # 그래프 의존 없음 — 격자 자체가 자료구조
│           ├── FloodFillAlgorithm.cs
│           ├── FloodFillGridVisualizer.cs
│           └── README.md
└── Scenes/
    ├── BFS.unity
    ├── DFS.unity
    ├── Dijkstra.unity
    ├── AStar.unity
    ├── Flood_Fill.unity
    ├── Minimax.unity
    ├── MCTS.unity
    ├── BehaviorTree.unity
    ├── GOAP.unity
    ├── UtilityAI.unity
    └── FSM.unity
```

## 콘텐츠 분리 원칙

같은 내용을 두 곳에 복제하지 않는다.

| 채널 | 권위 있는 콘텐츠 |
|------|-----------------|
| **GitHub** | 코드 본체, 코드 안 한국어 주석, 알고리즘별 짧은 README |
| **Notion** | 다이어그램, 비교표, 학습 순서, 알고리즘 간 크로스 링크 |

양쪽은 서로를 **링크로** 가리킨다 (복제가 아니라 참조).

## AI 코딩 도구용 워크플로 문서

새 알고리즘을 일관된 형식으로 추가하기 위한 절차를 [`CLAUDE.md`](CLAUDE.md) 에 정리해 두었다. Claude Code 같은 AI 코딩 도구가 새 세션에서도 같은 컨벤션을 자동으로 따른다.

## 작성자

[YongHeeHee](https://github.com/YongHeeHee)
