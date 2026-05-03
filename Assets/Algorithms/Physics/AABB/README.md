# AABB — Axis-Aligned Bounding Box

축에 평행한 (회전 없는) 직사각형/박스. 게임의 모든 충돌 시스템이 가장 먼저 만나는 **원자 단위**다. 회전된 OBB / 폴리곤 / 곡선 충돌 같은 비싼 narrow-phase 검사 전에 AABB 가 1 차 필터로 들어가, 안 겹치면 정밀 검사 자체를 스킵한다. **broad-phase** (Quadtree, Spatial Hashing) 의 셀에 들어가는 것도, **frustum culling** 도, **선택 박스 (RTS)** 도 모두 AABB.

이 페이지는 *연산 자체* 가 주제 — Quadtree 의 `QuadtreeBounds` / Spatial Hashing 의 `SpatialHashBounds` 는 자료구조 *내부 부품* 으로 AABB 를 이미 쓰고 있지만, 그 데모는 *공간 인덱싱* 이 주제고 AABB 연산은 보조 역할이었다. 이 페이지는 AABB 의 3 핵심 연산 (Contains / Overlaps / Slab Raycast) 을 한 화면에 펼쳐 보여준다.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `AABBAlgorithm.cs` | 순수 알고리즘. `AABB` (struct) + `Contains` + `Overlaps` + `GetSeparatingAxis` + `Raycast` (Slab method) + `AABBRaycastResult` |
| `AABBVisualizer.cs` | Unity 시각화 MonoBehaviour. **3 데모 한 화면 가로 배치** — 마우스 한 번에 세 데모 동시 업데이트. OnGUI 에 t 값 + 부등식 결과 |

> AABB 는 새 카테고리 `Algorithms.Physics` (물리 / 충돌, narrow-phase + 시뮬) 의 첫 알고리즘. `Spatial` 카테고리 (broad-phase) 와 *짝* 을 이룬다 — 공간 분할이 후보를 추리고, AABB / SAT / GJK 가 정밀 판정.

## 학습 노트 (Notion)

다음 자료는 모두 Notion에 정리되어 있다.

- 알고리즘 개요 / 동작 흐름 / 시간·공간 복잡도 / 게임 사용처
- 다이어그램
  1. Slab method 의 핵심 통찰 — *각 축의 진입/이탈 구간을 교집합 한 게 박스 통과 구간*
  2. 두 박스 Overlap 의 분리 축 (SAT 의 단순 형태) 미리보기
  3. AABB 가 broad-phase ↔ narrow-phase 어디에 들어가는지 위치 표
- 응용 — OBB (회전된 박스), AABB Tree, Sweep AABB (이동 중 충돌)
- 직접 만져보며 확인하기 (각 데모 인스펙터 파라미터 실험)

