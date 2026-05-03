# BVH — Bounding Volume Hierarchy (객체 기반 공간 분할 트리)

부피를 가진 객체들을 *가까운 것끼리 묶어* 만든 이진 트리. 각 노드는 "이 서브트리가 담는 모든 객체 AABB 의 합집합 (= 둘러싸는 AABB)" 을 가지고, leaf 만 실제 객체 인덱스를 보관한다. **Ray Query** (광선 ↔ 객체) 를 평균 `O(log N + K)` 로 가속 — Brute Force `O(N)` 대비 N 이 클수록 차이가 폭발적.

> **Quadtree / Spatial Hashing 과의 결정적 차이 — *공간이 아니라 객체를 분할***
> Quadtree 는 공간을 균등 4 등분, Spatial Hashing 은 균등 격자. 둘 다 *객체 분포를 무시* 하고 공간을 미리 나눈다. BVH 는 *객체를 따라가며* 둘로 묶으므로 형제 AABB 가 **겹쳐도 된다** (이게 버그가 아니라 정의). 비균일 분포 / 다양한 크기 객체에 강해 **레이트레이싱 (RTX) / 물리 broad-phase (Dynamic AABBTree) 의 사실상 표준**.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `BVHAlgorithm.cs` | 순수 알고리즘. `BVHObject<T>` (AABB+데이터), `BVHNode` (binary tree), `BVH<T>` (top-down longest-axis median split + Raycast), `BruteForceRaycast` (비교용 baseline) |
| `BVHVisualizer.cs` | Unity 시각화 MonoBehaviour. 2 페이즈 (Phase 1 BFS Build 애니메이션 + Phase 2 마우스 광선 Ray Query) |

> **공유 의존성** : `Algorithms.Physics.AABB` 를 *직접* 재사용. 노드의 둘러싸는 박스 = AABB, 광선 검사 = `AABB.Raycast` (Slab method). 별도 BVHBounds 를 만들지 않음으로써 "BVH 는 AABB.Raycast 를 트리 위에 쌓은 것" 이라는 학습 메시지를 코드 import 한 줄로 표현. AI 카테고리가 `Search/A*` 를 호출하는 협업 패턴과 같은 결.

## 학습 노트 (Notion)

다음 자료는 모두 Notion 에 정리되어 있다.

- 알고리즘 개요 / 동작 흐름 / 시간·공간 복잡도 / 게임 사용처
- 다이어그램
  1. AABB (원자) ↔ BVH (트리) 의존 관계 — `AABB.Raycast` 가 트리 모든 노드에서 호출되는 부품
  2. Build 흐름도 (top-down longest-axis median split, 단계별 합집합 AABB 가 좁아지는 과정)
  3. Ray Query 흐름도 (Slab method 가지치기 — 안 맞는 서브트리 통째 스킵)
  4. Quadtree / Spatial Hashing / BVH 비교표 (분할 대상 / 형제 겹침 / 동적 객체 / 킬러 앱)
  5. Brute Force vs BVH 가속 비교 (객체 N 별 ObjectsTested 추이)
- 응용 — *이 알고리즘이 ~~ 로 바뀌면 어떻게 될까* (SAH 분할, Dynamic AABBTree / Refit, 4-way BVH SIMD, BVH8 / Wide BVH)
- 직접 만져보며 확인하기 (MaxLeafSize · ObjectCount · BuildStepDelay 인스펙터 실험)

