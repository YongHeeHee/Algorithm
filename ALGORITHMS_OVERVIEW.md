# 게임 개발 알고리즘 카테고리 개요

이 프로젝트에 구현된 알고리즘은 **탐색(Search)** 카테고리에 속한다. 게임 개발에는 그 외에도 다양한 카테고리의 알고리즘이 자주 쓰인다. 이 문서는 학습 로드맵 / 다음 구현 후보를 잡기 위한 *카테고리 개요* 다.

> 이 프로젝트의 *구현 현황* 은 [`README.md`](README.md), 새 알고리즘 추가 워크플로는 [`CLAUDE.md`](CLAUDE.md) 참고.

---

## 🔎 탐색 / 경로 (Search / Pathfinding)

> **이 프로젝트에서 학습 완료** : BFS, DFS, Dijkstra, A\*, Flood Fill

| 알고리즘 | 설명 | 게임 예시 |
|---------|------|-----------|
| **JPS+ (Jump Point Search)** | A\* 의 그리드 전용 가속 — 큰 빈 공간을 점프해 탐색 노드 수 대폭 감소 | RTS 의 대규모 유닛 길찾기 |
| **Theta\*** | A\* 가 만든 격자 경로를 *직선화* — any-angle pathfinding | 자연스러운 NPC 이동 |
| **D\* Lite** | 동적 환경 (장애물이 움직임) 에서 효율적 재계산 | StarCraft, 미로 변경 퍼즐 |

---

## 🌍 절차적 생성 (Procedural Generation)

랜덤 콘텐츠 자동 생성. *지형 / 던전 / 레벨* 무한 생성에 핵심.

| 알고리즘 | 설명 | 게임 예시 |
|---------|------|-----------|
| **Perlin / Simplex Noise** | 자연스러운 연속적 랜덤 — 지형 높이맵, 구름, 동굴 | Minecraft 의 지형 |
| **Cellular Automata** | 생명게임류 규칙 반복 — 동굴, 자연스러운 지형 | Terraria 의 동굴 생성 |
| **Wave Function Collapse** | 타일 패턴의 제약 만족 → 일관된 무작위 맵 | Townscaper, Caves of Qud |
| **Recursive Backtracking** | DFS 로 미로 만들기 — 완벽한 미로 보장 | 던전 크롤러류 |
| **Poisson Disk Sampling** | 균등 분포로 점 찍기 (뭉치지 않음) | 나무/풀 자연스럽게 배치 |

---

## 🧠 AI / 의사결정 (Decision Making)

NPC 행동, 적 AI, 보드게임 봇.

> **이 프로젝트에서 학습 완료** : Minimax + Alpha-Beta (틱택토)

| 알고리즘 | 설명 | 게임 예시 |
|---------|------|-----------|
| **MCTS (Monte Carlo Tree Search)** | 시뮬레이션으로 유망한 수 선택 — Minimax 대안 | 바둑(AlphaGo), 보드게임 봇 |
| **Behavior Tree** | 행동을 트리로 — Selector/Sequence/조건 노드 | Halo, 대부분 모던 AAA NPC |
| **FSM (Finite State Machine)** | 상태 + 전이 규칙 — Idle/Patrol/Chase | 클래식 NPC, 레트로 게임 |
| **GOAP (Goal-Oriented Action Planning)** | 목표 → 필요 액션 계획 (A\* 변형) | F.E.A.R., S.T.A.L.K.E.R. |
| **Utility AI** | 점수 함수로 행동 선택 — 직관적 튜닝 | The Sims, RimWorld |

---

## 💥 물리 / 공간 분할 (Physics / Spatial Partitioning)

충돌 검사, 근처 객체 찾기.

| 알고리즘 | 설명 | 게임 예시 |
|---------|------|-----------|
| **Quadtree / Octree** | 공간을 4 / 8 분할 재귀 — 충돌/렌더 가속 | 2D/3D 충돌 광역 검사 |
| **Spatial Hashing** | 그리드 셀에 객체 매핑 — 균등 분포에 빠름 | 슈팅 게임의 탄막 |
| **BVH (Bounding Volume Hierarchy)** | 바운딩 박스 트리 — 레이트레이싱 표준 | Unity Physics, 모던 GPU 레이트레이싱 |
| **SAT (Separating Axis Theorem)** | 볼록 다각형 충돌 판정 | 2D 박스/폴리곤 충돌 |
| **GJK** | 임의 볼록 도형 충돌 (3D) | Unity/Bullet 물리 엔진 내부 |
| **Verlet Integration** | 위치 기반 물리 — 안정적 시뮬레이션 | 천 시뮬레이션, 로프, 헤어 |

---

## 🎬 애니메이션 / 보간 (Animation / Interpolation)

부드러운 움직임의 수학.

