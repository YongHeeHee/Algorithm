using System.Collections.Generic;
using Algorithms.Common;
using Algorithms.Search;   // AStarAlgorithm + WeightedGraph 재사용 — FSM × A* 협업
using UnityEngine;
using UnityEngine.InputSystem;

namespace Algorithms.AI
{
    /// <summary>
    /// =====================================================================
    ///  FSM 시각화 데모 (Unity 용)
    /// =====================================================================
    ///
    /// ▶ 데모 시나리오 — "경비병 NPC"
    ///   격자 위에 NPC(경비병) + Player(사용자 조종) + Heal 거점이 있다.
    ///   Player 는 WASD / 방향키로 한 칸씩 이동.
    ///   NPC 는 5 개 상태 사이를 *명시적 전이* 로 오간다 — 디자이너가 그린 그래프 그대로.
    ///
    /// ▶ 5 개 상태
    ///   - Patrol  : patrolPoints 를 순서대로 순회 (NPC 회색)
    ///   - Chase   : 매 tick A* 한 칸씩 player 추격     (NPC 노랑)
    ///   - Attack  : 정지 + tick 마다 HP 감소 (전투 비용) (NPC 빨강)
    ///   - Flee    : 매 tick A* 한 칸씩 Heal 거점으로     (NPC 청록)
    ///   - Heal    : 정지 + tick 마다 HP 회복            (NPC 초록)
    ///
    /// ▶ 7 개 전이 — 등록 순서가 우선순위 (first match wins)
    ///     (1) Attack → Flee   : HP < threshold              ← 가장 위급, 먼저 등록
    ///     (2) Patrol → Chase  : 시야 진입
    ///     (3) Chase  → Attack : 인접
    ///     (4) Chase  → Patrol : 시야 잃음
    ///     (5) Attack → Chase  : 인접 벗어남
    ///     (6) Flee   → Heal   : Heal 거점 도착
    ///     (7) Heal   → Patrol : HP == 최대
    ///
    /// ▶ 핵심 메시지 — "FSM = 디자이너가 그린 명시적 그래프"
    ///   다른 의사결정 알고리즘과 가장 두드러진 차이가 시각화에 그대로 드러난다:
    ///     - BT  : *트리* 패널 — 매 tick 어느 가지가 활성인지
    ///     - GOAP: *행동 큐* 패널 — 사슬을 미리 계획
    ///     - UAI : *점수 막대* 패널 — 매 tick 모든 행동 점수 비교
    ///     - FSM : *상태 다이어그램* — 노드 + 화살표 + 라벨, 디자이너가 그린 그대로
    ///
    /// ▶ FSM × A* 협업
    ///   Chase 와 Flee 상태의 OnUpdate 안에서 `AStarAlgorithm.FindPath` 한 칸씩 호출 → 벽이 있어도
    ///   알아서 우회. BT/GOAP/UAI 와 같은 분업 패턴.
    ///
    /// ▶ 색상 의미
    ///     셀
    ///       옅은 회색 : 빈 칸
    ///       검정      : 벽
    ///       옅은 청록 : 패트롤 포인트
    ///       옅은 노랑 : NPC 시야 반경 안 (Patrol/Chase 일 때만 표시 — 시야가 *언제* 작동하는지 한눈에)
    ///       초록      : Heal 거점
    ///     Capsule
    ///       파랑      : Player (사용자 조작)
    ///       회색      : NPC — Patrol
    ///       노랑      : NPC — Chase
    ///       빨강      : NPC — Attack
    ///       청록      : NPC — Flee
    ///       초록      : NPC — Heal
    ///
    /// ▶ 우측 OnGUI 패널 — 상태 머신 그대로
    ///   각 상태 노드 아래에 *나가는 전이* 들을 화살표 + 조건 라벨로 나열.
    ///   현재 상태는 ▶ + 노란 강조. 방금 발생한 전이 (3 초 안) 는 ✦ 로 깜빡 강조.
    ///   "최근 전이 N 초 전: From → To (조건)" 한 줄도 별도로 표시.
    /// </summary>
    public class FSMGridVisualizer : MonoBehaviour, IAlgorithmDemo
    {
        // ─────────────────────────────────────────────────────────────
        // 인스펙터 노출 설정
        // ─────────────────────────────────────────────────────────────

