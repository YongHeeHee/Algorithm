using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Algorithms.Common;
using Algorithms.Search;   // AStarAlgorithm + WeightedGraph 재사용 — GOAP 와 A* 의 협업
using UnityEngine;

namespace Algorithms.AI
{
    /// <summary>
    /// =====================================================================
    ///  GOAP 시각화 데모 (Unity 용)
    /// =====================================================================
    ///
    /// ▶ 데모 시나리오 — "나무꾼 / 사냥꾼 NPC"
    ///   격자 위에 NPC 와 5 개의 거점이 있다.
    ///     - Axe(노랑)   : 도끼가 놓인 자리
    ///     - Tree(초록)  : 나무 — 도끼가 있어야 베어 장작이 됨
    ///     - Fire(주황)  : 모닥불 — 장작으로 점화 가능
    ///     - Food(베이지): 식재료 — 점화된 모닥불에서 조리해야 먹을 수 있음
    ///     - Enemy(빨강) : 적 — 도끼로 처치 가능
    ///
    ///   NPC 는 이 다섯 거점을 자유롭게 오갈 수 있고, 각 거점에서 정해진 행동만 수행 가능.
    ///   행동 사이의 *순서는 어디에도 정의되어 있지 않다*. 디자이너가 정의한 건 오직
    ///   "각 행동의 전제조건과 효과" 뿐.
    ///
    /// ▶ 핵심 메시지
    ///   인스펙터에서 *목표(goal)* 만 바꾸면 — Eat 또는 KillEnemy — GOAP 가 그 목표에 맞는
    ///   *전혀 다른 행동 시퀀스* 를 자동으로 짜낸다.
    ///     Eat 목표:
    ///       Move(axe) → PickAxe → Move(tree) → ChopWood → Move(fire) → LightFire
    ///         → Move(food) → PickFood → Move(fire) → CookAndEat
    ///     KillEnemy 목표:
    ///       Move(axe) → PickAxe → Move(enemy) → AttackEnemy
    ///   같은 행동 카탈로그, 같은 세상, 다른 목표 → 다른 계획. 이게 GOAP 의 본질.
    ///
    /// ▶ GOAP × A* 협업
    ///   1) 추상 GOAP 계획 — 어떤 *행동 사슬* 을 수행할지 결정 (state space A*).
    ///   2) Move 행동 한 단위 = 격자 위 A* 길찾기로 한 칸씩 이동.
    ///   "행동 *사이* 의 순서는 GOAP 가, 행동 *내부* 의 이동은 격자 A* 가" — BT × A* 와 비슷한
    ///   분업 패턴이지만 결정 주체가 *디자이너의 트리* → *행동 카탈로그* 로 바뀐 것이 GOAP 의 차이.
    ///
    /// ▶ 색상 의미
    ///     셀
    ///       옅은 회색 : 빈 칸
    ///       검정      : 벽 (이동 불가)
    ///       분홍      : 현재 진행 중인 격자 경로 (Move 행동 중)
    ///     오브젝트 (특정 좌표의 색)
    ///       노랑      : Axe — 줍기 전 (PickAxe 후 사라짐)
    ///       초록      : Tree — 베기 전 (ChopWood 후 흐려짐)
    ///       주황      : Fire — 꺼짐 / 빨강 : Fire — 점화됨
    ///       베이지    : Food — 줍기 전 (PickFood 후 사라짐)
    ///       진빨강    : Enemy — 살아 있음 / 회색 : Enemy — 처치됨
    ///     NPC
    ///       파랑      : 이동 중 / 대기
    ///       노랑      : 행동 수행 중 (PickAxe, ChopWood, ...)
    ///
    /// ▶ 우측 OnGUI 패널 — 모든 GOAP 정보가 한 화면에
    ///   - 현재 목표 (Eat / KillEnemy)
    ///   - 계획 통계 (탐색 상태 수 / 생성 상태 수 / 총 비용)
    ///   - 현재 세상 상태 (사실 ✓/✗ 리스트) — *행동 1 개마다 갱신*
    ///   - 계획된 행동 큐 (현재 실행 중인 행동 강조)
    /// </summary>
    public class GOAPGridVisualizer : MonoBehaviour, IAlgorithmDemo
    {
        // ─────────────────────────────────────────────────────────────
        // 인스펙터 노출 설정
        // ─────────────────────────────────────────────────────────────

        public enum GoalType { Eat, KillEnemy }