> **BVH 학습 노트**: [BVH — Notion](https://www.notion.so/3555e18a57368180a42ec03735328a38)

## 코드 안 학습 주석

각 `.cs` 파일 상단에는 한국어 헤더 블록이 있다.

- `BVHAlgorithm.cs` — 5섹션 (개요 / 동작 흐름 / 복잡도 / 게임 사용처 / 자료구조 선택). Quadtree / Spatial Hashing 과의 결정적 차이 (*공간 분할 → 객체 분할, 형제 영역 겹침 허용*) 명시. AABB (원자) → BVH (분자) 재사용 관계를 헤더 한 문단으로 강조.
- `BVHVisualizer.cs` — 사용법, 색상 의미, 2 페이즈 구조, 본 알고리즘과의 동등성 (시각화의 *광선 ↔ 노드 검사* 가 알고리즘의 *가지치기 검사* 와 정확히 같은 `AABB.Raycast` 호출).

라인별 주석에는 `[1] 가속의 본체`, `[3] 두 자식 모두에 재귀` 처럼 단계 번호가 달려 있어, 헤더의 "동작 흐름" 단계와 1:1 매핑된다.

## 빠른 사용법

1. `Assets/Scenes/Spatial/BVH.unity` 새로 생성 (`Assets/Scenes/Spatial/Quadtree.unity` 를 Duplicate 후 컴포넌트만 교체가 가장 빠름)
2. 빈 GameObject 생성 → 이름 `BVHDemo`
3. `BVHVisualizer` 컴포넌트 부착
4. **카메라 셋업** — 위에서 내려다보는 각도 필수
   - Position: `(0, 25, 0)` 부근
   - Rotation: `(90, 0, 0)`
   - Projection: `Orthographic` 권장, Size `12` (Quadtree 와 동일)
5. (선택) Restart 버튼 셋업: Canvas + Button + `AlgorithmDemoUI` 컴포넌트 (`Assets/Algorithms/Common/`)
6. Play
   - **Phase 1**: 객체들이 회색으로 나타난 뒤, BFS 순서 (루트 → 자식 → 손자) 로 노드 외곽선이 한 개씩 등장. 형제 AABB 가 겹치는 모습이 한눈에 보인다.
   - **Phase 2**: Build 종료 후 마우스를 움직이면 좌하단 파란 점 (광선 origin) 에서 마우스로 노란 광선이 뻗는다. 광선이 통과한 노드만 외곽선 주황, 가지치기된 서브트리는 회색으로 흐려짐. 분홍 객체 = 광선 hit.

## 색상 의미

| 색 | 의미 |
|----|------|
| 외곽선 흰색 | idle (Phase 1 등장 직후) |
| 외곽선 주황 | 광선이 통과 = 알고리즘이 *실제로 들어간* 노드 |
| 외곽선 회색 (반투명) | 가지치기 = 서브트리 통째로 스킵 |
| 광선 노랑 | 활성 광선 |
| 객체 회색 | pruned — 검사 자체가 안 됨 |
| 객체 주황 | 검사는 했지만 ray miss |
| 객체 분홍 | ray hit (광선이 객체 AABB 통과) |
| 원점 파랑 점 | 광선 발사 위치 (고정) |

> Quadtree 의 "외곽선 노랑 = 방문 / 점 분홍 = 결과" 컨벤션을 그대로 계승. BVH 는 광선이 1 차원 직선이라 일반적으로 *대부분의 서브트리가 회색* 이 되어 가지치기 효과가 더 극적으로 보인다.

## 변형 실험

`BVHVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- **Max Leaf Size** (1 ~ 16) — 작을수록 트리가 깊어지고 leaf 수가 많아짐. 광선 검사 정확도 ↑, 빌드 비용 ↑. `1` 로 두면 모든 객체가 자기만의 leaf 를 가지는 극단 구조.
- **Object Count** (10 ~ 200) — 객체가 많을수록 가속 효과가 체감된다. OnGUI 의 "검사한 객체 vs Brute Force = N" 비율로 확인.
- **Object Half Size Min/Max** — 객체 AABB 크기 범위. *크기 차이가 클수록* BVH 의 강점이 드러남 (Quadtree 의 약점이 BVH 의 강점).
- **Build Step Delay** — 0.2 초 이상으로 올리면 트리가 한 노드씩 등장하는 과정이 또렷이 보인다. 0 으로 두면 즉시 완성.
- **Ray Origin** — 광선 발사 위치. 월드 가운데 두면 광선이 짧고 가지치기가 줄어든다. 모서리에 두면 광선이 길고 가지치기가 극적.
- **Random Seed** — 같은 시드 = 같은 객체 분포 (재현성).

### 사고 실험

- **객체가 한 점에 100 개 몰리면?** — 모든 객체의 중심이 거의 같으므로 median split 이 거의 안 갈라진다. 트리 한 leaf 에 모두 들어가고 깊이가 거의 0 이 됨. SAH (Surface Area Heuristic) 가 이 케이스를 지능적으로 처리.
- **객체 크기 편차가 크면 (작은 100 개 + 거대한 5 개)?** — Quadtree 라면 큰 객체가 여러 셀에 걸쳐 *중복 등록* 되거나 *상위 노드에 머무름*. BVH 는 큰 객체도 작은 객체와 함께 묶이므로 *균일하게 처리*. → **이게 BVH 가 비균일 분포에 강한 본질적 이유**.
- **Median split 을 SAH 로 바꾸면?** — Surface Area Heuristic: "분할 후 두 자식 AABB 의 표면적 × 객체 수의 합" 을 최소화하는 분할점을 찾는다. 광선이 자식 박스를 *덜 만나도록* 트리를 짠다. 실무 표준이지만 빌드 비용이 크고 코드가 길어 학습용으로는 median split 이 시작점.
- **Binary tree 를 4-way / 8-way 로 바꾸면?** — 자식 4 개 / 8 개 BVH (= BVH4 / BVH8). SIMD 로 4 ~ 8 개 자식 AABB.Raycast 를 한 번에 처리해 캐시 / 명령어 단위로 빠르다. Embree / OptiX 등 산업용 레이트레이서가 사용. 알고리즘 골격은 같음.
- **객체가 매 프레임 움직이면?** — 매 프레임 재빌드는 너무 비싸다. **Dynamic AABBTree** (Box2D / Bullet 의 broad-phase) 가 답: Insert / Remove / Refit 으로 트리를 점진적 갱신. Spatial Hashing 의 "셀 좌표만 다시 계산" 에 해당하는 BVH 의 동적 변형.

## Spatial / Physics 가족 한눈에

| 항목 | Quadtree | Spatial Hashing | **BVH** | AABB |
|------|----------|-----------------|---------|------|
| 카테고리 | Spatial | Spatial | **Spatial** | Physics |
| 분할 대상 | **공간** (4 등분) | **공간** (균등 격자) | **객체** (가까운 것 묶음) | — (연산) |
| 형제 영역 겹침 | ❌ | ❌ | **✅ (정의)** | — |
| 자료구조 | 재귀 트리 (4-way) | Dictionary (해시) | **재귀 트리 (binary)** | struct 4 float |
| 동적 객체 | △ (재구축 비용 큼) | ⭕ (셀 좌표만 재계산) | △ (Refit / Dynamic AABBTree 변형 필요) | — |
| 킬러 앱 | 정적 / 중간 밀도 | 슈팅 / 입자 / Boids | **레이트레이싱 (RTX) / 물리 broad-phase** | 모든 충돌의 1차 필터 |
| Query 종류 | AABB 영역 | AABB 영역 | **광선 (ray)** | — |

이 표가 "BVH 는 *객체를 따라가는* 트리, 광선용으로 특화" 라는 한 줄 요약. AABB 와의 관계는 **AABB (원자) → BVH (분자)** — `AABB.Raycast` 가 트리 모든 노드에서 호출되는 부품이다.