        [Header("그리드 크기")]
        [SerializeField] private int width  = 13;
        [SerializeField] private int height = 10;
        [SerializeField] private float cellSize = 1.0f;

        [Header("초기 위치")]
        [SerializeField] private Vector2Int npcStart    = new Vector2Int(1, 1);
        [SerializeField] private Vector2Int playerStart = new Vector2Int(6, 4);
        [SerializeField] private Vector2Int healSpot    = new Vector2Int(11, 0);

        [Header("패트롤 경로 (NPC 의 Patrol 상태에서 순회)")]
        [SerializeField] private Vector2Int[] patrolPoints =
        {
            new Vector2Int( 1,  1),
            new Vector2Int(11,  1),
            new Vector2Int(11,  8),
            new Vector2Int( 1,  8),
        };

        [Header("미로 (벽 비율)")]
        [Range(0f, 0.3f)]
        [SerializeField] private float wallRatio = 0.08f;
        [SerializeField] private int randomSeed = 13;

        [Header("FSM 동작 설정")]
        [Tooltip("FSM 한 tick 의 주기(초). NPC 의 한 칸 이동 주기.")]
        [SerializeField] private float tickInterval = 0.30f;
        [Tooltip("이 거리 이내(체비셰프) 면 시야 안. Patrol → Chase 전이의 조건.")]
        [SerializeField] private int sightRange = 4;
        [Tooltip("이 거리 이내(체비셰프) 면 공격 범위. Chase → Attack 전이의 조건.")]
        [SerializeField] private int attackRange = 1;
        [Tooltip("HP 가 이 값 미만이면 'IsHpLow' true → Attack → Flee.")]
        [SerializeField] private int hpLowThreshold = 30;
        [Tooltip("최대 HP. Heal 상태의 종료 조건이기도 함 (HP == max).")]
        [SerializeField] private int hpMax = 100;
        [Tooltip("Attack 1 tick 당 NPC 가 입는 피해(전투 비용).")]
        [SerializeField] private int hpDamagePerAttack = 10;
        [Tooltip("Heal 1 tick 당 NPC 가 회복하는 HP.")]
        [SerializeField] private int hpRegenPerTick = 8;

        [Header("색상 — 셀")]
        [SerializeField] private Color colorEmpty  = new Color(0.85f, 0.85f, 0.85f);
        [SerializeField] private Color colorWall   = new Color(0.10f, 0.10f, 0.10f);
        [SerializeField] private Color colorPatrol = new Color(0.55f, 0.85f, 0.85f);
        [SerializeField] private Color colorSight  = new Color(1.00f, 0.95f, 0.55f);
        [SerializeField] private Color colorHeal   = new Color(0.40f, 0.85f, 0.40f);

        [Header("색상 — Capsule")]
        [SerializeField] private Color colorPlayer    = new Color(0.30f, 0.55f, 1.00f);
        [SerializeField] private Color colorNpcPatrol = new Color(0.65f, 0.65f, 0.65f);
        [SerializeField] private Color colorNpcChase  = new Color(1.00f, 0.90f, 0.20f);
        [SerializeField] private Color colorNpcAttack = new Color(0.95f, 0.30f, 0.30f);
        [SerializeField] private Color colorNpcFlee   = new Color(0.30f, 0.85f, 0.85f);
        [SerializeField] private Color colorNpcHeal   = new Color(0.40f, 0.85f, 0.40f);

        // ─────────────────────────────────────────────────────────────
        // 내부 상태
        // ─────────────────────────────────────────────────────────────

        private static readonly Vector2Int[] FourDirs =
        {
            new Vector2Int( 1,  0),
            new Vector2Int(-1,  0),
            new Vector2Int( 0,  1),
            new Vector2Int( 0, -1),
        };

        // 격자
        private readonly Dictionary<Vector2Int, GameObject> _cellObjects = new();
        private readonly HashSet<Vector2Int> _walls = new();
        private WeightedGraph<Vector2Int> _graph;

        // 캡슐
        private GameObject _npcGo, _playerGo;
        private Vector2Int _npcPos, _playerPos;