> **AABB 학습 노트**: [AABB — Notion](https://www.notion.so/3555e18a573681c9b988fcf703a37dbd)

## 코드 안 학습 주석

각 `.cs` 파일 상단에는 한국어 헤더 블록이 있다.

- `AABBAlgorithm.cs` — 5섹션 (개요 / 동작 흐름 / 복잡도 / 게임 사용처 / 자료구조 선택). Slab method 의 핵심 통찰을 헤더에서 한 문장으로: *"각 축의 진입/이탈 구간을 교집합 한 게 박스 통과 구간"*.
- `AABBVisualizer.cs` — 3 데모 구성, 한 마우스 → 세 데모 동시 갱신, Quadtree / SpatialHashing 의 2 페이즈 데모와의 차이 (AABB 는 단일 페이즈 — 연산 자체가 주제라 삽입 단계 없음).

라인별 주석에는 `[1] X 슬랩`, `[3] 두 슬랩 교집합` 처럼 단계 번호가 달려 있어, 헤더의 "동작 흐름" 단계와 1:1 매핑된다.

## 빠른 사용법

1. `Assets/Scenes/0.TempScene.unity` 를 복사 → `Assets/Scenes/Physics/AABB.unity` 로 이름 변경 (부모 폴더 `Physics/` 는 이미 존재)
2. 빈 GameObject 생성 → 이름 `AABBDemo` → `AABBVisualizer` 컴포넌트 부착
3. **카메라 셋업** — 위에서 내려다보는 각도 + *조금 더 넓게* (3 데모 가로 배치라 시야가 더 필요)
   - Position `(0, 25, 0)` / Rotation `(90, 0, 0)`
   - Projection `Orthographic`, Size `11` 권장 (Quadtree 의 12 보다 약간 작게 — 가로 영역에 집중)
4. (선택) Restart 버튼 셋업: Canvas + Button + `AlgorithmDemoUI` (`Assets/Algorithms/Common/`)
5. Play
   - 마우스를 움직이며 *세 데모를 동시에 관찰*. 마우스 위치에 따라 1번/2번/3번이 각각 반응
   - 좌상단 OnGUI 에 각 데모의 결과 텍스트 (Slab method 의 4 개 t 값 + 부등식 포함)

## 데모 구성

### 1️⃣ Contains (점 ↔ 박스) — 워밍업
- 정적 박스 + 마우스 따라다니는 점
- 점이 박스 안 → **박스 초록 / 점 분홍**
- 검사는 4 비교: `MinX ≤ x ≤ MaxX AND MinY ≤ y ≤ MaxY`

### 2️⃣ Overlap (박스 ↔ 박스) — SAT 미리보기
- 정적 박스 + 마우스 중심 작은 박스 (probe)
- 겹침 → **두 박스 모두 노랑**
- OnGUI 에 *분리 축* 표시: "X 축에서 분리" / "Y 축에서 분리" / "분리 축 없음 (겹침)"
- → SAT 의 *분리 축 정리* 의 가장 단순한 형태: "한 축이라도 분리되면 안 겹침"

### 3️⃣ Slab Raycast (광선 ↔ 박스) — **메인**
- 정적 박스 + 고정 원점 + 마우스 방향으로 광선
- **4 개 슬랩 점** (광선 위 작은 동그라미):
  - 파랑 2 개: X 슬랩 진입/이탈 시각의 광선 위치
  - 초록 2 개: Y 슬랩 진입/이탈 시각의 광선 위치
- **2 개 분홍 점**: Hit 일 때만 표시 — 박스 *진입* / *이탈* 지점
- 광선 색: 회색 (miss) / 노랑 (hit)
- OnGUI 에 *모든 t 값* + 부등식 (`tEnter ≤ tExit ∧ tExit ≥ 0`)

> **이 데모의 학습 핵심**: 4 개 슬랩 점이 광선 위에 흩어져 있다가, 광선이 박스를 통과할 때 *교집합 구간* 이 비어있지 않은 모양으로 정렬된다. Slab method 의 직관 — "*각 축의 진입/이탈 구간을 교집합 한 게 박스 통과 구간*" — 이 시각으로 박힌다.

## 색상 의미

| 색 | 의미 |
|----|------|
| 박스 회색 | 검사 결과 음성 (Contains 밖 / Overlap 안 됨 / Raycast miss) |
| 박스 초록 | Contains — 점이 안에 있음 |
| 박스/광선 노랑 | Overlap 또는 Raycast Hit |
| 점 회색 | Demo 1 의 마우스 점 (박스 밖) |
| 점 분홍 | Demo 1 의 마우스 점 (박스 안) **또는** Demo 3 의 박스 진입/이탈 지점 |
| 광선 위 파랑 점 | Demo 3 의 X 슬랩 진입/이탈 시각 광선 위치 |
| 광선 위 초록 점 | Demo 3 의 Y 슬랩 진입/이탈 시각 광선 위치 |
| 작은 파란 점 (고정) | Demo 3 의 광선 원점 |

> BFS / DFS 의 "노랑 = 활성 / 분홍 = 결과 / 회색 = 미활성" 컨벤션을 그대로 계승. *시작* / *벽* 색은 카테고리 성격 (탐색이 아닌 *연산*) 이라 자연스럽게 빠진다.

## 변형 실험

`AABBVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- **Demo 1 / 2 / 3 Center** — 세 데모의 화면 위치 조정
- **Demo 1 Box Half Size** / **Demo 2 Static Half Size** — 박스 크기 변경
- **Demo 2 Probe Half Size** — 마우스 박스 크기. 작게 하면 거의 점, 크게 하면 정적 박스에 거의 닿지 않아도 겹침
- **Demo 3 Ray Origin** — 광선 발사 위치. 박스 가까이 두면 슬랩 점이 박스 근처에 모이고, 멀리 두면 광선이 길어진다
- **Demo 3 Ray Max Length** — 광선 시각화 길이

### 사고 실험

- **Demo 3 에서 마우스를 박스 가운데로 정확히 가져가면?** — 광선이 박스를 통과하므로 hit. tEnter, tExit 사이에 박스의 너비/높이만큼의 거리가 들어 있음.
- **마우스를 박스 *모서리* 근처로 가져가면?** — tEnterX 와 tEnterY 가 거의 같아진다 (= 두 슬랩에 거의 동시에 진입). 슬랩의 의미가 가장 명확히 보이는 순간.
- **마우스를 박스 *옆* 으로 비스듬히** — 한 슬랩만 통과 → tEnter > tExit → miss. 4 개 점은 광선 위에 있지만 *교집합이 비어있는 패턴* 이 보인다.
- **광선을 *반대 방향* 으로 (마우스를 origin 너머로)** — 박스가 광선 *뒤쪽* 이면 tExit < 0 → miss. tEnter, tExit 가 모두 음수.

## broad-phase ↔ narrow-phase 위치

| 단계 | 알고리즘 | AABB 의 역할 |
|------|----------|-------------|
| **Broad-phase** (후보 추리기) | Quadtree, Spatial Hashing, BVH | 셀/노드의 영역 표현 = AABB. 두 셀의 AABB 가 안 겹치면 그 셀 안의 객체쌍은 검사 안 함 |
| **Narrow-phase 1차 필터** | (이 페이지) | 두 객체의 AABB Overlaps 가 false 면 정밀 검사 스킵 |
| **Narrow-phase 정밀** | SAT, GJK | 회전 OBB / 임의 볼록도형 — AABB 통과한 쌍에만 적용 |
| **시뮬레이션** | Verlet, Impulse Response | 충돌 *후* 어떻게 움직일지 |

→ AABB 는 broad ↔ narrow 어디에서나 *1 차 필터* 로 등장. 학습 우선순위가 가장 앞에 있어야 하는 이유.

## 다른 알고리즘들과의 비교 한눈에

| 항목 | Quadtree | Spatial Hashing | **AABB** (이 페이지) |
|------|----------|-----------------|------|
| 카테고리 | Spatial | Spatial | **Physics** |
| 역할 | 자료구조 (broad) | 자료구조 (broad) | **연산 (narrow 1차 / broad 보조)** |
| 자료 | 재귀 트리 | Dictionary | **struct 4 float** |
| 페이즈 | 2 (삽입 + 쿼리) | 2 (삽입 + 쿼리) | **단일 페이즈** (연산 자체가 주제) |

이 표가 "AABB 는 *알고리즘이 자료구조* 다" 라는 한 줄 요약. 박스 하나가 모든 정보고, 연산이 자체 의미를 가진다.
