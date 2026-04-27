# CLAUDE.md

이 파일은 Claude Code (또는 다른 AI 코딩 도구) 가 이 프로젝트에서 작업할 때 자동으로 읽어 들이는 워크플로 문서다. 새 세션에서도 같은 컨벤션으로 작업이 이어지도록 핵심 규칙과 절차를 여기에 명시해 둔다.

---

## 1. 프로젝트 개요

Unity 기반 게임 알고리즘 학습 프로젝트. 각 알고리즘을 Unity Editor 에서 시각적으로 확인할 수 있는 데모와 함께 구현하는 **모노레포** (단일 GitHub repo).

### 핵심 원칙

1. **학습 우선** — 각 알고리즘은 한 파일을 처음부터 끝까지 읽으면 이해되도록 작성. 추상 베이스 클래스/프레임워크로 메커니즘을 가리지 않는다.
2. **시각화 필수** — 알고리즘마다 Unity Scene 에서 동작 패턴을 눈으로 볼 수 있는 Visualizer 를 함께 만든다.
3. **한국어 우선 문서화** — 헤더 주석, 라인 주석, README, Notion 모두 한국어로 풍부하게. (이는 기본 "최소 주석" 규칙을 덮어쓰는 프로젝트 컨벤션)

---

## 2. 폴더 구조

```
Assets/
├── Algorithms/
│   ├── Common/                          ← 공유 유틸리티 (UI, 인터페이스)
│   │   ├── IAlgorithmDemo.cs            ← 모든 Visualizer 가 구현해야 하는 contract
│   │   └── AlgorithmDemoUI.cs           ← Restart 버튼 ↔ Visualizer 를 잇는 다리
│   └── Search/                          ← 알고리즘 카테고리
│       ├── Graph.cs                     ← BFS/DFS/Dijkstra/A* 가 공유하는 인접 리스트
│       ├── BFS/
│       │   ├── BFSAlgorithm.cs
│       │   ├── BFSGridVisualizer.cs
│       │   └── README.md
│       └── DFS/
│           ├── DFSAlgorithm.cs
│           ├── DFSGridVisualizer.cs
│           └── README.md
└── Scenes/
    ├── BFS.unity                        ← 알고리즘마다 씬 분리
    └── DFS.unity
```

> 향후 Pathfinding (Dijkstra, A\*), Sorting, Procedural 등 새 카테고리는 `Assets/Algorithms/<Category>/` 로 추가.

---

## 3. 새 알고리즘 추가 워크플로

새 알고리즘 `X` 를 추가할 때 반드시 다음 순서를 따른다. 각 단계가 빠지면 인덱스/링크가 깨진다.

### 3.1 코드

`Assets/Algorithms/<Category>/X/` 폴더에 다음 파일들을 만든다.

#### `XAlgorithm.cs` — 순수 알고리즘 (Unity 의존 없음)

- 네임스페이스는 카테고리 단위 (예: `Algorithms.Search`)
- 공유 자료구조 (`Graph<T>` 등) 는 카테고리 루트의 파일을 그대로 사용 (재구현 X)
- 클래스 헤더 주석에 5섹션 필수:
  1. 알고리즘 개요
  2. 동작 흐름 (단계별 번호 — `①②③` 또는 `[1][2][3]`)
  3. 시간 / 공간 복잡도 (V, E 등 변수 명시)
  4. 게임에서의 사용처
  5. 사용 자료구조와 선택 이유 (왜 Queue/Stack/HashSet 인가)
- 라인 주석에는 헤더의 "동작 흐름" 단계 번호와 1:1 매핑되는 마커 (`[6-1]`, `[6-2]` 등)

#### `XGridVisualizer.cs` (또는 적절한 이름) — Unity MonoBehaviour

- `using Algorithms.Common;` 추가
- 클래스 선언: `public class XGridVisualizer : MonoBehaviour, IAlgorithmDemo`
- `Start()` 은 `Restart()` 만 호출 (첫 실행 = 재시작과 동일 흐름)
- `Restart()` 안에서:
  1. `StopAllCoroutines()`
  2. 자식 GameObject 모두 `Destroy`
  3. 내부 `Dictionary` / `HashSet` 등 `Clear`
  4. 그리드/그래프 재구성
  5. 코루틴 재시작
- 색상은 BFS/DFS 와 일관되게 유지:
  - 회색 = 미방문, 노랑 = Frontier, 파랑 = 방문 완료, 초록 = 시작, 빨강 = 목표, 분홍 = 경로, 검정 = 벽

#### `README.md` — BFS/DFS README 와 동일 구조

- 한 줄 요약
- 파일 구성 표 (공유 의존성 명시)
- 학습 노트 (Notion 실제 URL — placeholder 금지)
- 코드 안 학습 주석 안내
- 빠른 사용법 (Scene 열기 → GameObject 생성 → 컴포넌트 부착 → Play)
- 색상 의미
- 변형 실험 (인스펙터에서 바꿔볼 거리, "이 알고리즘이 ~~ 로 바뀌면 어떻게 될까" 류)

### 3.2 Scene

- `Assets/Scenes/X.unity` 새로 생성 (`BFS.unity` 를 Duplicate 후 컴포넌트 교체가 가장 빠름)
- 씬 안에:
  - 빈 GameObject + `XGridVisualizer` 컴포넌트
  - Canvas + Button (Restart 용)
  - 빈 GameObject + `AlgorithmDemoUI` 컴포넌트 (Inspector 에서 Button 슬롯과 Visualizer 슬롯 연결)
