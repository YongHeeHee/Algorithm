using System.Collections.Generic;
using Algorithms.Common;
using Algorithms.Search; // AStarAlgorithm + WeightedGraph 재사용 — BT 와 A* 의 협업
using UnityEngine;
using UnityEngine.InputSystem;

namespace Algorithms.AI
{
    /// <summary>
    /// =====================================================================
    ///  Behavior Tree 시각화 데모 (Unity 용)
    /// =====================================================================
    ///
    /// ▶ 데모 시나리오
    ///   격자 위에 Capsule 두 개 — NPC(AI) 와 Player(사용자) — 가 있다.
    ///   Player 는 WASD / 방향키로 한 칸씩 이동.
    ///   NPC 는 BT 가 매 tick 결정한 행동대로 움직인다 — Patrol → Chase → Attack → Flee.
    ///
    /// ▶ NPC 의 BT (트리 구조 자체가 우선순위 표)
    ///   Selector "Root"
    ///     ├ Sequence "Survive"  : HP 낮으면 도망 (1 순위)
    ///     │    ├ Condition "IsHpLow"
    ///     │    └ Action    "Flee"
    ///     ├ Sequence "Combat"   : 인접하면 공격 (2 순위)
    ///     │    ├ Condition "IsInAttackRange"
    ///     │    └ Action    "Attack"
    ///     ├ Sequence "Chase"    : 적이 보이면 추격 (3 순위)
    ///     │    ├ Condition "IsPlayerVisible"
    ///     │    └ Action    "Chase"   ← 여기서 A* 호출
    ///     └ Action "Patrol"     : 기본 — 미리 정한 점들 순회
    ///
    /// ▶ BT × A* 협업
    ///   "Chase" 액션이 매 tick A*Algorithm.FindPath 를 호출해 NPC → Player 의 최단 경로를 계산.
    ///   "BT 가 *어디로 갈지* 결정 → A* 가 *어떻게 갈지* 계산" 의 교과서적 분업이 같은 격자 위에서 일어난다.
    ///   이미 구현한 A* 의 *전혀 다른 응용* 사례.
    ///
    /// ▶ 색상 의미
    ///     셀
    ///       옅은 회색 : 빈 칸
    ///       검정      : 벽 (이동 불가)
    ///       옅은 청록 : 패트롤 포인트
    ///     Capsule
    ///       파랑      : Player (사용자 조작)
    ///       회색      : NPC — Idle / Patrol
    ///       노랑      : NPC — Chase (적 발견, 추격 중)
    ///       빨강      : NPC — Attack (인접 공격 중)
    ///       청록      : NPC — Flee   (HP 낮아 도주)
    ///
    /// ▶ 우측 OnGUI 트리 패널
    ///   현재 tick 의 BT 상태를 *트리 구조 그대로* 텍스트로 표시한다.
    ///     ● = 이번 tick 에 평가된 노드 (활성 경로)
    ///     ○ = 평가됐으나 결과가 Failure 라 다음 가지로 넘어간 노드
    ///     공란 = 평가 안 됨 (단락 평가로 건너뜀)
    ///     ✓ Success / ✗ Failure / … Running
    ///   캐릭터 동작과 트리 활성 분기를 *한 화면에서* 볼 수 있어 BT 의 본질이 한눈에 들어온다.
    /// </summary>
    public class BehaviorTreeVisualizer : MonoBehaviour, IAlgorithmDemo
    {
        // ─────────────────────────────────────────────────────────────
        // 인스펙터 노출 설정
        // ─────────────────────────────────────────────────────────────

        [Header("그리드 크기")]
        [SerializeField] private int width  = 12;
        [SerializeField] private int height = 12;

        [Header("타일 / 캡슐 프리팹 / 셀 간격")]
        [Tooltip("비워두면 기본 Cube 셀이 자동 생성됩니다.")]
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private float cellSize = 1.0f;

        [Header("초기 위치")]
        [SerializeField] private Vector2Int npcStart    = new Vector2Int(1, 1);
        [SerializeField] private Vector2Int playerStart = new Vector2Int(10, 10);

        [Header("패트롤 포인트 (NPC 가 Idle 시 순회)")]
        [SerializeField] private Vector2Int[] patrolPoints =
        {
            new Vector2Int( 1,  1),
            new Vector2Int(10,  1),
            new Vector2Int(10, 10),
            new Vector2Int( 1, 10),
        };

