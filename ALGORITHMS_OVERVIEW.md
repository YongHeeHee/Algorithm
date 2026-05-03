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

> **이 프로젝트에서 학습 완료** : Minimax + Alpha-Beta (틱택토), MCTS (틱택토), Behavior Tree (격자 NPC + A\* 협업), GOAP (격자 NPC + A\* 협업), Utility AI (격자 NPC + 욕구 바 + A\* 협업), FSM (격자 NPC + Player + 상태 다이어그램 + A\* 협업)
>
> **🗺️ AI 카테고리는 여기서 일단 휴식.** 대중적인 의사결정 6 종을 모두 다뤘으므로, 다른 카테고리 (Procedural Generation, Spatial Partitioning 등) 를 학습한 뒤 돌아온다. 그때 *다음에 무엇을 할지* 의 지도가 Notion 에 정리되어 있다 → [**AI 의사결정 — 추가로 알면 좋은 알고리즘 (TODO)**](https://www.notion.so/3545e18a573681eca7d5e84f7a35f372). Steering Behaviors / HTN / Influence Maps / Q-Learning / HFSM 의 학습 가치, 시각화 아이디어, 우선순위가 적혀 있다.

| 알고리즘 | 설명 | 게임 예시 |
|---------|------|-----------|
| **Behavior Tree** ✅ | 트리 구조의 의사결정 (Sequence/Selector 단락 평가) | 모던 AAA NPC 의 사실상 표준 |
| **GOAP** ✅ | 목표 → 필요 액션 계획 (A\* 변형 — state space 위) | F.E.A.R., S.T.A.L.K.E.R. |
| **Utility AI** ✅ | 점수 함수로 행동 선택 — 직관적 튜닝 | The Sims, RimWorld |
| **FSM (Finite State Machine)** ✅ | 상태 + 전이 규칙 — Idle/Patrol/Chase | 클래식 NPC, 레트로 게임, Animator |

---

## 🌳 공간 분할 (Spatial Partitioning)

> **이 프로젝트에서 학습 완료** : Quadtree, Spatial Hashing

*"객체를 어디에 보관할까"* — 객체 위치를 인덱싱해 공간 쿼리 (이 영역 안의 객체 찾기 / 근처 K 개 찾기) 를 가속하는 자료구조 카테고리. 두 자료구조 모두 broad-phase (충돌 후보 추리기) 의 핵심.

| 알고리즘 | 설명 | 게임 예시 |
|---------|------|-----------|
| **Quadtree / Octree** ✅ | 공간을 4 / 8 분할 재귀 — *적응적* 분할 | 2D/3D 충돌 광역 검사, 분포 편향에 강함 |
| **Spatial Hashing** ✅ | 균등 격자 셀에 객체 매핑 — *동적 객체* 에 압도적 | 슈팅 게임의 탄막, MMO AoI, Boids |
| **BVH (Bounding Volume Hierarchy)** | 바운딩 박스 *트리* — 공간이 아닌 *객체* 를 트리 구조로 묶음 | Unity Physics 내부, 모던 GPU 레이트레이싱, 동적 객체에 강함 |
| **k-d tree** | 데이터 균등 분할 (median 점에서 가름) — Quadtree 의 편향 분포 대안 | Nearest Neighbor 검색, NavMesh 근접 쿼리, ML 의 KNN |
| **Sweep and Prune (SAP)** | 한 축으로 정렬 후 겹치는 구간만 검사 | Box2D 의 broad-phase, 객체 *대부분 정적* 인 환경 |

### 학습 추천 다음 알고리즘

1. **BVH** — Quadtree 와의 직접 비교 (공간 분할 vs 객체 분할). 동적 객체 + frustum culling 시각화로 인상적.
2. (선택) **k-d tree** — 점이 한쪽에 몰린 *편향 분포* 데이터에서 Quadtree 와 비교하면 적응성 차이가 시각적으로 드러남.

> Spatial Hashing 까지 마쳤으면 이 카테고리의 *균등 vs 적응* 두 축은 다뤘다. BVH 가 다음 자연스러운 단계 — *공간이 아닌 객체* 를 트리화하는 세 번째 축.

---

## 💥 물리 / 충돌 (Physics / Collision)

> **이 프로젝트에서 학습 완료** : AABB (Contains / Overlaps / Slab Raycast)

*"두 도형이 겹치는가, 어떻게 움직이는가"* — 공간 분할이 broad-phase 였다면 이쪽은 narrow-phase (정밀 판정) + 시뮬레이션 (위치/속도/힘 갱신). 수학·역학 사고 모델.

| 알고리즘 | 설명 | 게임 예시 |
|---------|------|-----------|
| **AABB Overlap + Raycast (Slab)** ✅ | 축에 정렬된 박스의 겹침/광선 충돌 — 모든 충돌 시스템의 *원자 단위* | broad-phase 1차 필터, FPS 라인 오브 사이트, 픽셀 충돌 |
| **SAT (Separating Axis Theorem)** | 볼록 다각형 충돌 정밀 판정 — *분리 축이 존재하면 안 겹친다* | 2D 박스/폴리곤 충돌의 사실상 표준, OBB-OBB |
| **GJK + EPA** | 임의 볼록 도형 충돌 (3D) — Minkowski 차이 위에서 원점 포함 검사 | Unity/Bullet 물리 엔진 narrow-phase 내부 |
| **CCD (Continuous Collision Detection)** | 빠른 객체가 벽을 *통과* 하는 tunneling 방지 | 빠른 총알, 작은 객체. Unity Rigidbody "Continuous" 옵션 |
| **Verlet Integration** | 위치 기반 물리 — Euler 적분 대안, 제약 조건과 잘 어울림 | 천 시뮬레이션, 로프, 헤어, 소프트바디 |
| **Impulse-Based Collision Response** | 충돌 후 *어떻게 튕길지* — 운동량 보존으로 속도 갱신 | 당구공, 박스 던지기, 물리 퍼즐 |

### 학습 추천 다음 알고리즘

1. **SAT** — 2D 충돌의 정석. AABB 의 *분리 축 정리* 를 *회전된 박스* 로 일반화. 회전 OBB 의 충돌이 *분리 축 후보* 와 함께 시각화되면 직관이 단번에 잡힌다. AABB 데모 2번 (Overlap) 에서 미리 본 분리 축의 정식 형태.
2. (선택) **Verlet Integration** — 충돌 검사가 아닌 *시뮬레이션* 측. 천/로프/소프트바디가 매 프레임 자연스럽게 흔들리는 데모는 비주얼 임팩트가 크다.
3. (선택) **CCD (Sweep AABB)** — AABB 의 *이동 경로* 까지 고려. 빠른 객체가 벽을 *통과* 하는 tunneling 문제를 해결.

> 공간 분할 + 충돌 narrow-phase + 시뮬레이션의 3 단계가 모이면 *완전한 자체 물리 엔진* 의 기본 골격. AABB 까지 마쳤으면 1/3 완성.

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
3. ~~**MCTS**~~ — *완료* (`Assets/Algorithms/AI/MCTS`). 같은 틱택토 위에서 Minimax 와 직접 비교 가능.
4. ~~**Behavior Tree**~~ — *완료* (`Assets/Algorithms/AI/BehaviorTree`). BT × A\* 협업 시각화.
5. ~~**GOAP**~~ — *완료* (`Assets/Algorithms/AI/GOAP`). 상태 공간 A\* + Move 행동 안에서 격자 A\* 협업.
6. ~~**Utility AI**~~ — *완료* (`Assets/Algorithms/AI/UtilityAI`). 점수 함수 + 매 tick 결정 + 환경 변화 즉각 반응.
7. ~~**FSM**~~ — *완료* (`Assets/Algorithms/AI/FSM`). NPC 의사결정 4 종 (BT / GOAP / UAI / FSM) 비교 마무리.
8. ~~**Quadtree**~~ — *완료* (`Assets/Algorithms/Spatial/Quadtree`). 적응형 4 분할 + 2 페이즈 시각화 (삽입 애니메이션 + 마우스 쿼리).
9. ~~**Spatial Hashing**~~ — *완료* (`Assets/Algorithms/Spatial/SpatialHashing`). 균등 격자 + broad↔narrow 2 단계 색상 시각화. Quadtree 의 *적응 vs 균등* 짝.
10. ~~**AABB**~~ — *완료* (`Assets/Algorithms/Physics/AABB`). 충돌 *narrow-phase* 입문, 3 데모 (Contains / Overlap / Slab Raycast) 를 한 화면에 가로 배치. *분리 축* 과 *슬랩 교집합* 직관을 시각으로 박는다.
11. **SAT** — AABB 의 *분리 축 정리* 를 회전된 박스로 일반화. AABB 데모 2번에서 본 \"분리 축\" 의 정식 형태. 2D 충돌의 사실상 표준.
12. **Perlin Noise** — 절차적 생성 입문. 카테고리를 바꿔 시각적 임팩트 폭발 (지형 / 구름 / 동굴).
13. **BVH** — Spatial 카테고리의 세 번째 축 (공간이 아닌 *객체* 를 트리). Quadtree / Spatial Hashing 과 한 씬에서 비교하면 *동적 객체 처리 차이* 가 드러남.
14. **Cellular Automata** — 동굴 생성 데모. 비주얼 임팩트 큼.
15. **A\* 변형 (JPS+ 또는 Theta\*)** — 이미 구현한 A\* 의 확장.
16. **Verlet Integration** — 충돌이 아닌 *시뮬레이션* 측. 천/로프/소프트바디.

가장 *게임답고 시각적인 데모* 가 나오는 건 **Perlin Noise** + **Cellular Automata** 조합. Unity 그리드 위에 즉시 인상적인 결과가 나와서 다음 학습으로 동기 부여가 잘 된다. **물리 / 충돌 카테고리 (10번)** 는 Spatial 카테고리 (broad-phase) 의 자연스러운 다음 단계 — broad → narrow → response 의 충돌 시스템 3 단계를 채워나가는 흐름.

---

## 더 깊이 학습하고 싶을 때 참고

- **Game Programming Patterns** (Robert Nystrom) — 패턴 중심이지만 알고리즘 활용도 같이 다룸 (한국어 번역 있음)
- **Red Blob Games** (https://www.redblobgames.com) — 알고리즘 시각화 끝판왕. 특히 A\* / 헥스 그리드 / 자동 생성
- **Game AI Pro** 시리즈 — AAA 스튜디오의 실제 사용 사례 모음
- Catlike Coding — Unity 기반 절차적 생성 / 셰이더 튜토리얼