        [Header("그리드 크기")]
        [SerializeField] private int width  = 11;
        [SerializeField] private int height = 7;
        [SerializeField] private float cellSize = 1.0f;

        [Header("거점 좌표")]
        [SerializeField] private Vector2Int npcStart  = new Vector2Int(0, 3);
        [SerializeField] private Vector2Int axeSpot   = new Vector2Int(2, 5);
        [SerializeField] private Vector2Int treeSpot  = new Vector2Int(4, 1);
        [SerializeField] private Vector2Int fireSpot  = new Vector2Int(6, 3);
        [SerializeField] private Vector2Int foodSpot  = new Vector2Int(8, 5);
        [SerializeField] private Vector2Int enemySpot = new Vector2Int(10, 1);

        [Header("목표 (인스펙터에서 바꿔서 다른 계획이 나오는지 보세요)")]
        [SerializeField] private GoalType goal = GoalType.Eat;

        [Header("미로 (벽 비율)")]
        [Range(0f, 0.4f)]
        [SerializeField] private float wallRatio = 0.05f;
        [SerializeField] private int randomSeed = 7;

        [Header("실행 속도")]
        [Tooltip("격자 한 칸 이동 사이의 대기 시간(초).")]
        [SerializeField] private float stepInterval = 0.18f;
        [Tooltip("Move 가 아닌 행동(PickAxe 등) 수행 시 시각 강조 시간(초).")]
        [SerializeField] private float actionPause  = 0.55f;
        [Tooltip("계획이 끝난 뒤 잠시 멈췄다가 자동 재시작하기까지의 시간(초).")]
        [SerializeField] private float planFinishedPause = 2.0f;

        [Header("색상 — 셀")]
        [SerializeField] private Color colorEmpty = new Color(0.85f, 0.85f, 0.85f);
        [SerializeField] private Color colorWall  = new Color(0.10f, 0.10f, 0.10f);
        [SerializeField] private Color colorPath  = new Color(1.00f, 0.65f, 0.85f);

        [Header("색상 — 오브젝트")]
        [SerializeField] private Color colorAxe       = new Color(1.00f, 0.85f, 0.20f);
        [SerializeField] private Color colorTree      = new Color(0.30f, 0.65f, 0.30f);
        [SerializeField] private Color colorFire      = new Color(1.00f, 0.55f, 0.20f);
        [SerializeField] private Color colorFireLit   = new Color(1.00f, 0.20f, 0.10f);
        [SerializeField] private Color colorFood      = new Color(0.95f, 0.78f, 0.45f);
        [SerializeField] private Color colorEnemy     = new Color(0.85f, 0.20f, 0.30f);
        [SerializeField] private Color colorEnemyDead = new Color(0.40f, 0.40f, 0.40f);

        [Header("색상 — NPC")]
        [SerializeField] private Color colorNpcIdle   = new Color(0.30f, 0.55f, 1.00f);
        [SerializeField] private Color colorNpcActing = new Color(1.00f, 0.90f, 0.20f);

        // ─────────────────────────────────────────────────────────────
        // 내부 상태
        // ─────────────────────────────────────────────────────────────

        // 격자
        private readonly Dictionary<Vector2Int, GameObject> _cellObjects = new();
        private readonly HashSet<Vector2Int> _walls = new();
        private WeightedGraph<Vector2Int> _graph;

        private static readonly Vector2Int[] FourDirs =
        {
            new Vector2Int( 1,  0),
            new Vector2Int(-1,  0),
            new Vector2Int( 0,  1),
            new Vector2Int( 0, -1),
        };

        // NPC
        private GameObject _npcGo;
        private Vector2Int _npcPos;

        // 위치 라벨 → 격자 좌표 (행동 비용 동적 계산에 사용)
        private Dictionary<string, Vector2Int> _locationOf;

        // GOAP 상태 / 카탈로그 / 계획
        private WorldState        _state;
        private List<GOAPAction>  _catalog;
        private GOAPPlan          _plan;

        // 실행 진행
        private int     _currentStep = -1;
        private string  _currentDoing = "";
        private List<Vector2Int> _highlightedPath = new();

        // OnGUI 캐시
        private GUIStyle _panelStyle;

        // ─────────────────────────────────────────────────────────────
        // Unity 라이프사이클
        // ─────────────────────────────────────────────────────────────

        private void Start() => Restart();