        // FSM
        private FSMachine _fsm;
        private FSMState  _sPatrol, _sChase, _sAttack, _sFlee, _sHeal;

        // NPC 상태
        private int  _npcHp;
        private int  _patrolIndex;
        private Color _npcColor;

        // 시야 셀 강조용
        private readonly HashSet<Vector2Int> _sightCells = new();

        // tick 주기 누적
        private float _tickTimer;

        // OnGUI
        private GUIStyle _panelStyle;
        private GUIStyle _smallStyle;

        // ─────────────────────────────────────────────────────────────
        // Unity 라이프사이클
        // ─────────────────────────────────────────────────────────────

        private void Start() => Restart();

        public void Restart()
        {
            // [1] 정리.
            StopAllCoroutines();
            foreach (Transform child in transform) Destroy(child.gameObject);
            _cellObjects.Clear();
            _walls.Clear();
            _sightCells.Clear();

            _npcPos      = npcStart;
            _playerPos   = playerStart;
            _npcHp       = hpMax;
            _patrolIndex = 0;
            _tickTimer   = 0f;

            // [2] 격자 / 그래프 / 캡슐 빌드.
            BuildGrid();
            BuildGraph();
            BuildCapsules();

            // [3] FSM 빌드 + 시작.
            BuildFSM();
            UpdateSightHighlight();
        }

        private void Update()
        {
            // [a] Player 입력 — 한 키 누름 = 한 칸.
            HandlePlayerInput();

            // [b] FSM tick — 일정 주기마다.
            _tickTimer += Time.deltaTime;
            if (_tickTimer >= tickInterval)
            {
                _tickTimer -= tickInterval;
                _fsm.Tick(Time.time);
                UpdateSightHighlight();
            }

            // [c] 시각 동기화.
            SyncVisuals();
        }

        // ─────────────────────────────────────────────────────────────
        // 1. 격자 / 그래프 / 캡슐
        // ─────────────────────────────────────────────────────────────