        [Header("미로")]
        [Range(0f, 0.4f)]
        [SerializeField] private float wallRatio = 0.15f;
        [SerializeField] private int randomSeed = 12345;

        [Header("BT 동작 설정")]
        [Tooltip("BT 한 tick 의 주기(초). NPC 의 한 칸 이동 주기와 같다.")]
        [SerializeField] private float tickInterval = 0.30f;

        [Tooltip("이 거리 이내 (체비셰프) 면 적이 보인다고 판단.")]
        [SerializeField] private int sightRange = 5;

        [Tooltip("이 거리 이내 (체비셰프) 면 공격 범위.")]
        [SerializeField] private int attackRange = 1;

        [Tooltip("HP 가 이 값 미만이면 'IsHpLow' 가 true → Survive 분기로.")]
        [SerializeField] private int hpLowThreshold = 30;

        [Tooltip("최대 HP. NPC HP 는 이 값을 넘지 않는다.")]
        [SerializeField] private int hpMax = 100;

        [Tooltip("Attack 액션 1 회 당 NPC 가 입는 피해(전투 비용).")]
        [SerializeField] private int hpDamagePerAttack = 12;

        [Tooltip("전투 중이 아닌 tick 마다 회복하는 HP.")]
        [SerializeField] private int hpRegenPerTick = 3;

        [Header("색상 — 셀")]
        [SerializeField] private Color colorEmpty  = new Color(0.85f, 0.85f, 0.85f);
        [SerializeField] private Color colorWall   = new Color(0.10f, 0.10f, 0.10f);
        [SerializeField] private Color colorPatrol = new Color(0.55f, 0.85f, 0.85f);

        [Header("색상 — Capsule")]
        [SerializeField] private Color colorPlayer    = new Color(0.30f, 0.55f, 1.00f);
        [SerializeField] private Color colorNpcIdle   = new Color(0.65f, 0.65f, 0.65f);
        [SerializeField] private Color colorNpcChase  = new Color(1.00f, 0.90f, 0.20f);
        [SerializeField] private Color colorNpcAttack = new Color(1.00f, 0.30f, 0.30f);
        [SerializeField] private Color colorNpcFlee   = new Color(0.30f, 0.85f, 0.85f);

        // ─────────────────────────────────────────────────────────────
        // 내부 상태 — 격자 / 그래프
        // ─────────────────────────────────────────────────────────────

        private readonly Dictionary<Vector2Int, GameObject> _cellObjects = new();
        private readonly HashSet<Vector2Int> _walls = new();

        // A* 가 사용할 가중치 그래프. 모든 가중치 = 1 (격자 균등 비용).
        private WeightedGraph<Vector2Int> _graph;

        // 4 방향 이웃 오프셋. (BFS / DFS / Flood Fill 시각화와 동일 패턴.)
        private static readonly Vector2Int[] FourDirs =
        {
            new Vector2Int( 1,  0),
            new Vector2Int(-1,  0),
            new Vector2Int( 0,  1),
            new Vector2Int( 0, -1),
        };

        // ─────────────────────────────────────────────────────────────
        // 내부 상태 — 캐릭터 (Player + NPC)
        // ─────────────────────────────────────────────────────────────

        private GameObject _playerGo, _npcGo;
        private Vector2Int _playerPos, _npcPos;

        // ─────────────────────────────────────────────────────────────
        // 내부 상태 — BT *블랙보드* (노드들이 클로저로 캡처해서 공유)
        // ─────────────────────────────────────────────────────────────

        private int _npcHealth;
        private int _patrolIdx;        // 다음에 향할 패트롤 포인트 인덱스
        private string _currentBehavior;     // UI 표시용 (Idle / Chase / Attack / Flee / Patrol)
        private Color _npcColor;             // 매 tick 의 동작에 따라 갱신
        private bool _attackedThisTick;      // 이번 tick 에 Attack 이 실행됐는가 → 데미지 적용
        private int _tickCount;

        // BT 의 루트 노드.
        private BTNode _root;

        // tick 주기 누적기 (Update 에서 시간 누적, tickInterval 도달 시 BT Tick).
        private float _tickAccumulator;

        // ─────────────────────────────────────────────────────────────
        // Unity 라이프사이클
        // ─────────────────────────────────────────────────────────────

        private void Start() => Restart();