        public void Restart()
        {
            // [1] 진행 중인 코루틴/오브젝트/내부 상태 정리.
            StopAllCoroutines();
            foreach (Transform child in transform) Destroy(child.gameObject);
            _cellObjects.Clear();
            _walls.Clear();
            _highlightedPath.Clear();
            _currentStep  = -1;
            _currentDoing = "Planning...";
            _npcPos       = npcStart;

            // [2] 격자 / 그래프 / NPC / 거점 좌표 매핑 구축.
            BuildGrid();
            BuildGraph();
            BuildNpc();
            BuildLocations();

            // [3] GOAP 입력 구축 — 초기 세상 상태, 행동 카탈로그, 목표.
            BuildInitialState();
            BuildCatalog();
            var goalState = BuildGoal();

            // [4] 계획 — 이게 GOAP 의 핵심 한 줄. 상태 공간 위의 A* 가 행동 시퀀스를 찾는다.
            _plan = GOAPPlanner.Plan(_state, goalState, _catalog);

            // [5] 계획 실행 — 코루틴이 행동 하나씩 시각화하며 진행.
            StartCoroutine(ExecutePlan());
        }

        // ─────────────────────────────────────────────────────────────
        // 1. 격자 / 그래프 / NPC / 거점
        // ─────────────────────────────────────────────────────────────

