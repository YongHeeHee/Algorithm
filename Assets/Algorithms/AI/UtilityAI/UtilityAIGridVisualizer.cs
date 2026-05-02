using System.Collections;
using System.Collections.Generic;
using Algorithms.Common;
using Algorithms.Search;   // AStarAlgorithm + WeightedGraph 재사용 — UAI × A* 협업
using UnityEngine;

namespace Algorithms.AI
{
    /// <summary>
    /// =====================================================================
    ///  Utility AI 시각화 데모 (Unity 용)
    /// =====================================================================
    ///
    /// ▶ 데모 시나리오 — "욕구 기반 NPC"
    ///   격자 위에 NPC + 4 개의 거점 + 1 마리의 적이 있다.
    ///     - Food(주황) : 음식 — Eat 행동으로 hunger 감소
    ///     - Bed(파랑)  : 침대 — Sleep 행동으로 energy 회복
    ///     - Social(초록): 친구 — Socialize 행동으로 social 회복
    ///     - Haven(흰색): 안전지대 — FleeToSafety 행동으로 safety 회복 (적이 못 따라옴)
    ///     - Enemy(빨강): 주기적으로 NPC 쪽으로 다가옴. 가까울수록 safety 욕구 폭증.
    ///
    ///   NPC 는 4 개 욕구 (`hunger`, `energy`, `social`, `safety`) 를 가진다. 시간이 지나며
    ///   각 욕구가 변화 — hunger/social 는 *상승* (커질수록 결핍), energy/safety 는 *하강*
    ///   (작아질수록 결핍). 매 tick UtilityAIBrain 이 모든 행동의 점수를 평가해 *최고점* 행동을 선택.
    ///
    /// ▶ 핵심 메시지
    ///   1) GOAP 처럼 *사슬을 계획* 하지 않는다. 매 tick 처음부터 다시 결정 → 환경 변화에 즉각 반응.
    ///   2) 점수 = Consideration 들의 *곱*. 한 사항이라도 0 이면 그 행동의 점수도 0.
    ///   3) 적이 가까워지면 safety Consideration 점수가 폭증 → *식사 중에도 도망* 으로 인터럽트.
    ///      이 "환경 변화 → 즉각 행동 전환" 모습이 UAI 의 시그니처.
    ///
    /// ▶ UtilityAI × A* 협업
    ///   1) 추상 결정 — 어떤 행동을 할지 (Brain.Decide).
    ///   2) Move 단계 — 결정된 행동의 목표 거점까지 격자 A* 길찾기.
    ///   GOAP × A* 와 같은 분업 패턴이지만, 결정 주체가 *상태 공간 탐색* → *점수 함수 평가* 로 바뀐다.
    ///
    /// ▶ 색상 의미
    ///     셀
    ///       옅은 회색  : 빈 칸
    ///       검정       : 벽 (이동 불가)
    ///       분홍       : 현재 진행 중인 격자 경로 (Move 중)
    ///     거점 (특정 좌표의 색)
    ///       주황       : Food
    ///       파랑       : Bed
    ///       초록       : Social
    ///       흰색       : Haven (안전지대)
    ///       빨강       : Enemy
    ///     NPC
    ///       파랑       : 이동 중 / 대기
    ///       노랑       : 거점 도착 후 욕구 충족 행동 수행 중
    ///
    /// ▶ 우측 OnGUI 패널 — 모든 UAI 정보가 한 화면에
    ///   - 현재 선택된 행동 + 인터럽트 표시
    ///   - 욕구 4 개 (hunger / energy / social / safety) 의 현재 값을 바로
    ///   - 행동 카탈로그 — *각 행동의 점수* 가 막대로. 선택된 것은 강조.
    ///   - 행동을 펼치면 *각 Consideration 의 raw / normalized / score* 까지 — 왜 이 점수인지가 한눈에.
    /// </summary>
    public class UtilityAIGridVisualizer : MonoBehaviour, IAlgorithmDemo
    {
        // ─────────────────────────────────────────────────────────────
        // 인스펙터 노출 설정
        // ─────────────────────────────────────────────────────────────

