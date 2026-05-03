using System.Collections;
using System.Collections.Generic;
using Algorithms.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Algorithms.AI
{
    /// <summary>
    /// =====================================================================
    ///  TicTacToe + Minimax 시각화 데모 (Unity 용)
    /// =====================================================================
    ///
    /// ▶ 사용 방법
    ///   1) 빈 GameObject 를 만들고 이 컴포넌트를 붙인다.
    ///   2) (선택) Cell Prefab 슬롯에 Cube/Quad 같은 메시 프리팹을 넣는다.
    ///        - 비워두면 기본 Cube 가 자동 생성되어 바로 실행된다.
    ///   3) 카메라는 그리드를 위에서 내려다보는 각도가 좋다 (예: y=8, 회전 x=90°).
    ///   4) Play 후 빈 칸을 *마우스 좌클릭* → X 가 놓이고, AI 가 잠시 '생각' 한 뒤 O 를 둔다.
    ///
    /// ▶ 색상 의미
    ///     옅은 회색 : 빈 칸
    ///     파랑      : X (사람)
    ///     빨강      : O (AI)
    ///     노랑 깜빡 : AI 가 *지금 평가 중인* 후보 칸 (Minimax thinking)
    ///     초록 깜빡 : 게임 종료 시 승리 라인
    ///
    /// ▶ 핵심 학습 포인트
    ///   - AI 가 사람을 이기지 못한다 = Minimax 가 *완벽히 작동* 한다는 증거.
    ///     틱택토는 양쪽이 최선을 두면 항상 무승부. 사람이 실수해야 AI 가 이긴다.
    ///   - Inspector 의 `Use Alpha Beta` 토글을 끄고 켜며 콘솔의 NodesExplored 비교.
    ///     같은 결과라도 노드 수가 수천 → 수백 수준으로 줄어드는 것을 확인할 수 있다.
    ///   - `Ai Plays First` 를 켜면 AI 가 X 로 시작 (= 최선의 시작 수가 무엇인지 시각적으로 확인).
    ///   - `Ai Vs Ai` 를 켜면 양쪽이 모두 Minimax → 항상 무승부 (틱택토의 유명한 정리 시각적 확인).
    /// </summary>
    public class TicTacToeVisualizer : MonoBehaviour, IAlgorithmDemo
    {
        // ─────────────────────────────────────────────────────────────
        // 인스펙터 노출 설정
        // ─────────────────────────────────────────────────────────────

        [Header("타일 프리팹 / 셀 간격")]
        [Tooltip("비워두면 기본 Cube 가 자동 생성됩니다.")]
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private float cellSize = 1.1f;

        [Header("플레이어 설정")]
        [Tooltip("켜면 AI 가 X 로 먼저 시작. 기본값(끔)은 사람이 X.")]
        [SerializeField] private bool aiPlaysFirst = false;
        [Tooltip("켜면 사람 입력을 받지 않고 AI 끼리 대국 → 항상 무승부.")]
        [SerializeField] private bool aiVsAi = false;

        [Header("Minimax 설정")]
        [Tooltip("끄면 순수 Minimax(모든 노드 탐색). 켜면 Alpha-Beta 가지치기.")]
        [SerializeField] private bool useAlphaBeta = true;
        [Tooltip("AI 가 후보 수를 평가할 때 노란색 깜빡임 시간(초). 0 이면 바로 둔다.")]
        [SerializeField] private float thinkStepDelay = 0.18f;
        [Tooltip("AI 가 수를 둔 뒤 다음 차례까지의 여유 시간(초).")]
        [SerializeField] private float postMoveDelay = 0.25f;

        [Header("색상")]
        [SerializeField] private Color colorEmpty       = new Color(0.85f, 0.85f, 0.85f);
        [SerializeField] private Color colorX           = new Color(0.30f, 0.55f, 1.00f);
        [SerializeField] private Color colorO           = new Color(1.00f, 0.30f, 0.30f);
        [SerializeField] private Color colorThinking    = new Color(1.00f, 0.90f, 0.20f);
        [SerializeField] private Color colorWinningLine = new Color(0.20f, 0.90f, 0.30f);

        // ─────────────────────────────────────────────────────────────
        // 내부 상태
        // ─────────────────────────────────────────────────────────────

        // 좌표((row, col)) → 그 칸의 GameObject. 색을 갱신할 때 사용.
        private readonly Dictionary<(int r, int c), GameObject> _cellObjects = new();

        // 게임 상태 (논리 모델). 시각화는 이 보드를 따라간다.
        private TicTacToeBoard _board;

        // AI 가 어느 표식으로 두는가. aiPlaysFirst 에 따라 X 또는 O.
        private Player _aiPlayer;
        private Player _humanPlayer;

        // AI 가 코루틴으로 '생각' 중인 동안 사람 입력을 막기 위한 플래그.
        private bool _aiThinking;

        // 게임 종료 후엔 클릭/입력 무시.
        private bool _gameOver;

        // 메인 카메라 캐시 (Update 안에서 매 프레임 찾지 않도록).
        private Camera _camera;

        // ─────────────────────────────────────────────────────────────
        // Unity 라이프사이클
        // ─────────────────────────────────────────────────────────────

        private void Start()
        {
            // 처음 시작 = '재시작' 과 동일한 흐름이므로 Restart() 한 번 호출로 통합.
            Restart();
        }

        /// <summary>
        /// 게임을 처음 상태로 되돌리고 다시 시작한다.
        /// AlgorithmDemoUI 의 Restart 버튼이 이 메서드를 호출한다.
        /// </summary>
        public void Restart()
        {
            // [1] 진행 중인 코루틴이 있으면 중단 (AI 사고 중에 누른 경우 대비).
            StopAllCoroutines();

            // [2] 이전 셀 GameObject + 내부 상태 정리.
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            _cellObjects.Clear();

            _board       = new TicTacToeBoard();
            _aiPlayer    = aiPlaysFirst ? Player.X : Player.O;
            _humanPlayer = aiPlaysFirst ? Player.O : Player.X;
            _aiThinking  = false;
            _gameOver    = false;
            _camera      = Camera.main;

            // [3] 보드 셀 시각화 생성 + 첫 차례 처리.
            BuildBoardCells();
            StartCoroutine(GameLoopKick());
        }

        // ─────────────────────────────────────────────────────────────
        // 1. 보드 셀 생성
        //    BFSGridVisualizer 와 동일한 패턴 — Cube 자동 생성 + Renderer 의 인스턴스 머티리얼.
        // ─────────────────────────────────────────────────────────────

        private void BuildBoardCells()
        {
            for (int r = 0; r < TicTacToeBoard.Size; r++)
            {
                for (int c = 0; c < TicTacToeBoard.Size; c++)
                {
                    GameObject cell;
                    var pos = new Vector3(c * cellSize, 0, r * cellSize);

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
                    cell.name = $"Cell ({r},{c})";

                    SetCellColor(cell, colorEmpty);
                    _cellObjects[(r, c)] = cell;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 2. 게임 루프
        //    "사람 차례 → 클릭 대기, AI 차례 → AI 코루틴" 으로 단순 분기.
        //    aiVsAi 모드에선 사람 차례를 건너뛰고 AI 가 양쪽 다 둔다.
        // ─────────────────────────────────────────────────────────────

        private IEnumerator GameLoopKick()
        {
            // 첫 차례가 AI 면 즉시 AI 가 둔다.
            // (aiPlaysFirst = true 또는 aiVsAi = true 일 때)
            if (aiVsAi || _board.Current == _aiPlayer)
            {
                yield return StartCoroutine(AiTurn());
            }
        }

        private void Update()
        {
            // 사람 차례가 아닐 때(또는 게임 종료/AI vs AI/AI 사고 중) 입력 무시.
            if (_gameOver || _aiThinking || aiVsAi) return;
            if (_board.Current != _humanPlayer) return;

            // 좌클릭 검사 → 그 위치의 셀 찾기 → 합법이면 둔다.
            // ※ 신규 Input System 사용. 프로젝트 Player Settings 의 'Active Input Handling' 이
            //   "Input System Package (New)" 또는 "Both" 로 설정돼 있어야 동작한다.
            //   (구 Input 은 InvalidOperationException 으로 실패 → 이 코드는 신규 API 만 사용.)
            var mouse = Mouse.current;
            if (mouse == null) return;  // 마우스 없는 환경(터치 전용 등) 안전 가드.

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (TryGetClickedCell(mouse, out var coord))
                {
                    if (_board.IsEmpty(coord.r, coord.c))
                    {
                        PlaceMove(coord.r, coord.c, _humanPlayer);
                        // 사람 수가 끝났으면 AI 차례 시작.
                        if (!_gameOver) StartCoroutine(AiTurn());
                    }
                }
            }
        }

        private bool TryGetClickedCell(Mouse mouse, out (int r, int c) coord)
        {
            coord = (-1, -1);
            if (_camera == null) return false;

            // 마우스 위치에서 광선 → Collider 가 있는 셀에 부딪히면 좌표 회수.
            // Cube primitive 는 BoxCollider 가 자동 포함돼 있어 별도 셋업 불필요.
            // ※ 신규 Input System: Vector2 좌표를 ReadValue() 로 받는다 (구 API 의 Input.mousePosition 대체).
            Vector2 screenPos = mouse.position.ReadValue();
            var ray = _camera.ScreenPointToRay(screenPos);
            // UnityEngine. 명시 — Algorithms.Physics 네임스페이스(AABB) 와 충돌 방지.
            if (UnityEngine.Physics.Raycast(ray, out var hit))
            {
                foreach (var kv in _cellObjects)
                {
                    if (kv.Value == hit.collider.gameObject)
                    {
                        coord = kv.Key;
                        return true;
                    }
                }
            }
            return false;
        }

        // ─────────────────────────────────────────────────────────────
        // 3. AI 차례 — Minimax 호출 + 후보 평가 시각화
        // ─────────────────────────────────────────────────────────────

        private IEnumerator AiTurn()
        {
            _aiThinking = true;

            // aiVsAi 모드면 게임 끝날 때까지 두 AI 가 번갈아 둔다.
            // 일반 모드면 한 수만 두고 빠져나간다.
            do
            {
                if (_gameOver) break;

                // [1] 현재 차례인 플레이어를 'AI 입장' 으로 본다.
                //     aiVsAi 모드에서는 매 턴 시점의 Current 가 곧 그 턴의 AI.
                Player movingAi = _board.Current;

                // [2] Minimax 실행 + 통계/후보 점수 수집.
                //     candidates 는 시각화 측에서 후보 수마다 점수를 콘솔에 표시하는 데 사용.
                var stats      = new MinimaxAlgorithm.SearchStats();
                var candidates = new List<MinimaxAlgorithm.ScoredMove>();

                var bestMove = MinimaxAlgorithm.FindBestMove(
                    _board,
                    movingAi,
                    useAlphaBeta,
                    candidates,
                    stats);

                // [3] AI 사고 시각화 — 후보 수마다 잠깐 노란색으로 깜빡인다.
                //     "AI 가 어느 칸을 어떻게 평가했는지" 를 직관적으로 보여주기 위함.
                if (thinkStepDelay > 0f)
                {
                    foreach (var cand in candidates)
                    {
                        // 빈 칸만 깜빡 — 이미 둬진 칸은 이론상 후보에 들어가지 않으므로 안전 검사.
                        if (!_board.IsEmpty(cand.Row, cand.Col)) continue;

                        PaintCell(cand.Row, cand.Col, colorThinking);
                        yield return new WaitForSeconds(thinkStepDelay);
                        PaintCell(cand.Row, cand.Col, colorEmpty);
                    }
                }

                // [4] 통계 + 후보 점수 콘솔 출력 (학습용 핵심 정보).
                Debug.Log(
                    $"[Minimax] {movingAi} 차례 — Best=({bestMove.row},{bestMove.col}), " +
                    $"Nodes={stats.NodesExplored}, Cutoffs={stats.Cutoffs}, " +
                    $"AlphaBeta={(useAlphaBeta ? "ON" : "OFF")}");
                foreach (var cand in candidates)
                {
                    Debug.Log($"  · ({cand.Row},{cand.Col}) → score = {cand.Score}");
                }

                // [5] 실제 수를 둔다.
                if (bestMove.row < 0)
                {
                    // 둘 수 없는 경우 (이미 종료된 게임). 안전 종료.
                    break;
                }
                PlaceMove(bestMove.row, bestMove.col, movingAi);

                yield return new WaitForSeconds(postMoveDelay);
            }
            while (aiVsAi && !_gameOver);

            _aiThinking = false;
        }

        // ─────────────────────────────────────────────────────────────
        // 4. 수 적용 + 종료 검사
        // ─────────────────────────────────────────────────────────────

        private void PlaceMove(int row, int col, Player player)
        {
            // [1] 보드에 반영.
            _board.MakeMove(row, col);

            // [2] 시각화: X 는 파랑, O 는 빨강.
            PaintCell(row, col, player == Player.X ? colorX : colorO);

            // [3] 결과 검사. 끝났으면 승리 라인 강조 + 종료 플래그.
            var result = _board.Result();
            if (result != GameResult.Ongoing)
            {
                _gameOver = true;
                StartCoroutine(HighlightEndGame(result));
            }
        }

        private IEnumerator HighlightEndGame(GameResult result)
        {
            string msg = result switch
            {
                GameResult.XWins => "X 승리!",
                GameResult.OWins => "O 승리!",
                GameResult.Draw  => "무승부.",
                _                => "?"
            };
            Debug.Log($"[Game Over] {msg}");

            // 승리 라인이 있으면 잠깐 초록으로 강조 (무승부엔 라인 없음).
            var line = _board.WinningLine();
            if (line.Count > 0)
            {
                // 두세 번 깜빡거리도록 반복.
                for (int i = 0; i < 3; i++)
                {
                    foreach (var (r, c) in line) PaintCell(r, c, colorWinningLine);
                    yield return new WaitForSeconds(0.25f);
                    // 원래 색(X/O 색) 으로 되돌리기.
                    foreach (var (r, c) in line)
                    {
                        var p = _board.Get(r, c);
                        PaintCell(r, c, p == Player.X ? colorX : colorO);
                    }
                    yield return new WaitForSeconds(0.15f);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 5. 헬퍼 (BFS / DFS Visualizer 와 동일 패턴)
        // ─────────────────────────────────────────────────────────────

        private void PaintCell(int row, int col, Color color)
        {
            if (_cellObjects.TryGetValue((row, col), out var cell))
            {
                SetCellColor(cell, color);
            }
        }

        private static void SetCellColor(GameObject cell, Color color)
        {
            // Renderer.material 은 호출 시 인스턴스 머티리얼을 자동 생성하므로
            // 셀마다 독립된 색상을 가질 수 있다.
            // sharedMaterial 을 직접 수정하면 같은 머티리얼을 쓰는 모든 셀이 동시에 바뀌어 버린다.
            if (cell.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = color;
            }
        }
    }
}
