# SAP — Sweep and Prune (정렬 끝점 기반 broad-phase 충돌 검출)

N 개 AABB 의 *모든 겹치는 쌍* 을 평균 `O(N + k)` 로 찾는 broad-phase 알고리즘. 각 객체가 한 축에 *2 개 끝점 (lo, hi)* 을 만들고, 정렬한 뒤 sweep line 으로 한 번 훑으면 X projection 이 겹치는 쌍을 자연스럽게 추출. 후보쌍에 `AABB.Overlaps` 로 narrow-phase 확정.

> **Spatial 가족 안에서 SAP 만의 정체성 — *트리도 격자도 아닌 정렬***
> Quadtree / BVH / K-d tree 는 모두 *재귀 트리*, Spatial Hashing 은 *Dictionary 격자*. SAP 는 *축별 정렬된 끝점 리스트* — 완전히 다른 접근. 게다가 다른 알고리즘들이 "한 점/광선 → 결과" 인 것과 달리 SAP 는 "*N 개 객체의 모든 겹침 쌍*" 을 한꺼번에 계산. 쿼리가 *전체* 라는 게 결정적 다름.

> **SAP 의 진짜 강점 — *Temporal Coherence***
> 객체가 매 프레임 *조금* 움직이면 정렬 순서가 *거의* 그대로. *insertion sort* 로 정렬 유지하면 평균 O(N) (full re-sort 의 O(N log N) 대신). 객체 30 개여도 매 프레임 swap 이 보통 0~3 회. 이게 **Bullet / PhysX / 초기 Box2D 의 broad-phase 표준이었던 본질적 이유**.

## 파일 구성

| 파일 | 역할 |
|------|------|
| `SAPAlgorithm.cs` | 순수 알고리즘. `SAPObject<T>`, `SAPEndpoint`, `SAPPair`, `SweepAndPrune<T>` (Build / SweepAll / SweepAllStepped / UpdateAndSweep with insertion sort), `BruteForceAllPairs` |
| `SAPVisualizer.cs` | Unity 시각화 MonoBehaviour. **2 페이즈 골격이 다른 Spatial 데모와 다름** — Phase 1 정적 sweep 애니메이션 → Phase 2 동적 모드 (객체 이동 + insertion sort + 매 프레임 swap 카운트) |

> **공유 의존성** : `Algorithms.Physics.AABB` 의 `Overlaps` (분리축 검사) 를 후보쌍 검사에 *그대로* 호출. BVH 의 `AABB.Raycast` 재사용과 같은 "원자 → 분자" 패턴 — AABB 3 핵심 연산 중 *Overlaps* 가 SAP 의 narrow-phase 부품.

## 학습 노트 (Notion)

다음 자료는 모두 Notion 에 정리되어 있다.

- 알고리즘 개요 / 동작 흐름 / 시간·공간 복잡도 / 게임 사용처
- 다이어그램
  1. Spatial 5 형제 비교 (트리 / 격자 / 정렬 + 동적 객체 강점)
  2. Sweep 흐름도 (lo → active 추가 → 후보쌍 검사, hi → active 제거)
  3. 동적 모드 흐름도 (위치 갱신 → endpoint Value 갱신 → insertion sort → sweep)
  4. Temporal coherence 시각화 (객체 30 개 움직일 때 swap 분포 관찰)
  5. Brute Force vs SAP (객체 N 별 비교 횟수)
- 응용 — *이 알고리즘이 ~~ 로 바뀌면 어떻게 될까* (Multi-axis SAP, Incremental SAP, Persistent Manifold, Dynamic AABBTree 로의 이전 — 모던 PhysX 가 SAP 를 떠난 이유)
- 직접 만져보며 확인하기 (ObjectCount · MaxSpeed · SweepStepDelay 인스펙터 실험)