        [Header("그리드 크기")]
        [SerializeField] private int width  = 13;
        [SerializeField] private int height = 9;
        [SerializeField] private float cellSize = 1.0f;

        [Header("거점 좌표")]
        [SerializeField] private Vector2Int npcStart   = new Vector2Int(6, 4);
        [SerializeField] private Vector2Int foodSpot   = new Vector2Int(2, 7);
        [SerializeField] private Vector2Int bedSpot    = new Vector2Int(2, 1);
        [SerializeField] private Vector2Int socialSpot = new Vector2Int(10, 7);
        [SerializeField] private Vector2Int havenSpot  = new Vector2Int(10, 1);
        [SerializeField] private Vector2Int enemyStart = new Vector2Int(0, 4);

        [Header("욕구 변화 속도 (단위/초)")]
        [Tooltip("hunger 가 1 초당 얼마나 증가하는가. 100 에 가까워질수록 Eat 점수 ↑.")]
        [SerializeField] private float hungerRate = 7f;
        [Tooltip("energy 가 1 초당 얼마나 감소하는가. 0 에 가까워질수록 Sleep 점수 ↑.")]
        [SerializeField] private float energyRate = 5f;
        [Tooltip("social 이 1 초당 얼마나 감소하는가.")]
        [SerializeField] private float socialRate = 4f;

        [Header("적 동작")]
        [Tooltip("적이 NPC 쪽으로 한 칸 다가오는 주기(초).")]
        [SerializeField] private float enemyStepInterval = 1.4f;
        [Tooltip("적이 NPC 와 이만큼 가까워지면 safety 욕구가 0 으로 떨어진다.")]
        [SerializeField] private int enemyDangerRange = 3;

        [Header("실행 속도")]
        [Tooltip("NPC 가 한 칸 이동하는 데 걸리는 시간(초).")]
        [SerializeField] private float stepInterval = 0.18f;
        [Tooltip("거점에 도착해 욕구 충족 행동을 수행할 때 강조 시간(초).")]
        [SerializeField] private float actionPause = 0.2f;
        [Tooltip("Brain.Decide 호출 주기(초). 너무 짧으면 flip-flop, 너무 길면 반응 둔함.")]
        [SerializeField] private float decisionInterval = 0.15f;

