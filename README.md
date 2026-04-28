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
2. `Assets/Scenes/` 의 알고리즘별 씬을 연다 (예: `BFS.unity`, `DFS.unity`, `Dijkstra.unity`, `AStar.unity`, `Flood_Fill.unity`, `Minimax.unity`)
3. 씬 안에 Visualizer 가 붙은 GameObject 가 없으면:
   - 빈 GameObject 생성 → 원하는 알고리즘의 Visualizer 컴포넌트 부착
4. 카메라 배치 권장:
   - BFS / DFS / Flood Fill / Minimax : 위에서 내려다보는 각도 — 그리드/보드가 평면
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

† 평가 함수는 비-잎(non-terminal) 노드에서 탐색을 중단할 때 쓰는 휴리스틱이지만, 틱택토는 완전 탐색이 가능하므로 사용하지 않는다. 대신 **Alpha-Beta 가지치기** 로 노드 수를 O(b^d) → 최선 O(b^(d/2)) 로 줄인다.

각 Visualizer 의 `randomSeed` 를 동일하게 맞추면 같은 좌표계에서 다섯 탐색 알고리즘의 패턴 차이가 시각적으로 드러난다 (BFS 동심원 vs DFS 뱀 vs Dijkstra 비용 등고선 vs A\* 화살표 vs Flood Fill 색 영역 채움). Minimax 는 좌표가 아닌 게임 국면 위에서 동작하므로 별도 데모 (틱택토) 로 따로 비교한다.

## 프로젝트 구조

```
Assets/
├── Algorithms/
│   ├── Common/                              # 공유 UI / 인터페이스
│   │   ├── IAlgorithmDemo.cs                #   Restart() 한 메서드 contract
│   │   └── AlgorithmDemoUI.cs               #   UI Button ↔ Visualizer 연결
│   ├── AI/                                  # AI / 의사결정 카테고리
│   │   └── TicTacToe/                       #   Minimax + Alpha-Beta 데모 게임
│   │       ├── TicTacToeBoard.cs            #     게임 국면 (Make/Undo 패턴)
│   │       ├── MinimaxAlgorithm.cs          #     순수 Minimax + Alpha-Beta 로직
│   │       ├── TicTacToeVisualizer.cs       #     Unity 시각화 (마우스 클릭 입력)
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
    └── Minimax.unity
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