        public void Restart()
        {
            // [1] 진행 중인 코루틴 중단 + 자식 GameObject 모두 정리.
            StopAllCoroutines();
            foreach (Transform child in transform) Destroy(child.gameObject);
            _cellObjects.Clear();
            _walls.Clear();

            // [2] 블랙보드 초기화.
            _npcHealth        = hpMax;
            _patrolIdx        = 0;
            _currentBehavior  = "Idle";
            _npcColor         = colorNpcIdle;
            _attackedThisTick = false;
            _tickCount        = 0;
            _tickAccumulator  = 0f;
            _npcPos           = npcStart;
            _playerPos        = playerStart;

            // [3] 격자 + 그래프 + 캡슐 + BT 트리 구축.
            BuildGrid();
            BuildGraph();
            BuildCapsules();
            BuildBT();
        }

        private void Update()
        {
            // [1] 사용자 입력 (Player 한 칸 이동) — Input System 사용.
            HandlePlayerInput();

            // [2] BT tick — tickInterval 마다 한 번 Tick.
            //     매 프레임 Tick 하면 NPC 가 너무 빨라 시각화가 어렵고, BT 의 *결정 단위* 자체가
            //     게임에선 보통 0.1~0.5 초 주기다.
            _tickAccumulator += Time.deltaTime;
            if (_tickAccumulator >= tickInterval)
            {
                _tickAccumulator -= tickInterval;
                TickBT();
            }

            // [3] 캡슐 시각 동기화 (위치 + NPC 색상).
            SyncCapsuleVisuals();
        }

        // ─────────────────────────────────────────────────────────────
        // 1. 격자 / 그래프 / 캡슐 / 트리 구축
        // ─────────────────────────────────────────────────────────────

