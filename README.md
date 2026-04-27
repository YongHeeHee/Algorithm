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
2. `Assets/Scenes/SampleScene.unity` 열기
3. 빈 GameObject 생성 → 원하는 알고리즘의 Visualizer 컴포넌트 부착
4. (선택) 카메라를 위에서 내려다보도록 배치 (예: position `(6, 15, 6)`, rotation `(90, 0, 0)`)
5. Play

## 학습 노트 (Notion)

알고리즘 개요, 다이어그램, 비교표, 학습 순서 같은 시각 자료는 Notion 학습 허브에 정리되어 있다.

> **Notion 학습 허브**: [[Algorithm Study — Notion](https://www.notion.so/algorithm-study-NOTION_URL_HERE)](https://www.notion.so/34f5e18a5736819e9a6bc804cbe12a42)
>
> *위 링크는 placeholder. Notion 페이지 작성 후 실제 공개 URL로 교체 필요.*

## 알고리즘 인덱스

| # | 알고리즘 | 카테고리 | 코드 | Notion 노트 | 데모 |
|---|----------|----------|------|-------------|------|
| 01 | BFS (Breadth-First Search) | Search | [`Assets/Algorithms/Search/BFS`](Assets/Algorithms/Search/BFS) | [BFS 노트](https://www.notion.so/bfs-NOTION_URL_HERE) | 추가 예정 |

## 프로젝트 구조

```
Assets/
├── Algorithms/
│   └── Search/
│       └── BFS/
│           ├── Graph.cs              # 인접 리스트 그래프 (Dictionary<T, List<T>>)
│           ├── BFSAlgorithm.cs       # 순수 BFS 로직 (Search / FindPath)
│           ├── BFSGridVisualizer.cs  # Unity 그리드 시각화 (코루틴)
│           └── README.md             # BFS 폴더 안내
└── Scenes/
    └── SampleScene.unity
```

## 콘텐츠 분리 원칙

같은 내용을 두 곳에 복제하지 않는다.

| 채널 | 권위 있는 콘텐츠 |
|------|-----------------|
| **GitHub** | 코드 본체, 코드 안 한국어 주석, 알고리즘별 짧은 README |
| **Notion** | 다이어그램, 비교표, 학습 순서, 알고리즘 간 크로스 링크 |

양쪽은 서로를 **링크로** 가리킨다 (복제가 아니라 참조).

## 작성자

[YongHeeHee](https://github.com/YongHeeHee)