        private void BuildGrid()
        {
            Random.InitState(randomSeed);

            // 거점과 시작점은 절대 벽이 되지 않도록 보호.
            var protectedSet = new HashSet<Vector2Int>
                { npcStart, axeSpot, treeSpot, fireSpot, foodSpot, enemySpot };

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

                    Color initial = isWall ? colorWall : InitialColorAt(coord);
                    SetColor(cell, initial);

                    _cellObjects[coord] = cell;
                    if (isWall) _walls.Add(coord);
                }
            }
        }

        private Color InitialColorAt(Vector2Int c)
        {
            // 거점은 각자의 색, 그 외는 빈 칸 색.
            if (c == axeSpot)   return colorAxe;
            if (c == treeSpot)  return colorTree;
            if (c == fireSpot)  return colorFire;
            if (c == foodSpot)  return colorFood;
            if (c == enemySpot) return colorEnemy;
            return colorEmpty;
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

        private void BuildNpc()
        {
            _npcGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _npcGo.name = "NPC";
            _npcGo.transform.SetParent(transform);
            _npcGo.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            _npcGo.transform.position   = ToWorld(_npcPos) + new Vector3(0f, 0.6f, 0f);
            SetColor(_npcGo, colorNpcIdle);
        }

        private void BuildLocations()
        {
            // 거점 라벨 → 격자 좌표. 행동 카탈로그에서 동적 비용 계산에 사용.
            _locationOf = new Dictionary<string, Vector2Int>
            {
                ["axe"]   = axeSpot,
                ["tree"]  = treeSpot,
                ["fire"]  = fireSpot,
                ["food"]  = foodSpot,
                ["enemy"] = enemySpot,
            };
        }

        // ─────────────────────────────────────────────────────────────
        // 2. GOAP 입력 — 초기 상태 / 목표 / 행동 카탈로그
        //
        //  사실 키 컨벤션:
        //    at_<location> : NPC 가 그 거점에 도착했는가 (한 시점에 최대 1 개 true)
        //    has_<item>    : 인벤토리 보유 여부
        //    fire_lit / enemy_alive / hungry : 세상 상태 플래그
        // ─────────────────────────────────────────────────────────────

        private void BuildInitialState()
        {
            // open-world 가정: 명시 안 한 사실은 false. 아래는 *명시적으로 true* 인 사실들만.
            _state = new WorldState()
                .With("hungry", true)
                .With("enemy_alive", true);

            // at_<location> 은 모두 false (NPC 는 거점이 아닌 npcStart 에 있음).
            // has_<item> 도 false.
        }

        private WorldState BuildGoal()
        {
            // 목표는 *부분 상태* — 명시한 사실들만 만족하면 된다. 다른 사실은 무엇이든 무관.
            return goal switch
            {
                GoalType.Eat       => new WorldState().With("hungry", false),
                GoalType.KillEnemy => new WorldState().With("enemy_alive", false),
                _                  => new WorldState(),
            };
        }

        private void BuildCatalog()
        {
            _catalog = new List<GOAPAction>();

            // ── Move(<location>) 5 종 ──
            // Pre : 이미 그 거점이 아닐 것
            // Eff : at_<this>=true, 다른 at_<other>=false 로 강제
            // Cost: 현재 위치(상태에서 추론) → 거점 좌표의 맨해튼 거리
            //       *동적 비용* — 같은 행동도 NPC 위치에 따라 비용이 달라짐.
            foreach (var pair in _locationOf)
            {
                string locKey = pair.Key;
                _catalog.Add(new GOAPAction
                {
                    Name        = $"Move({locKey})",
                    Pre         = new WorldState().With($"at_{locKey}", false),
                    Eff         = SetLocationEffect(locKey),
                    CostDynamic = (s) => MoveCost(s, locKey),
                });
            }

            // ── 상호작용 행동들 ──

            _catalog.Add(new GOAPAction
            {
                Name = "PickAxe",
                Pre  = new WorldState().With("at_axe", true).With("has_axe", false),
                Eff  = new WorldState().With("has_axe", true),
                CostStatic = 1f,
            });

            _catalog.Add(new GOAPAction
            {
                Name = "ChopWood",
                Pre  = new WorldState()
                    .With("at_tree", true)
                    .With("has_axe", true)
                    .With("has_wood", false),
                Eff  = new WorldState().With("has_wood", true),
                CostStatic = 2f,                            // 약간 비싸게 — 의미 있는 작업
            });

            _catalog.Add(new GOAPAction
            {
                Name = "LightFire",
                Pre  = new WorldState()
                    .With("at_fire", true)
                    .With("has_wood", true)
                    .With("fire_lit", false),
                // 점화 시 장작 소비 → has_wood=false 가 효과에 포함됨.
                Eff  = new WorldState().With("fire_lit", true).With("has_wood", false),
                CostStatic = 1f,
            });

            _catalog.Add(new GOAPAction
            {
                Name = "PickFood",
                Pre  = new WorldState().With("at_food", true).With("has_food", false),
                Eff  = new WorldState().With("has_food", true),
                CostStatic = 1f,
            });

            _catalog.Add(new GOAPAction
            {
                Name = "CookAndEat",
                Pre  = new WorldState()
                    .With("at_fire", true)
                    .With("fire_lit", true)
                    .With("has_food", true)
                    .With("hungry", true),
                // 음식 소비 + 배부름 — 효과가 *여러 사실을 동시에* 바꾸는 모습.
                Eff  = new WorldState().With("hungry", false).With("has_food", false),
                CostStatic = 1f,
            });

            _catalog.Add(new GOAPAction
            {
                Name = "AttackEnemy",
                Pre  = new WorldState()
                    .With("at_enemy", true)
                    .With("has_axe", true)
                    .With("enemy_alive", true),
                Eff  = new WorldState().With("enemy_alive", false),
                // 비싸게 → 가능하면 다른 길로 돌아가도록. KillEnemy 목표일 때만 유의미하게 쓰임.
                CostStatic = 3f,
            });
        }

        // 모든 at_<location> 사실을 *한 번에* 갱신하는 효과 빌더.
        // Move 행동 하나가 "여기 도착 + 다른 곳에 있던 상태 해제" 두 가지를 한꺼번에 한다.
        private WorldState SetLocationEffect(string targetKey)
        {
            var w = new WorldState();
            foreach (var k in _locationOf.Keys)
            {
                w = w.With($"at_{k}", k == targetKey);
            }
            return w;
        }

        // 동적 비용 — 현재 NPC 위치(상태에서 역추적)에서 target 까지의 맨해튼 거리.
        // 정확한 격자 거리를 쓰려면 A* 를 호출해도 되지만, 그러면 GOAP 계획 1 회당 A* 가 수십 번 호출돼
        // 학습 데모로는 너무 무거워진다. 맨해튼은 4 방향 격자에서 admissible 휴리스틱이기도 하므로
        // 비용 추정으로도 충분히 합리적.
        private float MoveCost(WorldState s, string targetKey)
        {
            Vector2Int from = NpcCurrentCoordFromState(s);
            Vector2Int to   = _locationOf[targetKey];
            return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
        }

        private Vector2Int NpcCurrentCoordFromState(WorldState s)
        {
            // 어느 거점에 있는지 사실로 추론. 어디에도 없으면(=시작 시점) npcStart.
            foreach (var k in _locationOf.Keys)
            {
                if (s.Get($"at_{k}")) return _locationOf[k];
            }
            return npcStart;
        }

        // ─────────────────────────────────────────────────────────────
        // 3. 계획 실행 코루틴
        // ─────────────────────────────────────────────────────────────

        private IEnumerator ExecutePlan()
        {
            // 계획 실패/이미 만족 케이스 — 그냥 잠시 대기 후 재시작.
            if (_plan == null || !_plan.Found || _plan.Actions.Count == 0)
            {
                _currentDoing = _plan != null && _plan.Found
                    ? "Already satisfied."
                    : "Plan failed.";
                yield return new WaitForSeconds(planFinishedPause);
                Restart();
                yield break;
            }

            // 행동 한 개씩 진행.
            for (int i = 0; i < _plan.Actions.Count; i++)
            {
                _currentStep  = i;
                var a         = _plan.Actions[i];
                _currentDoing = a.Name;

                if (a.Name.StartsWith("Move("))
                {
                    // Move 행동 → 격자 위 A* 로 한 칸씩 이동.
                    string targetKey = a.Name.Substring(5, a.Name.Length - 6);
                    yield return StartCoroutine(MoveOnGrid(_locationOf[targetKey]));
                }
                else
                {
                    // 즉시 행동 — 시각 강조용으로 잠깐 정지 + NPC 색 변화.
                    SetColor(_npcGo, colorNpcActing);
                    yield return new WaitForSeconds(actionPause);
                    SetColor(_npcGo, colorNpcIdle);
                }

                // 행동 효과 반영 — 추상 상태 + 시각 둘 다.
                _state = a.Apply(_state);
                ApplyVisualEffect(a);
            }

            _currentDoing = "Done.";
            yield return new WaitForSeconds(planFinishedPause);
            Restart();   // 다시 처음부터 (목표를 인스펙터에서 바꾸면 다음 사이클부터 반영됨).
        }

        private IEnumerator MoveOnGrid(Vector2Int target)
        {
            if (_npcPos == target) yield break;

            // 격자 A* — Move 행동 *내부* 의 길찾기.
            var path = AStarAlgorithm.FindPath(
                _graph,
                _npcPos,
                target,
                (a, b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y));

            if (path.Count < 2)
            {
                // 경로 없음(벽으로 막힘) — 학습용으로는 메시지만 남기고 통과.
                _currentDoing = $"{_currentDoing}  (no path!)";
                yield break;
            }

            // 경로 분홍색 강조 (거점 셀은 자기 색 유지 — 정보 손실 방지).
            HighlightPath(path);

            // 한 칸씩 이동 — 매 칸마다 stepInterval 대기.
            for (int i = 1; i < path.Count; i++)
            {
                _npcPos = path[i];
                _npcGo.transform.position = ToWorld(_npcPos) + new Vector3(0f, 0.6f, 0f);
                yield return new WaitForSeconds(stepInterval);
            }

            ClearPathHighlight();
        }

        private void HighlightPath(List<Vector2Int> path)
        {
            _highlightedPath = new List<Vector2Int>(path);
            foreach (var c in path)
            {
                if (IsObjectSpot(c)) continue;     // 거점 셀은 자기 색 유지
                if (_cellObjects.TryGetValue(c, out var go)) SetColor(go, colorPath);
            }
        }

        private void ClearPathHighlight()
        {
            foreach (var c in _highlightedPath)
            {
                if (IsObjectSpot(c)) continue;
                if (_walls.Contains(c)) continue;
                if (_cellObjects.TryGetValue(c, out var go)) SetColor(go, colorEmpty);
            }
            _highlightedPath.Clear();
        }

        private bool IsObjectSpot(Vector2Int c)
            => c == axeSpot || c == treeSpot || c == fireSpot || c == foodSpot || c == enemySpot;

        // 행동에 따른 셀 색 변화 — "세상이 바뀌는 모습" 을 시각화.
        private void ApplyVisualEffect(GOAPAction a)
        {
            switch (a.Name)
            {
                case "PickAxe":
                    SetCellColor(axeSpot, colorEmpty); break;
                case "ChopWood":
                    SetCellColor(treeSpot, Color.Lerp(colorTree, colorEmpty, 0.6f)); break;
                case "LightFire":
                    SetCellColor(fireSpot, colorFireLit); break;
                case "PickFood":
                    SetCellColor(foodSpot, colorEmpty); break;
                case "CookAndEat":
                    // 단순화 — 불은 그대로 둔다. CookAndEat 는 hungry=false 가 핵심.
                    break;
                case "AttackEnemy":
                    SetCellColor(enemySpot, colorEnemyDead); break;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 4. OnGUI — 우측 패널에 GOAP 정보 텍스트 출력
        // ─────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_plan == null) return;

            if (_panelStyle == null)
            {
                _panelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            }

            const float panelWidth = 380f;
            var area = new Rect(Screen.width - panelWidth - 10, 10, panelWidth, Screen.height - 20);
            GUI.Box(area, "");

            GUILayout.BeginArea(new Rect(area.x + 8, area.y + 6, area.width - 16, area.height - 12));

            GUILayout.Label("<b>GOAP — 목표 기반 자동 계획</b>", _panelStyle);
            GUILayout.Label($"목표: <b>{goal}</b>  → {GoalLabel()}", _panelStyle);
            GUILayout.Label($"NPC 위치: ({_npcPos.x},{_npcPos.y})", _panelStyle);
            GUILayout.Space(6);

            // ── 계획 통계 ──
            GUILayout.Label("─── 계획 통계 ───", _panelStyle);
            if (_plan.Found)
            {
                GUILayout.Label(
                    $"<color=#7CFC8C>계획 성공</color> · 행동 {_plan.Actions.Count} 개 · 총비용 {_plan.TotalCost:F1}",
                    _panelStyle);
            }
            else
            {
                GUILayout.Label("<color=#FF7C7C>계획 실패</color> — 행동 카탈로그로는 도달 불가", _panelStyle);
            }
            GUILayout.Label(
                $"탐색한 상태: {_plan.StatesExplored}  /  생성된 상태: {_plan.StatesGenerated}",
                _panelStyle);
            GUILayout.Space(6);

            // ── 현재 세상 상태 ──
            GUILayout.Label("─── 현재 세상 상태 ───", _panelStyle);
            DrawState(_state);
            GUILayout.Space(6);

            // ── 계획된 행동 큐 ──
            GUILayout.Label("─── 행동 큐 (▶ 진행 중) ───", _panelStyle);
            DrawPlan();

            GUILayout.EndArea();
        }

        private string GoalLabel() => goal switch
        {
            GoalType.Eat       => "hungry = false",
            GoalType.KillEnemy => "enemy_alive = false",
            _                  => "?"
        };

        // 사실 키를 보기 좋게 정렬해 ✓ / ✗ 와 함께 출력.
        private void DrawState(WorldState s)
        {
            // 정렬 — at_* 끼리, has_* 끼리, 나머지.
            var keys = new List<string>();
            foreach (var k in _locationOf.Keys) keys.Add($"at_{k}");
            keys.AddRange(new[] { "has_axe", "has_wood", "has_food" });
            keys.AddRange(new[] { "fire_lit", "hungry", "enemy_alive" });

            foreach (var k in keys)
            {
                bool v = s.Get(k);
                string mark  = v ? "<color=#7CFC8C>✓</color>" : "<color=#777777>✗</color>";
                string label = v ? $"<color=#FFFFFF>{k}</color>" : $"<color=#9A9A9A>{k}</color>";
                GUILayout.Label($"  {mark}  {label}", _panelStyle);
            }
        }

        private void DrawPlan()
        {
            if (!_plan.Found || _plan.Actions.Count == 0)
            {
                GUILayout.Label("  (no actions)", _panelStyle);
                return;
            }

            for (int i = 0; i < _plan.Actions.Count; i++)
            {
                var a       = _plan.Actions[i];
                bool active = (i == _currentStep);
                bool done   = (i < _currentStep);

                string prefix =
                    active ? "<color=#FFE066>▶</color>" :
                    done   ? "<color=#7CFC8C>✓</color>" :
                             "<color=#7A7A7A> </color>";

                string color = active ? "#FFFFFF" : (done ? "#7CFC8C" : "#9A9A9A");
                GUILayout.Label(
                    $"  {prefix} {i + 1,2}. <color={color}>{a.Name}</color>  " +
                    $"<color=#7A7A7A>(c={a.Cost(_state):F1})</color>",
                    _panelStyle);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 헬퍼
        // ─────────────────────────────────────────────────────────────

        private Vector3 ToWorld(Vector2Int coord)
            => new Vector3(coord.x * cellSize, 0f, coord.y * cellSize);

        private void SetCellColor(Vector2Int coord, Color c)
        {
            if (_cellObjects.TryGetValue(coord, out var go)) SetColor(go, c);
        }

        private static void SetColor(GameObject go, Color color)
        {
            if (go != null && go.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = color;
            }
        }
    }
}