> **SAP 학습 노트**: [SAP — Notion](https://www.notion.so/3555e18a5736813dbd04ded647cbe9e7)

## 코드 안 학습 주석

각 `.cs` 파일 상단에는 한국어 헤더 블록이 있다.

- `SAPAlgorithm.cs` — 5섹션 (개요 / 동작 흐름 / 복잡도 / 게임 사용처 / 자료구조 선택). Spatial 4 트리 형제와의 결정적 차이 (*트리가 아닌 정렬*) + 진짜 정체성 (*Temporal coherence*) 를 헤더에서 강조. 왜 insertion sort 인지 (거의 정렬된 데이터에 평균 O(N)) 까지 명시.
- `SAPVisualizer.cs` — 사용법, 색상 의미, 2 페이즈 구조, AABB.Overlaps 호출 위치 (broad-phase candidate 검사 시점) 명시.

라인별 주석에는 `[1] lo 끝점 — 기존 actives 모두와 후보쌍`, `[3] *Insertion sort* — temporal coherence 의 본체` 처럼 단계 번호 + 의미 주석.

## 빠른 사용법

1. `Assets/Scenes/Spatial/SAP.unity` 새로 생성 (`Quadtree.unity` Duplicate 후 컴포넌트만 교체가 가장 빠름)
2. 빈 GameObject 생성 → 이름 `SAPDemo`
3. `SAPVisualizer` 컴포넌트 부착
4. **카메라 셋업** — 위에서 내려다보는 각도, *endpoint strip 까지 보이도록 약간 크게*
   - Position: `(0, 28, -3)` 부근 (z 를 약간 음수로 — 하단 strip 까지 시야 확보)
   - Rotation: `(75, 0, 0)` (수직보다 약간 기울임 — strip 이 평면에 보이도록)
   - Projection: `Orthographic`, Size `14` (다른 Spatial 데모의 12 보다 약간 크게)
   - 또는 평면 강조하려면 Quadtree 와 같은 (90, 0, 0) 도 OK — strip 마커가 평면에 일자로 보임
5. (선택) Restart 버튼 셋업: Canvas + Button + `AlgorithmDemoUI` 컴포넌트 (`Assets/Algorithms/Common/`)
6. Play
   - **Phase 1**: 객체들이 회색으로 등장. 파란 sweep line 이 좌→우로 한 endpoint 씩 이동. lo (초록 점) 만나면 객체 노랑 + 노란 candidate 연결선 깜빡 → 분홍 confirmed 영구 연결선. hi (빨강 점) 만나면 active 해제. 우상단 OnGUI 에 active 리스트 + 진행 메시지.
   - **Phase 2**: Phase 1 종료 후 1~2초 정지, 그 다음 객체들이 무작위로 이동 시작. Endpoint 마커들이 X 축 따라 슬라이드. **매 프레임 insertion sort swap 횟수가 OnGUI 에 표시** — 객체 20 개여도 보통 0~5 회. 현재 겹침 쌍은 분홍 연결선으로 매 프레임 갱신.

## 색상 의미

| 색 | 의미 |
|----|------|
| AABB 회색 | idle — 검사 안 됨 / active 아님 |
| AABB 노랑 | 현재 active set 멤버 (sweep line 통과 중, Phase 1 만) |
| AABB 분홍 | 확정 겹침에 포함된 객체 |
| Endpoint 초록 | lo (sweep 이 만나면 active 추가) |
| Endpoint 빨강 | hi (sweep 이 만나면 active 제거) |
| Sweep line 파랑 | 현재 sweep 위치 (Phase 1 만 표시) |
| 연결선 노랑 | candidate 쌍 (검사 직전, 짧게 깜빡) |
| 연결선 분홍 | confirmed 겹침 쌍 (Phase 1 누적 / Phase 2 매 프레임 재생성) |

> Quadtree / BVH / K-d tree 의 "노랑 = 활성 / 분홍 = 결과" 컨벤션 계승. SAP 는 *sweep 진행* 이 메인이라 파란 sweep line + 양 끝점 색 (lo 초록 / hi 빨강) 이 추가됨.

## 변형 실험

`SAPVisualizer` 인스펙터에서 바로 바꿔 볼 수 있다.

- **Object Count** (5 ~ 100) — 객체가 많을수록 endpoint 도 늘고, Phase 1 sweep 이 길어짐. Phase 2 의 swap 카운트 비교가 더 인상적.
- **Sweep Step Delay** (Phase 1) — 0.5 초로 올리면 sweep 의 한 단계가 또렷이 보임. 0 으로 두면 즉시 완료.
- **Max Speed** (Phase 2) — 객체 이동 속도. 빠를수록 endpoint 가 격렬히 슬라이드 → swap 횟수 증가. *느리면 swap 거의 0* — temporal coherence 가 가장 잘 보이는 설정.
- **Object Half Size Min/Max** — 객체 크기. 클수록 겹침이 많고 active set / overlap 쌍 수가 폭증.
- **Random Seed** — 같은 시드 = 같은 객체 분포 + 같은 속도 (재현성).

### 사고 실험

- **객체가 모두 *같은 X 좌표* 에 있으면?** — Phase 1 에서 모든 객체의 lo 가 동시에 발생 (실제로는 정렬 안정성에 따라 순서 달라지지만), active set 이 N 까지 커져 *후보쌍이 N²/2* — SAP 가속 효과 사라짐. *축 분포의 균일성이 SAP 효율의 핵심*.
- **객체가 *느리게* 움직이면?** — Phase 2 swap 횟수가 거의 0 에 수렴. 이게 SAP 가 게임 물리 엔진에 적합한 이유 (보통 객체는 *프레임 간 작게* 움직임).
- **객체가 *순간이동* 하면?** — Insertion sort 가 O(N²) 로 폭발. 이때는 full re-sort 가 더 빠름. Persistent Manifold 같은 고급 기법으로 회피.
- **양축 SAP 로 확장하면?** — Y 축에도 endpoint 리스트 + sweep. 후보쌍 = X *AND* Y 둘 다 active 인 쌍. 정확도 높아지지만 코드 2 배 + Y 축에서도 insertion sort 별도 유지. 모던 PhysX 가 한때 표준.
- **객체가 100,000 개로 늘면?** — SAP 는 active set 폭증 시 약점. 모던 PhysX 는 SAP 를 떠나 **Dynamic AABBTree** (BVH 변형) 로 이전했음. SAP 는 객체 수가 *수백 ~ 수천* 일 때 sweet spot.

## Spatial 5 형제 한눈에

| 항목 | Quadtree | Spatial Hashing | BVH | K-d tree | **SAP** |
|------|----------|-----------------|-----|----------|---------|
| 자료구조 | 재귀 트리 (4-way) | Dictionary 격자 | binary 트리 (객체) | binary 트리 (반평면) | **축별 정렬 리스트** |
| 분할 단위 | 공간 4 등분 | 공간 균등 격자 | 객체 묶음 | 공간 (반평면) | **분할 없음** |
| 동적 객체 | △ (재구축) | ⭕ | △ (Refit) | △ (정적) | **⭕⭕ (insertion sort)** |
| 쿼리 종류 | 영역 (range) | 영역 (range) | 광선 (ray) | kNN (최근접) | **모든 겹침 쌍** |
| 산업 표준 사용처 | LOD / 2D culling | 슈팅 / 입자 / Boids | 레이트레이싱 (RTX) | ML 벡터 검색 | **물리 broad-phase (Bullet)** |

이 표가 "SAP 는 *트리도 격자도 아닌, 정렬과 sweep 으로 매 프레임 N 객체의 모든 겹침을 빠르게 갱신* 하는 자료구조" 라는 한 줄 요약. AABB 와의 관계는 **AABB.Overlaps 의 sorted-list 활용** — `BVH ↔ AABB.Raycast` 와 짝을 이루는 또 다른 "원자 → 분자" 사례.
