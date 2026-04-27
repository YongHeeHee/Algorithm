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
2. `Assets/Scenes/` 의 알고리즘별 씬을 연다 (예: `BFS.unity`, `DFS.unity`, `Dijkstra.unity`, `AStar.unity`)
3. 씬 안에 Visualizer 가 붙은 GameObject 가 없으면:
   - 빈 GameObject 생성 → 원하는 알고리즘의 Visualizer 컴포넌트 부착
4. 카메라 배치 권장:
   - BFS / DFS : 위에서 내려다보는 각도 — 그리드가 평면
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

## 알고리즘 비교

네 알고리즘이 *우선순위 큐의 priority 기준* 만 다른 동일 골격이라는 점이 코드로 확인된다:

| 알고리즘 | 자료구조 | Priority | 가중치 사용 | 휴리스틱 사용 |
|---------|----------|---------|:--------:|:----------:|
| **BFS** | `Queue<T>` (FIFO) | 입력 순서 (먼저 들어온 것 먼저) | ❌ | ❌ |
| **DFS** | `Stack<T>` (LIFO) | 입력 순서 (나중에 들어온 것 먼저) | ❌ | ❌ |
| **Dijkstra** | `MinPriorityQueue<T>` | `g(n)` — 누적 비용 | ✅ | ❌ |
| **A\*** | `MinPriorityQueue<T>` | `g(n) + h(n)` — 누적 + 예상 | ✅ | ✅ |

각 Visualizer 의 `randomSeed` 를 동일하게 맞추면 같은 미로에서 네 알고리즘의 탐색 패턴 차이가 시각적으로 드러난다 (BFS 동심원 vs DFS 뱀 vs Dijkstra 비용 등고선 vs A\* 화살표).

## 프로젝트 구조

```
Assets/
├── Algorithms/
│   ├── Common/                              # 공유 UI / 인터페이스
│   │   ├── IAlgorithmDemo.cs                #   Restart() 한 메서드 contract
│   │   └── AlgorithmDemoUI.cs               #   UI Button ↔ Visualizer 연결
│   └── Search/                              # 알고리즘 카테고리
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
│       └── AStar/
│           ├── AStarAlgorithm.cs
│           ├── AStarGridVisualizer.cs
│           └── README.md
└── Scenes/
    ├── BFS.unity
    ├── DFS.unity
    ├── Dijkstra.unity
    └── AStar.unity
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