        private void BuildGrid()
        {
            Random.InitState(randomSeed);

            // 패트롤 포인트와 캐릭터 시작점은 절대 벽이 되지 않도록 보호.
            var protectedSet = new HashSet<Vector2Int>(patrolPoints) { npcStart, playerStart };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var coord = new Vector2Int(x, y);

                    bool isWall = !protectedSet.Contains(coord) && Random.value < wallRatio;

                    GameObject cell;
                    var pos = new Vector3(x * cellSize, 0f, y * cellSize);
                    if (cellPrefab != null)
                    {
                        cell = Instantiate(cellPrefab, pos, Quaternion.identity, transform);
                    }
                    else
                    {
                        cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cell.transform.SetParent(transform);
                        cell.transform.position = pos;
                    }
                    cell.name = $"Cell ({x},{y})";

                    // 색 우선순위: 벽 > 패트롤 포인트 > 빈 칸.
                    Color initial =
                        isWall                       ? colorWall   :
                        IsPatrolPoint(coord)         ? colorPatrol :
                                                       colorEmpty;
                    SetColor(cell, initial);

                    _cellObjects[coord] = cell;
                    if (isWall) _walls.Add(coord);
                }
            }
        }

        private void BuildGraph()
        {
            _graph = new WeightedGraph<Vector2Int>();

            // 벽이 아닌 셀을 정점으로 등록.
            foreach (var kv in _cellObjects)
            {
                if (_walls.Contains(kv.Key)) continue;
                _graph.AddNode(kv.Key);
            }

            // 4 방향 이웃에 가중치 1 의 간선.
            // BFS / DFS 시각화와 동일한 패턴 — 한 방향만 추가하면 자연스럽게 양방향이 된다.
            foreach (var node in _graph.Nodes)
            {
                foreach (var dir in FourDirs)
                {
                    var next = node + dir;
                    if (!_graph.Contains(next)) continue;
                    _graph.AddEdge(node, next, 1f, bidirectional: false);
                }
            }
        }

        private void BuildCapsules()
        {
            _playerGo = CreateCapsule("Player", _playerPos, colorPlayer);
            _npcGo    = CreateCapsule("NPC",    _npcPos,    colorNpcIdle);
        }

        private GameObject CreateCapsule(string label, Vector2Int coord, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = label;
            go.transform.SetParent(transform);
            // 셀 위에 살짝 띄워 둔다 (셀 Cube 와 겹치면 가려지므로).
            go.transform.position   = ToWorld(coord) + new Vector3(0f, 0.6f, 0f);
            go.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            SetColor(go, color);
            return go;
        }

        // ─────────────────────────────────────────────────────────────
        // 2. BT 트리 구축
        //    빌더 패턴(.Add 체이닝)으로 트리 모양이 코드에 그대로 드러난다.
        //    Predicate / Behavior 람다는 *이 인스턴스의 메서드* 를 가리키므로
        //    블랙보드(_npcHealth 등)를 자연스럽게 캡처한다.
        // ─────────────────────────────────────────────────────────────

        private void BuildBT()
        {
            _root =
                new Selector { Name = "Root" }
                    .Add(new Sequence { Name = "Survive" }
                        .Add(new Condition { Name = "IsHpLow",        Predicate = IsHpLow })
                        .Add(new Action    { Name = "Flee",           Behavior  = FleeAction }))
                    .Add(new Sequence { Name = "Combat" }
                        .Add(new Condition { Name = "IsInAttackRange",Predicate = IsInAttackRange })
                        .Add(new Action    { Name = "Attack",         Behavior  = AttackAction }))
                    .Add(new Sequence { Name = "Chase" }
                        .Add(new Condition { Name = "IsPlayerVisible",Predicate = IsPlayerVisible })
                        .Add(new Action    { Name = "Chase (A*)",     Behavior  = ChaseAction }))
                    .Add(new Action    { Name = "Patrol",             Behavior  = PatrolAction });
        }

        // ─────────────────────────────────────────────────────────────
        // 3. BT Tick — tickInterval 마다 한 번 호출
        // ─────────────────────────────────────────────────────────────

        private void TickBT()
        {
            // [1] 시각화 메타필드 리셋 — 이번 tick 에 *어느 노드가 평가됐는지* 를 새로 측정.
            _root.ResetTickFlag();

            // [2] HP 시뮬레이션 — 이번 tick 시작 시점에 결정 (Action 콜백에서 _attackedThisTick 갱신).
            //     Attack 한 tick 이면 데미지, 아니면 자연 회복.
            //     이 단순한 시뮬레이션이 "전투 → HP 낮아짐 → Flee → 회복 → 다시 전투" 의
            //     자연스러운 oscillation 을 만들어 BT 의 반응성을 시각적으로 드러낸다.
            _attackedThisTick = false;

            // [3] 루트 평가 — 트리 전체가 한 번에 한 결정으로 수렴.
            _root.Tick();

            // [4] HP 갱신 (Action 들이 _attackedThisTick 를 켰다면 데미지 적용).
            if (_attackedThisTick)
            {
                _npcHealth = Mathf.Max(0, _npcHealth - hpDamagePerAttack);
            }
            else
            {
                _npcHealth = Mathf.Min(hpMax, _npcHealth + hpRegenPerTick);
            }

            _tickCount++;
        }

        // ─────────────────────────────────────────────────────────────
        // 4. BT — Conditions
        //    이름이 곧 의미. 셋 다 *현재 블랙보드 상태에 대한 한 줄 질문*.
        // ─────────────────────────────────────────────────────────────

        private bool IsHpLow()         => _npcHealth < hpLowThreshold;
        private bool IsPlayerVisible() => Chebyshev(_npcPos, _playerPos) <= sightRange;
        private bool IsInAttackRange() => Chebyshev(_npcPos, _playerPos) <= attackRange;

        // ─────────────────────────────────────────────────────────────
        // 5. BT — Actions
        //    각 Action 은 한 tick 에 끝나거나 (Success/Failure) 여러 tick 에 걸친다 (Running).
        //    이 데모에선 한 tick = 한 칸 이동이라 대부분 Success/Failure 만 사용.
        // ─────────────────────────────────────────────────────────────

        /// <summary>Patrol — patrolPoints 를 순서대로 도는 행동. A* 로 다음 점까지의 한 칸 전진.</summary>
        private NodeStatus PatrolAction()
        {
            _currentBehavior = "Patrol";
            _npcColor        = colorNpcIdle;

            if (patrolPoints == null || patrolPoints.Length == 0) return NodeStatus.Failure;

            var target = patrolPoints[_patrolIdx];

            // 이미 도착한 점이면 다음 점으로 인덱스를 넘긴다.
            if (_npcPos == target)
            {
                _patrolIdx = (_patrolIdx + 1) % patrolPoints.Length;
                return NodeStatus.Success;
            }

            // 다음 점을 향해 한 칸 — A* 의 첫 스텝만 사용.
            return StepTowardWithAStar(target);
        }

        /// <summary>Chase — Player 위치를 향해 A* 로 한 칸 이동.</summary>
        private NodeStatus ChaseAction()
        {
            _currentBehavior = "Chase";
            _npcColor        = colorNpcChase;
            return StepTowardWithAStar(_playerPos);
        }

        /// <summary>Attack — 인접해 있을 때 그 자리에서 공격 (HP 소모 + 빨간색).</summary>
        private NodeStatus AttackAction()
        {
            _currentBehavior  = "Attack";
            _npcColor         = colorNpcAttack;
            _attackedThisTick = true;  // TickBT 의 후처리에서 데미지 반영.
            return NodeStatus.Success;
        }

        /// <summary>Flee — Player 와 *반대 방향* 으로 한 칸. 단순 욕심쟁이 회피.</summary>
        private NodeStatus FleeAction()
        {
            _currentBehavior = "Flee";
            _npcColor        = colorNpcFlee;

            // 4 방향 후보 중 *플레이어로부터의 체비셰프 거리* 가 가장 커지는 칸으로.
            // 이동 가능한 칸이 하나도 없으면 Failure (벽에 갇힘).
            int currentDist = Chebyshev(_npcPos, _playerPos);
            Vector2Int? best = null;
            int bestDist = currentDist;

            foreach (var dir in FourDirs)
            {
                var cand = _npcPos + dir;
                if (!IsWalkable(cand))           continue;
                if (cand == _playerPos)          continue; // 사람한테 돌진하지 않음
                int d = Chebyshev(cand, _playerPos);
                if (d > bestDist) { bestDist = d; best = cand; }
            }

            if (!best.HasValue) return NodeStatus.Failure;

            _npcPos = best.Value;
            return NodeStatus.Success;
        }

        // ─────────────────────────────────────────────────────────────
        // 6. BT × A* 협업 — Move 의 핵심 헬퍼
        //    BT 의 Patrol / Chase 가 공유하는 "한 칸 전진" 로직.
        //    A* 로 전체 경로를 구하고 *첫 스텝* 만 사용한다.
        //    매 tick 새로 호출 → Player 가 움직여도 자동으로 새 경로로 추격 (반응성).
        // ─────────────────────────────────────────────────────────────

        private NodeStatus StepTowardWithAStar(Vector2Int target)
        {
            // 시작점이나 목표가 그래프에 없으면 진행 불가 (벽에 갇혔거나 잘못된 좌표).
            if (!_graph.Contains(_npcPos) || !_graph.Contains(target))
                return NodeStatus.Failure;

            if (_npcPos == target) return NodeStatus.Success;

            // A* 호출 — 휴리스틱은 Manhattan (4 방향 격자에서 admissible & consistent).
            // 이게 이 데모의 핵심: BT 의 한 *Action* 안에서 A* 알고리즘을 그대로 호출한다.
            var path = AStarAlgorithm.FindPath(_graph, _npcPos, target, ManhattanHeuristic);

            // path 는 [start, ..., goal]. 0 번째가 현 위치이므로 1 번째가 첫 스텝.
            if (path.Count < 2) return NodeStatus.Failure;

            _npcPos = path[1];
            return NodeStatus.Success;
        }

        private static float ManhattanHeuristic(Vector2Int a, Vector2Int b)
            => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        // ─────────────────────────────────────────────────────────────
        // 7. Player 입력 (신규 Input System)
        // ─────────────────────────────────────────────────────────────

        private void HandlePlayerInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // 한 키 누름 = 한 칸 이동 (wasPressedThisFrame). 누르고 있어도 한 번만 동작.
            Vector2Int dir = Vector2Int.zero;
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)    dir = new Vector2Int( 0,  1);
            else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)  dir = new Vector2Int( 0, -1);
            else if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)  dir = new Vector2Int(-1,  0);
            else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) dir = new Vector2Int( 1,  0);

            if (dir == Vector2Int.zero) return;

            var next = _playerPos + dir;
            if (IsWalkable(next) && next != _npcPos)
            {
                _playerPos = next;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 8. 캡슐 시각 동기화 — 매 프레임 호출
        //    논리 좌표 (Vector2Int) 를 월드 좌표로 변환해 캡슐 위치 갱신.
        //    NPC 는 매 tick 에 결정된 색상을 적용.
        // ─────────────────────────────────────────────────────────────

        private void SyncCapsuleVisuals()
        {
            if (_playerGo != null)
                _playerGo.transform.position = ToWorld(_playerPos) + new Vector3(0f, 0.6f, 0f);

            if (_npcGo != null)
            {
                _npcGo.transform.position = ToWorld(_npcPos) + new Vector3(0f, 0.6f, 0f);
                SetColor(_npcGo, _npcColor);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 9. OnGUI — BT 트리를 우측 패널에 매 프레임 텍스트로 렌더링
        //    "캐릭터가 *무엇을* 하는지" + "트리가 *왜* 그렇게 결정했는지" 를 한 화면에.
        // ─────────────────────────────────────────────────────────────

        private GUIStyle _treeStyle;

        private void OnGUI()
        {
            if (_root == null) return;

            // 폰트 크기를 한 번만 셋업 (OnGUI 에서 매번 new 하면 GC 가 발생).
            if (_treeStyle == null)
            {
                _treeStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            }

            const float panelWidth = 360f;
            var area = new Rect(Screen.width - panelWidth - 10, 10, panelWidth, Screen.height - 20);
            GUI.Box(area, "");

            GUILayout.BeginArea(new Rect(area.x + 8, area.y + 6, area.width - 16, area.height - 12));
            GUILayout.Label($"<b>Behavior Tree</b>     tick #{_tickCount}", _treeStyle);
            GUILayout.Label($"NPC HP: {_npcHealth} / {hpMax}", _treeStyle);
            GUILayout.Label($"현재 행동: <b>{_currentBehavior}</b>", _treeStyle);
            GUILayout.Label($"NPC: ({_npcPos.x},{_npcPos.y})  Player: ({_playerPos.x},{_playerPos.y})", _treeStyle);
            GUILayout.Space(8);
            GUILayout.Label("─── 트리 (● 활성, ○ Failure, 공란 미평가) ───", _treeStyle);
            DrawTreeNode(_root, 0);
            GUILayout.EndArea();
        }

        private void DrawTreeNode(BTNode n, int indent)
        {
            // ● 이번 tick 평가됨, ○ 평가됐으나 Failure (단락 평가 직전), 공란 = 평가 안 됨.
            string activeMark =
                !n.TickedThisFrame                       ? " "  :
                n.LastStatus == NodeStatus.Success       ? "●"  :
                n.LastStatus == NodeStatus.Running       ? "●"  :
                                                            "○";

            // 결과 아이콘.
            string statusIcon = n.LastStatus switch
            {
                NodeStatus.Success => "<color=#7CFC8C>✓</color>",
                NodeStatus.Failure => "<color=#FF7C7C>✗</color>",
                NodeStatus.Running => "<color=#FFE066>…</color>",
                _                   => " "
            };

            // 노드 종류 라벨 (텍스트가 가장 명확).
            string typeLabel = n switch
            {
                Sequence  => "[Seq]",
                Selector  => "[Sel]",
                Inverter  => "[Inv]",
                Condition => "[Cond]",
                Action    => "[Act]",
                _          => "[?]"
            };

            // 활성 경로 강조 (rich text 색상). 미평가 노드는 흐리게.
            string color = n.TickedThisFrame ? "#FFFFFF" : "#7A7A7A";

            string indentStr = new string(' ', indent * 3);
            GUILayout.Label(
                $"<color={color}>{indentStr}[{activeMark}] {typeLabel} {n.Name}  {statusIcon}</color>",
                _treeStyle);

            foreach (var c in n.EnumerateChildren()) DrawTreeNode(c, indent + 1);
        }

        // ─────────────────────────────────────────────────────────────
        // 헬퍼
        // ─────────────────────────────────────────────────────────────

        private bool IsWalkable(Vector2Int c)
        {
            if (c.x < 0 || c.x >= width || c.y < 0 || c.y >= height) return false;
            return !_walls.Contains(c);
        }

        private bool IsPatrolPoint(Vector2Int c)
        {
            if (patrolPoints == null) return false;
            for (int i = 0; i < patrolPoints.Length; i++)
                if (patrolPoints[i] == c) return true;
            return false;
        }

        private static int Chebyshev(Vector2Int a, Vector2Int b)
            => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        private Vector3 ToWorld(Vector2Int coord)
            => new Vector3(coord.x * cellSize, 0f, coord.y * cellSize);

        private static void SetColor(GameObject go, Color color)
        {
            // Renderer.material 호출은 인스턴스 머티리얼을 자동 생성 → 셀별 독립 색.
            if (go.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = color;
            }
        }
    }
}