        [Header("UAI 파라미터")]
        [Tooltip("진행 중 행동에서 다른 행동으로 바꾸려면 이만큼 더 높은 점수가 필요. flip-flop 방지.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float interruptThreshold = 0.06f;

        [Header("미로 (벽 비율)")]
        [Range(0f, 0.3f)]
        [SerializeField] private float wallRatio = 0.06f;
        [SerializeField] private int randomSeed = 11;

        [Header("색상 — 셀")]
        [SerializeField] private Color colorEmpty = new Color(0.85f, 0.85f, 0.85f);
        [SerializeField] private Color colorWall  = new Color(0.10f, 0.10f, 0.10f);
        [SerializeField] private Color colorPath  = new Color(1.00f, 0.65f, 0.85f);

        [Header("색상 — 거점")]
        [SerializeField] private Color colorFood   = new Color(1.00f, 0.55f, 0.20f);
        [SerializeField] private Color colorBed    = new Color(0.30f, 0.55f, 1.00f);
        [SerializeField] private Color colorSocial = new Color(0.30f, 0.75f, 0.45f);
        [SerializeField] private Color colorHaven  = new Color(0.95f, 0.95f, 0.95f);
        [SerializeField] private Color colorEnemy  = new Color(0.85f, 0.20f, 0.30f);

        [Header("색상 — NPC")]
        [SerializeField] private Color colorNpcIdle   = new Color(0.30f, 0.55f, 1.00f);
        [SerializeField] private Color colorNpcActing = new Color(1.00f, 0.90f, 0.20f);

        // ─────────────────────────────────────────────────────────────
        // 내부 상태
        // ─────────────────────────────────────────────────────────────

        // 격자 / 그래프
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

        // Enemy
        private GameObject _enemyGo;
        private Vector2Int _enemyPos;
        private Coroutine  _enemyCo;

        // 욕구 (0~100)
        private float _hunger;     // 클수록 결핍
        private float _energy;     // 작을수록 결핍
        private float _social;     // 작을수록 결핍
        private float _safety;     // 작을수록 결핍

        // UAI
        private UtilityAIBrain     _brain;
        private UtilityContext     _ctx;
        private DecisionSnapshot   _lastSnap;
        private UtilityAction      _currentAction;
        private string             _currentDoing = "";
        private bool               _isMoving;
        private bool               _isAtTarget;

        // 행동 — 빠른 lookup 용
        private UtilityAction _aEat, _aSleep, _aSocialize, _aFlee, _aWander;

        // 행동 → 거점 좌표 (Wander 는 null)
        private Dictionary<UtilityAction, Vector2Int?> _actionTarget;

        // 디버그 — 펼친 행동
        private string _expandedActionName = "";

        // 시각화 보조
        private List<Vector2Int> _highlightedPath = new();
        private GUIStyle _panelStyle;
        private GUIStyle _smallStyle;

        // ─────────────────────────────────────────────────────────────
        // Unity 라이프사이클
        // ─────────────────────────────────────────────────────────────

        private void Start() => Restart();

        public void Restart()
        {
            // [1] 진행 중 코루틴 / 자식 GameObject / 내부 상태 정리.
            StopAllCoroutines();
            _enemyCo = null;
            foreach (Transform child in transform) Destroy(child.gameObject);
            _cellObjects.Clear();
            _walls.Clear();
            _highlightedPath.Clear();

            _npcPos        = npcStart;
            _enemyPos      = enemyStart;
            _isMoving      = false;
            _isAtTarget    = false;
            _currentAction = null;
            _currentDoing  = "thinking...";
            _lastSnap      = null;

            // 욕구 초기값 — hunger/social 은 0(만족), energy/safety 는 100(만족).
            _hunger = 25f;
            _energy = 90f;
            _social = 70f;
            _safety = 100f;

            // [2] 격자 / 그래프 / NPC / 적 빌드.
            BuildGrid();
            BuildGraph();
            BuildNpc();
            BuildEnemy();

            // [3] UAI 카탈로그 + 컨텍스트 빌드.
            BuildBrain();

            // [4] 적 이동 + 메인 결정 루프 시작.
            _enemyCo = StartCoroutine(EnemyLoop());
            StartCoroutine(MainLoop());
        }

        private void Update()
        {
            // 매 프레임 욕구가 시간에 따라 변화. *결정* 은 코루틴이 따로 돌리지만, 욕구는 부드럽게.
            float dt = Time.deltaTime;
            _hunger = Mathf.Clamp(_hunger + hungerRate * dt, 0f, 100f);
            _energy = Mathf.Clamp(_energy - energyRate * dt, 0f, 100f);
            _social = Mathf.Clamp(_social - socialRate * dt, 0f, 100f);

            // safety 는 *적과의 거리* 로 결정 — 즉시 갱신 (시간 변화가 아님).
            int dist = Mathf.Abs(_npcPos.x - _enemyPos.x) + Mathf.Abs(_npcPos.y - _enemyPos.y);
            // 안전지대(haven)에 있으면 적과 무관하게 safety 회복.
            if (_npcPos == havenSpot)
            {
                _safety = Mathf.Min(_safety + 60f * dt, 100f);
            }
            else if (dist <= enemyDangerRange)
            {
                // 가까우면 빠르게 떨어진다. 거리 1 = 거의 0.
                float danger = 1f - (dist / (float)(enemyDangerRange + 1));
                _safety = Mathf.Max(_safety - danger * 80f * dt, 0f);
            }
            else
            {
                // 멀면 천천히 회복.
                _safety = Mathf.Min(_safety + 8f * dt, 100f);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 1. 격자 / 그래프 / NPC / 적
        // ─────────────────────────────────────────────────────────────

        private void BuildGrid()
        {
            Random.InitState(randomSeed);

            var protectedSet = new HashSet<Vector2Int>
                { npcStart, foodSpot, bedSpot, socialSpot, havenSpot, enemyStart };

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
            if (c == foodSpot)   return colorFood;
            if (c == bedSpot)    return colorBed;
            if (c == socialSpot) return colorSocial;
            if (c == havenSpot)  return colorHaven;
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

        private void BuildNpc()
        {
            _npcGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _npcGo.name = "NPC";
            _npcGo.transform.SetParent(transform);
            _npcGo.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            _npcGo.transform.position   = ToWorld(_npcPos) + new Vector3(0f, 0.6f, 0f);
            SetColor(_npcGo, colorNpcIdle);
        }

        private void BuildEnemy()
        {
            _enemyGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _enemyGo.name = "Enemy";
            _enemyGo.transform.SetParent(transform);
            _enemyGo.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            _enemyGo.transform.position   = ToWorld(_enemyPos) + new Vector3(0f, 0.6f, 0f);
            SetColor(_enemyGo, colorEnemy);
        }

        // ─────────────────────────────────────────────────────────────
        // 2. UAI 카탈로그
        //
        //  컨텍스트 키 컨벤션:
        //    "hunger" / "energy" / "social" / "safety"  : 0~100 욕구값
        //    "dist_food" / "dist_bed" / ... / "dist_haven" / "dist_enemy" : 격자 거리
        // ─────────────────────────────────────────────────────────────

        private void BuildBrain()
        {
            _brain = new UtilityAIBrain { InterruptThreshold = interruptThreshold };
            _ctx   = new UtilityContext();

            // 모든 행동에 공통으로 들어가는 "거리" 고려사항 — 가까울수록 점수 ↑.
            // 격자 대각선 길이로 정규화해서 (0,1) 범위.
            float maxDist = width + height;

            // ── Eat : hunger 가 클수록 + 음식까지 가까울수록 ──
            _aEat = new UtilityAction
            {
                Name = "Eat",
                Weight = 1.0f,
                Considerations =
                {
                    new Consideration {
                        Label = "hunger",
                        InputProvider = c => c.Get("hunger"),
                        InputMin = 0f, InputMax = 100f,
                        Curve = ResponseCurve.Quadratic,    // 임계 넘기면 폭발적으로 ↑
                    },
                    new Consideration {
                        Label = "거리(food)",
                        InputProvider = c => c.Get("dist_food"),
                        InputMin = 0f, InputMax = maxDist,
                        Curve = ResponseCurve.Inverse,      // 가까울수록 ↑
                    },
                },
            };

            // ── Sleep : energy 가 작을수록 + 침대까지 가까울수록 ──
            _aSleep = new UtilityAction
            {
                Name = "Sleep",
                Weight = 1.0f,
                Considerations =
                {
                    new Consideration {
                        Label = "낮은 energy",
                        InputProvider = c => c.Get("energy"),
                        InputMin = 0f, InputMax = 100f,
                        Curve = ResponseCurve.Inverse,      // 작을수록 ↑
                    },
                    new Consideration {
                        Label = "거리(bed)",
                        InputProvider = c => c.Get("dist_bed"),
                        InputMin = 0f, InputMax = maxDist,
                        Curve = ResponseCurve.Inverse,
                    },
                },
            };

            // ── Socialize : social 이 작을수록 + 친구까지 가까울수록 ──
            _aSocialize = new UtilityAction
            {
                Name = "Socialize",
                Weight = 0.9f,                              // 살짝 낮은 우선순위
                Considerations =
                {
                    new Consideration {
                        Label = "낮은 social",
                        InputProvider = c => c.Get("social"),
                        InputMin = 0f, InputMax = 100f,
                        Curve = ResponseCurve.Inverse,
                    },
                    new Consideration {
                        Label = "거리(social)",
                        InputProvider = c => c.Get("dist_social"),
                        InputMin = 0f, InputMax = maxDist,
                        Curve = ResponseCurve.Inverse,
                    },
                },
            };

            // ── FleeToSafety : safety 가 작을수록 + Haven 까지 가까울수록 ──
            //   여기에 Logistic 곡선을 써서 "임계 통과 시 폭발" 모양 — 진짜 위험할 때만 도망.
            _aFlee = new UtilityAction
            {
                Name = "FleeToSafety",
                Weight = 1.4f,                              // 생존 행동은 살짝 높은 우선순위
                Considerations =
                {
                    new Consideration {
                        Label = "낮은 safety",
                        InputProvider = c => c.Get("safety"),
                        InputMin = 0f, InputMax = 100f,
                        Curve = ResponseCurve.InverseQuadratic, // 진짜 위험할 때 폭발
                    },
                    new Consideration {
                        Label = "거리(haven)",
                        InputProvider = c => c.Get("dist_haven"),
                        InputMin = 0f, InputMax = maxDist,
                        Curve = ResponseCurve.Inverse,
                    },
                },
            };

            // ── Wander : 다른 모든 행동 점수가 낮을 때의 *기본값* ──
            //   상수에 가까운 작은 점수를 만들어 "할 게 없을 때" 채택되도록.
            _aWander = new UtilityAction
            {
                Name = "Wander",
                Weight = 0.3f,
                Considerations =
                {
                    new Consideration {
                        Label = "기본",
                        InputProvider = c => 0.5f,           // 항상 0.5
                        InputMin = 0f, InputMax = 1f,
                        Curve = ResponseCurve.Linear,
                    },
                },
            };

            _brain.AddAction(_aEat);
            _brain.AddAction(_aSleep);
            _brain.AddAction(_aSocialize);
            _brain.AddAction(_aFlee);
            _brain.AddAction(_aWander);

            _actionTarget = new Dictionary<UtilityAction, Vector2Int?>
            {
                [_aEat]       = foodSpot,
                [_aSleep]     = bedSpot,
                [_aSocialize] = socialSpot,
                [_aFlee]      = havenSpot,
                [_aWander]    = null,
            };
        }

        // 매 tick UtilityContext 를 최신값으로 갱신.
        private void UpdateContext()
        {
            _ctx.Set("hunger", _hunger);
            _ctx.Set("energy", _energy);
            _ctx.Set("social", _social);
            _ctx.Set("safety", _safety);
            _ctx.Set("dist_food",   Manhattan(_npcPos, foodSpot));
            _ctx.Set("dist_bed",    Manhattan(_npcPos, bedSpot));
            _ctx.Set("dist_social", Manhattan(_npcPos, socialSpot));
            _ctx.Set("dist_haven",  Manhattan(_npcPos, havenSpot));
            _ctx.Set("dist_enemy",  Manhattan(_npcPos, _enemyPos));
        }

        // ─────────────────────────────────────────────────────────────
        // 3. 메인 루프 — 결정 + 실행
        // ─────────────────────────────────────────────────────────────

        private IEnumerator MainLoop()
        {
            while (true)
            {
                // [a] 컨텍스트 갱신 + Brain.Decide.
                UpdateContext();
                _lastSnap = _brain.Decide(_ctx, _currentAction);

                var chosen = _lastSnap.Chosen;

                // [b] 행동이 바뀌었으면 진행 중 이동을 *인터럽트* — 코루틴 정리.
                if (chosen != _currentAction)
                {
                    _currentAction = chosen;
                    _isMoving      = false;
                    _isAtTarget    = false;
                    ClearPathHighlight();
                }

                // [c] 행동 실행.
                yield return ExecuteOneTick(chosen);

                // 다음 결정까지 대기.
                yield return new WaitForSeconds(decisionInterval);
            }
        }

        private IEnumerator ExecuteOneTick(UtilityAction action)
        {
            _currentDoing = action.Name;

            // Wander : 지금 위치 그대로 유지하고 잠깐 대기.
            if (action == _aWander)
            {
                SetColor(_npcGo, colorNpcIdle);
                yield return new WaitForSeconds(decisionInterval);
                yield break;
            }

            // 그 외 행동 — 목표 거점이 있다.
            var target = _actionTarget[action].Value;

            if (_npcPos == target)
            {
                // 거점 도착 — 욕구 충족 행동 수행.
                _isAtTarget = true;
                SetColor(_npcGo, colorNpcActing);

                // 욕구 회복은 1 tick 만에 완전 회복하지 말고 *조금씩* — 그래야
                // "거점에 머물면서 다른 욕구들이 변하는 모습" 이 잘 보인다.
                ApplyNeedGain(action, decisionInterval);

                yield return new WaitForSeconds(actionPause);
                SetColor(_npcGo, colorNpcIdle);
            }
            else
            {
                // 거점까지 *한 칸씩* 이동. 매 칸 사이 결정이 다시 돌므로,
                // 환경 변화(적 접근 등) 가 생기면 즉시 다른 행동으로 전환된다.
                yield return MoveOneStep(target);
            }
        }

        private IEnumerator MoveOneStep(Vector2Int target)
        {
            _isMoving = true;
            SetColor(_npcGo, colorNpcIdle);

            var path = AStarAlgorithm.FindPath(
                _graph,
                _npcPos,
                target,
                (a, b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y));

            if (path.Count < 2)
            {
                _currentDoing = $"{_currentDoing}  (no path!)";
                yield break;
            }

            HighlightPath(path);

            // *한 칸만* 이동 — 다음 결정 사이클이 다시 평가하도록.
            _npcPos = path[1];
            _npcGo.transform.position = ToWorld(_npcPos) + new Vector3(0f, 0.6f, 0f);
            yield return new WaitForSeconds(stepInterval);

            ClearPathHighlight();
            _isMoving = false;
        }

        // 거점에서 머무는 동안 욕구 회복. dt 는 머문 시간(초).
        private void ApplyNeedGain(UtilityAction action, float dt)
        {
            if (action == _aEat)
            {
                _hunger = Mathf.Max(_hunger - 60f * dt, 0f);
            }
            else if (action == _aSleep)
            {
                _energy = Mathf.Min(_energy + 50f * dt, 100f);
            }
            else if (action == _aSocialize)
            {
                _social = Mathf.Min(_social + 55f * dt, 100f);
            }
            else if (action == _aFlee)
            {
                // safety 회복은 Update 에서 이미 처리됨 (haven 위에 있을 때).
                _safety = Mathf.Min(_safety + 10f * dt, 100f);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 4. 적 이동 — 일정 주기로 NPC 쪽으로 한 칸 다가옴
        // ─────────────────────────────────────────────────────────────

        private IEnumerator EnemyLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(enemyStepInterval);

                // NPC 가 haven 에 있으면 적은 *후퇴* — 시작점으로 한 칸씩.
                Vector2Int target = _npcPos == havenSpot ? enemyStart : _npcPos;

                if (_enemyPos == target) continue;

                // 격자 A* 로 한 칸 이동.
                var path = AStarAlgorithm.FindPath(
                    _graph,
                    _enemyPos,
                    target,
                    (a, b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y));

                if (path.Count >= 2)
                {
                    _enemyPos = path[1];
                    _enemyGo.transform.position = ToWorld(_enemyPos) + new Vector3(0f, 0.6f, 0f);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 5. 시각화 보조 — 경로 하이라이트
        // ─────────────────────────────────────────────────────────────

        private void HighlightPath(List<Vector2Int> path)
        {
            ClearPathHighlight();
            _highlightedPath = new List<Vector2Int>(path);
            foreach (var c in path)
            {
                if (IsObjectSpot(c)) continue;
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
            => c == foodSpot || c == bedSpot || c == socialSpot || c == havenSpot;

        // ─────────────────────────────────────────────────────────────
        // 6. OnGUI — 우측 패널
        // ─────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_brain == null) return;

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
            GUILayout.Label("<b>Utility AI — 점수 함수 의사결정</b>", _panelStyle);

            // 현재 행동
            string actName = _currentAction != null ? _currentAction.Name : "(none)";
            string interrupt = (_lastSnap != null && _lastSnap.Interrupted)
                ? "  <color=#FFE066>← interrupted!</color>" : "";
            GUILayout.Label($"현재 행동: <b>{actName}</b>{interrupt}", _panelStyle);
            GUILayout.Label($"NPC ({_npcPos.x},{_npcPos.y})  /  Enemy ({_enemyPos.x},{_enemyPos.y})", _smallStyle);
            GUILayout.Space(6);

            // 욕구 바
            GUILayout.Label("─── 현재 욕구 ───", _panelStyle);
            DrawNeedBar("hunger ↑",  _hunger, 100f, "#FF8B5A", warnHigh: true);
            DrawNeedBar("energy ↓",  _energy, 100f, "#5AAEFF", warnHigh: false);
            DrawNeedBar("social ↓",  _social, 100f, "#4CC780", warnHigh: false);
            DrawNeedBar("safety ↓",  _safety, 100f, "#E8E8E8", warnHigh: false);
            GUILayout.Space(6);

            // 행동 점수
            GUILayout.Label("─── 행동 점수 (클릭하면 펼침) ───", _panelStyle);
            if (_lastSnap != null)
            {
                foreach (var s in _lastSnap.AllScores)
                {
                    DrawActionRow(s, isChosen: (_lastSnap.Chosen != null && s.Name == _lastSnap.Chosen.Name));
                }
            }

            GUILayout.EndArea();
        }

        private void DrawNeedBar(string label, float value, float max, string colorHex, bool warnHigh)
        {
            // 결핍 상태면(hunger 가 높음 or 다른 욕구가 낮음) 라벨에 경고 색.
            float ratio = Mathf.Clamp01(value / max);
            bool deficient = warnHigh ? ratio > 0.7f : ratio < 0.3f;
            string labelColor = deficient ? "#FF7C7C" : "#DDDDDD";

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color={labelColor}>{label}</color>", _smallStyle, GUILayout.Width(80));
            // 막대 그리기
            Rect r = GUILayoutUtility.GetRect(220f, 14f);
            GUI.color = new Color(0.15f, 0.15f, 0.15f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            var fill = new Rect(r.x, r.y, r.width * ratio, r.height);
            GUI.color = HexColor(colorHex);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.Label($"<color=#BBBBBB>{value:F0}</color>", _smallStyle, GUILayout.Width(40));
            GUILayout.EndHorizontal();
        }

        private void DrawActionRow(ActionScore s, bool isChosen)
        {
            // 행동 이름 + 점수 막대 + 클릭 시 펼침.
            float maxBar = 1f;   // 점수가 1 을 넘을 수 있어 (Weight > 1 인 행동) 약간 여유.
            float ratio  = Mathf.Clamp01(s.FinalScore / maxBar);

            string highlight = isChosen ? "<color=#FFE066>▶</color> " : "  ";
            string nameColor = isChosen ? "#FFE066" : "#DDDDDD";

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"{highlight}<color={nameColor}>{s.Name,-12}</color>",
                new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, alignment = TextAnchor.MiddleLeft },
                GUILayout.Width(110)))
            {
                _expandedActionName = (_expandedActionName == s.Name) ? "" : s.Name;
            }

            Rect r = GUILayoutUtility.GetRect(180f, 12f);
            GUI.color = new Color(0.15f, 0.15f, 0.15f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            var fill = new Rect(r.x, r.y, r.width * ratio, r.height);
            GUI.color = isChosen ? new Color(1f, 0.88f, 0.4f) : new Color(0.55f, 0.65f, 0.85f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.Label($"<color=#BBBBBB>{s.FinalScore:F2}</color>", _smallStyle, GUILayout.Width(50));
            GUILayout.EndHorizontal();

            // 펼친 행동만 Consideration 상세 표시.
            if (_expandedActionName == s.Name)
            {
                foreach (var c in s.Considerations)
                {
                    GUILayout.Label(
                        $"      <color=#888888>· {c.Label,-14}</color>" +
                        $"<color=#777777>raw {c.Raw,6:F1}  →  norm {c.Normalized,4:F2}  →</color>" +
                        $"  <color=#CCCCCC>{c.Score,4:F2}</color>",
                        _smallStyle);
                }
                GUILayout.Label(
                    $"      <color=#777777>곱={s.RawScore:F2}  보정={s.CompensatedScore:F2}  ×Weight {s.Weight:F1}  =  {s.FinalScore:F2}</color>",
                    _smallStyle);
            }
        }

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        // ─────────────────────────────────────────────────────────────
        // 헬퍼
        // ─────────────────────────────────────────────────────────────

        private Vector3 ToWorld(Vector2Int coord)
            => new Vector3(coord.x * cellSize, 0f, coord.y * cellSize);

        private static int Manhattan(Vector2Int a, Vector2Int b)
            => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        private static void SetColor(GameObject go, Color color)
        {
            if (go != null && go.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = color;
            }
        }
    }
}