- Build Settings → Scenes In Build 에 등록

### 3.3 Notion (반드시 이 순서로!)

순서가 바뀌면 허브 인덱스가 깨진다.

1. **새 페이지 생성** — 부모는 반드시 `알고리즘 정리` 허브
   - 허브 page id: `34f5e18a-5736-819e-9a6b-c804cbe12a42`
   - 허브 URL: https://www.notion.so/34f5e18a5736819e9a6bc804cbe12a42
2. **페이지 구조** — BFS/DFS 페이지와 같은 토글 구조 (보통 9~10 개):
   1. 개념 / 정의
   2. 동작 흐름 (Mermaid 다이어그램 권장)
   3. 시간 / 공간 복잡도
   4. 게임에서의 사용처
   5. 사용 자료구조와 선택 이유
   6. 코드 구조 (파일 표 + 의존성 다이어그램)
   7. 핵심 코드 (csharp 코드블록)
   8. 응용 (FindPath 등 변형)
   9. Unity 시각화 데모 (사용법 + 색상표 + Restart 버튼 셋업)
3. **허브 인덱스 갱신** — `정리된 알고리즘` 섹션 카테고리 불릿 아래에 추가:
   ```
   - **Search (탐색)**
     - <mention-page url="...">X — 풀네임</mention-page>
   ```
4. **TODO 체크** — `추가 예정 (TODO)` 섹션의 X 항목을 `- [x]` 로 체크
5. **양방향 링크** — README 에는 Notion URL, Notion 페이지에는 GitHub 폴더/파일 딥링크 (예: `.../Assets/Algorithms/Search/X/XAlgorithm.cs#L75-L139`)

> **Notion 마크다운 문법** : `notion://docs/enhanced-markdown-spec` MCP 리소스로 읽을 수 있음. 토글은 `<details><summary>...</summary>` 형식, 자식은 **탭 들여쓰기** 필수. 코드블록 내부는 백슬래시 이스케이프 금지.

### 3.4 프로젝트 루트 README.md (반드시 갱신)

새 알고리즘을 추가할 때마다 루트 [`README.md`](README.md) 도 같이 갱신한다. 다음 4 곳 모두 손봐야 일관성이 유지된다:

1. **알고리즘 인덱스 표** — 새 행 추가 (`#`, 알고리즘 이름, 카테고리, 코드 폴더 링크, Notion URL, 씬 경로)
2. **알고리즘 가족 관계 표** — 새 알고리즘이 BFS/DFS/Dijkstra/A\* 가족과 어떤 관계인지 추가 (자료구조 / Priority / 가중치 / 휴리스틱 사용 여부)
3. **프로젝트 구조 트리** — 새 폴더 + 파일 추가
4. **공유 의존성** — 새 자료구조를 만들었다면 (`Search/Foo.cs` 같은 공유 파일) 트리에 함께 표시

이 단계가 빠지면 신규 방문자가 어떤 알고리즘이 있는지 한눈에 파악하지 못한다.

---

## 4. 공유 컴포넌트

| 이름 | 위치 | 역할 |
|------|------|------|
| `Algorithms.Search.Graph<T>` | `Assets/Algorithms/Search/Graph.cs` | 인접 리스트 그래프. 모든 탐색 알고리즘이 공유 |
| `Algorithms.Common.IAlgorithmDemo` | `Assets/Algorithms/Common/IAlgorithmDemo.cs` | `Restart()` 한 메서드짜리 contract |
| `Algorithms.Common.AlgorithmDemoUI` | `Assets/Algorithms/Common/AlgorithmDemoUI.cs` | UI Button 과 `IAlgorithmDemo` 를 Inspector 에서 연결 |

---

## 5. 주의사항

- **추상 베이스 클래스 만들지 말 것** — 알고리즘 메커니즘이 가려져 학습 목적에 어긋남. 인터페이스 (로직 없음) 는 OK.
- **모노레포 유지** — 알고리즘이 늘어나도 별도 repo 로 쪼개지 않는다. Unity 프로젝트 무게는 binary asset 때문이지 C# 스크립트 때문이 아님.
- **씬 분리 유지** — 알고리즘마다 한 씬. 메뉴/허브 씬은 학습 단계에선 불필요.
- **Graph.cs 위치 보존** — `Assets/Algorithms/Search/Graph.cs` 에 그대로 둘 것. BFS/DFS 폴더로 다시 옮기지 말기.
- **Notion 페이지 삭제 불가** — Notion MCP API 는 페이지 trash 기능을 노출하지 않음. 삭제는 사용자가 UI 에서 수동 처리.
- **README 의 Notion 링크는 placeholder 금지** — 페이지 생성 직후 실제 URL 로 교체.

---

## 6. 외부 참조

| 자료 | URL |
|------|-----|
| 📚 알고리즘 정리 (Notion 허브) | https://www.notion.so/34f5e18a5736819e9a6bc804cbe12a42 |
| 🔎 BFS Notion 페이지 | https://www.notion.so/34f5e18a57368191994ed675ceb4f846 |
| 🌲 DFS Notion 페이지 | https://www.notion.so/34f5e18a573681a0b707c342c39148da |

| Notion 페이지 ID (도구 호출용) | UUID |
|------|------|
| 알고리즘 정리 허브 | `34f5e18a-5736-819e-9a6b-c804cbe12a42` |
| BFS | `34f5e18a-5736-8191-994e-d675ceb4f846` |
| DFS | `34f5e18a-5736-81a0-b707-c342c39148da` |