        private void BuildGrid()
        {
            Random.InitState(randomSeed);

            var protectedSet = new HashSet<Vector2Int> { npcStart, playerStart, healSpot };
            foreach (var p in patrolPoints) protectedSet.Add(p);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var coord = new Vector2Int(x, y);
                    bool isWall = !protectedSet.Contains(coord) && Random.value < wallRatio;

                    var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cell.transform.SetParent(transform);
                    cell.transform.position = ToWorld(coord);
                    cell.name = $"Cell ({x},{y})";

                    SetColor(cell, isWall ? colorWall : InitialColorAt(coord));
                    _cellObjects[coord] = cell;
                    if (isWall) _walls.Add(coord);
                }
            }
        }

        private Color InitialColorAt(Vector2Int c)
        {
            if (c == healSpot) return colorHeal;
            foreach (var p in patrolPoints) if (c == p) return colorPatrol;
            return colorEmpty;
        }

        private void BuildGraph()
        {
            _graph = new WeightedGraph<Vector2Int>();
            foreach (var kv in _cellObjects)
            {
                if (_walls.Contains(kv.Key)) continue;
                _graph.AddNode(kv.Key);
            }
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
            _npcGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _npcGo.name = "NPC";
            _npcGo.transform.SetParent(transform);
            _npcGo.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);

            _playerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _playerGo.name = "Player";
            _playerGo.transform.SetParent(transform);
            _playerGo.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);

            SetColor(_playerGo, colorPlayer);
            _npcColor = colorNpcPatrol;
            SetColor(_npcGo, _npcColor);

            SyncVisuals();
        }

        // ─────────────────────────────────────────────────────────────
        // 2. FSM 구성 — 5 상태 + 7 전이
        //
        //   상태별 OnEnter/OnUpdate/OnExit 람다와 전이 Condition 람다를 *이 한 곳* 에 모아둠.
        //   알고리즘 파일이 아닌 visualizer 한 곳에서 NPC 의 모든 행동을 위에서 아래로 읽을 수 있다.
        // ─────────────────────────────────────────────────────────────

        private void BuildFSM()
        {
            _fsm = new FSMachine();

            // ── 상태 정의 ──

            _sPatrol = new FSMState
            {
                Name     = "Patrol",
                OnEnter  = () => _npcColor = colorNpcPatrol,
                OnUpdate = TickPatrol,
            };

            _sChase = new FSMState
            {
                Name     = "Chase",
                OnEnter  = () => _npcColor = colorNpcChase,
                OnUpdate = TickChase,
            };

            _sAttack = new FSMState
            {
                Name     = "Attack",
                OnEnter  = () => _npcColor = colorNpcAttack,
                OnUpdate = TickAttack,   // 정지 + HP 감소
            };

            _sFlee = new FSMState
            {
                Name     = "Flee",
                OnEnter  = () => _npcColor = colorNpcFlee,
                OnUpdate = TickFlee,
            };

            _sHeal = new FSMState
            {
                Name     = "Heal",
                OnEnter  = () => _npcColor = colorNpcHeal,
                OnUpdate = TickHeal,     // 정지 + HP 회복
            };

            _fsm.AddState(_sPatrol);
            _fsm.AddState(_sChase);
            _fsm.AddState(_sAttack);
            _fsm.AddState(_sFlee);
            _fsm.AddState(_sHeal);

            // ── 전이 정의 ── (등록 순서 = 우선순위, first match wins)

            // (1) Attack → Flee : 가장 위급 → 먼저 등록해 다른 전이보다 우선.
            _fsm.AddTransition(_sAttack, _sFlee,   () => _npcHp < hpLowThreshold, "HP < threshold");

            // (2) Patrol → Chase
            _fsm.AddTransition(_sPatrol, _sChase,  IsPlayerVisible, "시야 진입");

            // (3) Chase → Attack
            _fsm.AddTransition(_sChase,  _sAttack, IsInAttackRange, "인접 (공격 범위)");

            // (4) Chase → Patrol : 시야 잃음 — Attack 보다 뒤에 둠 (Attack 먼저 검사 → 인접하면 Attack).
            _fsm.AddTransition(_sChase,  _sPatrol, () => !IsPlayerVisible(), "시야 잃음");

            // (5) Attack → Chase : Flee 보다 뒤. HP 가 낮으면 (1) 이 먼저 발화.
            _fsm.AddTransition(_sAttack, _sChase,  () => !IsInAttackRange(), "인접 벗어남");

            // (6) Flee → Heal
            _fsm.AddTransition(_sFlee,   _sHeal,   () => _npcPos == healSpot, "Heal 거점 도착");

            // (7) Heal → Patrol
            _fsm.AddTransition(_sHeal,   _sPatrol, () => _npcHp >= hpMax, "HP == 최대");

            _fsm.SetInitial(_sPatrol);
        }

        // ─────────────────────────────────────────────────────────────
        // 3. 상태별 OnUpdate
        // ─────────────────────────────────────────────────────────────

        private void TickPatrol()
        {
            // 현재 patrolPoint 에 도착했으면 다음 점으로 인덱스 전진.
            if (_npcPos == patrolPoints[_patrolIndex])
            {
                _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
            }
            StepTowardWithAStar(patrolPoints[_patrolIndex]);
        }

        private void TickChase()
        {
            // 매 tick A* 한 칸씩 player 추격.
            StepTowardWithAStar(_playerPos);
        }

        private void TickAttack()
        {
            // 정지 + 전투 비용 (=NPC HP 감소). HP 가 낮아지면 다음 tick 의 전이 검사에서 Flee 로.
            _npcHp = Mathf.Max(0, _npcHp - hpDamagePerAttack);
        }

        private void TickFlee()
        {
            // 매 tick A* 한 칸씩 healSpot 으로.
            StepTowardWithAStar(healSpot);
        }

        private void TickHeal()
        {
            // 정지 + HP 회복. 다 차면 다음 tick 의 전이 검사에서 Patrol 로.
            _npcHp = Mathf.Min(hpMax, _npcHp + hpRegenPerTick);
        }

        // 격자 A* 로 target 을 향해 한 칸 이동. Chase / Flee / Patrol 모두 같은 헬퍼를 쓴다.
        private void StepTowardWithAStar(Vector2Int target)
        {
            if (_npcPos == target) return;

            var path = AStarAlgorithm.FindPath(
                _graph,
                _npcPos,
                target,
                (a, b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y));

            // path[0] = 현재 위치, path[1] = 다음 칸. 경로가 없거나 다음 칸이 player 면 멈춤.
            if (path.Count < 2) return;
            var next = path[1];
            if (next == _playerPos) return;     // 사람한테 돌진하지 않음
            _npcPos = next;
        }

        // ─────────────────────────────────────────────────────────────
        // 4. 전이 조건 헬퍼
        //
        //   체비셰프 거리 — 8 방향 이동 가정 (대각선도 1 칸). NPC 는 4 방향만 이동하지만
        //   *시야* 와 *공격 범위* 는 대각선 포함이 자연스럽다.
        // ─────────────────────────────────────────────────────────────

        private bool IsPlayerVisible() => Chebyshev(_npcPos, _playerPos) <= sightRange;
        private bool IsInAttackRange() => Chebyshev(_npcPos, _playerPos) <= attackRange;

        private static int Chebyshev(Vector2Int a, Vector2Int b)
            => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        // ─────────────────────────────────────────────────────────────
        // 5. Player 입력 — Unity Input System (BT 데모와 같은 패턴)
        // ─────────────────────────────────────────────────────────────

        private void HandlePlayerInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            Vector2Int dir = Vector2Int.zero;
            if      (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)    dir = new Vector2Int( 0,  1);
            else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)  dir = new Vector2Int( 0, -1);
            else if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)  dir = new Vector2Int(-1,  0);
            else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) dir = new Vector2Int( 1,  0);

            if (dir == Vector2Int.zero) return;

            var next = _playerPos + dir;
            if (!IsWalkable(next)) return;
            if (next == _npcPos)   return;
            _playerPos = next;
        }

        private bool IsWalkable(Vector2Int c)
            => _cellObjects.ContainsKey(c) && !_walls.Contains(c);

        // ─────────────────────────────────────────────────────────────
        // 6. 시각 갱신 — 시야 강조 / 캡슐 위치 / NPC 색
        // ─────────────────────────────────────────────────────────────

        private void UpdateSightHighlight()
        {
            // 이전 시야 셀 색 복원.
            foreach (var c in _sightCells)
            {
                if (_walls.Contains(c)) continue;
                if (_cellObjects.TryGetValue(c, out var go)) SetColor(go, InitialColorAt(c));
            }
            _sightCells.Clear();

            // Patrol/Chase 상태에서만 시야를 *볼 수 있게* 표시 (Attack/Flee/Heal 은 시야 무관).
            if (_fsm == null) return;
            if (_fsm.Current != _sPatrol && _fsm.Current != _sChase) return;

            for (int dy = -sightRange; dy <= sightRange; dy++)
            {
                for (int dx = -sightRange; dx <= sightRange; dx++)
                {
                    var c = _npcPos + new Vector2Int(dx, dy);
                    if (Chebyshev(_npcPos, c) > sightRange) continue;
                    if (!_cellObjects.ContainsKey(c)) continue;
                    if (_walls.Contains(c)) continue;
                    if (c == _npcPos) continue;
                    if (c == healSpot) continue;            // healSpot 색 보존
                    bool isPatrolPoint = false;
                    foreach (var p in patrolPoints) if (c == p) { isPatrolPoint = true; break; }
                    if (isPatrolPoint) continue;            // patrolPoint 색 보존

                    if (_cellObjects.TryGetValue(c, out var go)) SetColor(go, colorSight);
                    _sightCells.Add(c);
                }
            }
        }

        private void SyncVisuals()
        {
            if (_npcGo != null)
            {
                _npcGo.transform.position = ToWorld(_npcPos) + new Vector3(0f, 0.6f, 0f);
                SetColor(_npcGo, _npcColor);
            }
            if (_playerGo != null)
            {
                _playerGo.transform.position = ToWorld(_playerPos) + new Vector3(0f, 0.6f, 0f);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 7. OnGUI — 우측 패널 (상태 머신 다이어그램 + HP)
        // ─────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_fsm == null) return;

            if (_panelStyle == null)
            {
                _panelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
                _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, richText = true };
            }

            const float panelWidth = 420f;
            var area = new Rect(Screen.width - panelWidth - 10, 10, panelWidth, Screen.height - 20);
            GUI.Box(area, "");

            GUILayout.BeginArea(new Rect(area.x + 8, area.y + 6, area.width - 16, area.height - 12));

            // 헤더
            GUILayout.Label("<b>FSM — 유한 상태 기계</b>", _panelStyle);
            GUILayout.Label(
                $"현재 상태: <b>{_fsm.Current?.Name}</b>  /  HP: {_npcHp}/{hpMax}",
                _panelStyle);
            GUILayout.Label($"NPC ({_npcPos.x},{_npcPos.y})  /  Player ({_playerPos.x},{_playerPos.y})", _smallStyle);
            GUILayout.Space(4);

            // HP 바
            DrawHpBar();
            GUILayout.Space(6);

            // 조작 안내
            GUILayout.Label("<color=#999999>WASD / 화살표 — Player 한 칸 이동</color>", _smallStyle);
            GUILayout.Space(4);

            // 상태 머신 다이어그램
            GUILayout.Label("─── 상태 머신 (등록 순서 = 우선순위) ───", _panelStyle);
            DrawFSM();
            GUILayout.Space(6);

            // 최근 전이
            GUILayout.Label("─── 최근 전이 ───", _panelStyle);
            DrawLastTransition();

            GUILayout.EndArea();
        }

        private void DrawHpBar()
        {
            float ratio = Mathf.Clamp01(_npcHp / (float)hpMax);
            string colorHex =
                ratio < hpLowThreshold / (float)hpMax ? "#E84A4A" :
                ratio < 0.6f                          ? "#E8B14A" :
                                                        "#5ABF6E";

            GUILayout.BeginHorizontal();
            GUILayout.Label("<color=#DDDDDD>NPC HP</color>", _smallStyle, GUILayout.Width(60));
            Rect r = GUILayoutUtility.GetRect(280f, 14f);
            GUI.color = new Color(0.15f, 0.15f, 0.15f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            var fill = new Rect(r.x, r.y, r.width * ratio, r.height);
            ColorUtility.TryParseHtmlString(colorHex, out var fillCol);
            GUI.color = fillCol;
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }

        private void DrawFSM()
        {
            // 각 상태별로: 헤더 한 줄 + 그 상태에서 *나가는* 전이 들 들여쓰기로.
            foreach (var s in _fsm.States)
            {
                bool isCurrent = (s == _fsm.Current);
                string prefix  = isCurrent ? "<color=#FFE066>▶</color>" : "  ";
                string color   = isCurrent ? "#FFE066" : "#DDDDDD";
                GUILayout.Label($"{prefix} <b><color={color}>{s.Name}</color></b>", _panelStyle);

                foreach (var t in _fsm.OutgoingFrom(s))
                {
                    bool flash = (t == _fsm.LastTransition) && (Time.time - _fsm.LastTransitionTime < 1.2f);
                    string mark    = flash ? "<color=#FFFFFF>✦</color>" : "<color=#666666>→</color>";
                    string toColor = flash ? "#FFFFFF" : "#9A9A9A";
                    GUILayout.Label(
                        $"      {mark} <color={toColor}>{t.To.Name,-7}</color>" +
                        $" <color=#777777>:  {t.Label}</color>",
                        _smallStyle);
                }
            }
        }

        private void DrawLastTransition()
        {
            var t = _fsm.LastTransition;
            if (t == null)
            {
                GUILayout.Label("  <color=#777777>(아직 전이 없음 — Player 가 시야로 들어가 보세요)</color>",
                    _smallStyle);
                return;
            }
            float ago = Time.time - _fsm.LastTransitionTime;
            string ageColor = ago < 1.2f ? "#FFE066" : "#9A9A9A";
            GUILayout.Label(
                $"  <color={ageColor}>✦ {ago:F1}초 전: " +
                $"{t.From.Name} → {t.To.Name}  ({t.Label})</color>",
                _smallStyle);
        }

        // ─────────────────────────────────────────────────────────────
        // 헬퍼
        // ─────────────────────────────────────────────────────────────

        private Vector3 ToWorld(Vector2Int coord)
            => new Vector3(coord.x * cellSize, 0f, coord.y * cellSize);

        private static void SetColor(GameObject go, Color color)
        {
            if (go != null && go.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = color;
            }
        }
    }
}