| 알고리즘 | 설명 | 게임 예시 |
|---------|------|-----------|
| **CCD / FABRIK (IK)** | 역기구학 — 손/발 끝점 → 관절 각도 역산 | 캐릭터가 정확히 손잡이 잡기 |
| **Bezier / Catmull-Rom Spline** | 제어점으로 부드러운 곡선 | 카메라 돌리, 미사일 궤적 |
| **Slerp (Spherical Linear Interp)** | 쿼터니언 회전 보간 — 균일 각속도 | 캐릭터 회전, 카메라 회전 |
| **Easing Functions** | ease-in/out 등 가속도 곡선 | UI 트윈, 점프 곡선 |
| **State Blending** | 애니메이션 클립 가중치 혼합 | 걷기 → 뛰기 자연스러운 전환 |

---

## 📊 정렬 / 데이터 관리

게임 안에서 정렬은 *순위표* 만이 아니라 *렌더링 순서* / *의존 해결* 에 자주 등장.

| 알고리즘 | 설명 | 게임 예시 |
|---------|------|-----------|
| **Topological Sort** | DAG 위상 정렬 (DFS 기반) | 빌드 순서, 퀘스트/스킬 트리 잠금 해제 |
| **Radix Sort** | 자릿수 기반 O(n) 정렬 — 키가 정수일 때 | 렌더 큐 정렬 (depth, material 키) |
| **Heap / Priority Queue** | 최소/최대 자동 — 이벤트 스케줄링 | 시간 기반 이벤트, A\* 의 PQ |

---

## 🎨 그래픽 / 렌더링

| 알고리즘 | 설명 | 게임 예시 |
|---------|------|-----------|
| **Frustum Culling** | 카메라에 안 보이는 객체 렌더 제외 | 모든 3D 게임의 기본 |
| **Occlusion Culling** | 다른 물체 뒤에 가려진 거 제외 | Unity Occlusion Culling, AAA |
| **Marching Cubes / Squares** | 등치면(isosurface) 추출 — 부드러운 메시 | Minecraft 의 부드러운 변형 모드, 메탈볼 |
| **Bresenham's Line** | 정수 연산만으로 픽셀 라인 | 레트로 픽셀아트, 라인 오브 사이트 |

---

## 📐 기하학 (Computational Geometry)

| 알고리즘 | 설명 | 게임 예시 |
|---------|------|-----------|
| **Convex Hull (Graham scan / QuickHull)** | 점들의 볼록 외곽 | 충돌 영역 단순화 |
| **Delaunay Triangulation** | 점 집합의 최적 삼각화 | 지형 메시 생성, NavMesh |
| **Voronoi Diagram** | 가장 가까운 점 영역 분할 | 바이옴 / 영토 생성 |

---

## 🌐 네트워크 (Multiplayer)

| 기법 | 설명 | 게임 예시 |
|------|------|-----------|
| **Client-Side Prediction** | 클라이언트가 결과 예측 → 서버 검증 | FPS (오버워치, 발로란트) |
| **Lag Compensation** | 서버가 과거 시점에서 히트 검사 | Counter-Strike 의 "내 화면엔 맞았는데..." 해결 |
| **Lockstep** | 모든 클라이언트가 같은 결정론적 시뮬레이션 | RTS (StarCraft 1) |
| **Snapshot Interpolation** | 과거 상태들 사이 보간 표시 | Quake 시리즈 |

---

## 🗺️ 학습 우선순위 추천

이 프로젝트의 다음 학습 흐름과 게임 활용도를 고려한 권장 순서:

1. ~~**Flood Fill**~~ — *완료* (`Assets/Algorithms/Search/FloodFill`)
2. ~~**Minimax + Alpha-Beta**~~ — *완료* (`Assets/Algorithms/AI/TicTacToe`)
3. **Perlin Noise** — 절차적 생성 입문. 즉시 시각적 결과가 인상적.
4. **Quadtree / Spatial Hashing** — 게임 성능 최적화의 핵심.
5. **Behavior Tree** *또는* **FSM** — AI 입문 (둘 중 하나로 시작).
6. **Cellular Automata** — 동굴 생성 데모. 비주얼 임팩트 큼.
7. **A\* 변형 (JPS+ 또는 Theta\*)** — 이미 구현한 A\* 의 확장.
8. **MCTS** — Minimax 의 대안. 분기가 폭발하는 게임에 적용 (바둑 AlphaGo 의 핵심).

가장 *게임답고 시각적인 데모* 가 나오는 건 **Perlin Noise** + **Cellular Automata** 조합. Unity 그리드 위에 즉시 인상적인 결과가 나와서 다음 학습으로 동기 부여가 잘 된다.

---

## 더 깊이 학습하고 싶을 때 참고

- **Game Programming Patterns** (Robert Nystrom) — 패턴 중심이지만 알고리즘 활용도 같이 다룸 (한국어 번역 있음)
- **Red Blob Games** (https://www.redblobgames.com) — 알고리즘 시각화 끝판왕. 특히 A\* / 헥스 그리드 / 자동 생성
- **Game AI Pro** 시리즈 — AAA 스튜디오의 실제 사용 사례 모음
- Catlike Coding — Unity 기반 절차적 생성 / 셰이더 튜토리얼
