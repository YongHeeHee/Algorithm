using System.Collections;
using System.Collections.Generic;
using Algorithms.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Algorithms.AI
{
    /// <summary>
    /// =====================================================================
    ///  TicTacToe + MCTS 시각화 데모 (Unity 용)
    /// =====================================================================
    ///
    /// ▶ Minimax 시각화와 무엇이 다른가
    ///   - Minimax 는 후보 수마다 *정확한 점수* 를 한 번 계산하면 끝 (deterministic).
    ///   - MCTS 는 iteration 을 돌릴수록 통계가 점점 진해진다 (anytime).
    ///     → 그래서 시각화가 "후보 차례차례 깜빡" 이 아닌 "전체 진행도" 가 자연스럽다.
    ///
    /// ▶ 이 Visualizer 의 시각화 방식
    ///   1) AI 차례가 되면 MCTS Searcher 를 만들고, iteration 을 *chunk 단위로* 돌린다.
    ///   2) 매 chunk 가 끝날 때마다 루트 자식 후보들의 visit 비율을 *노란색 강도* 로 칠한다.
    ///      visit 이 가장 많은 칸이 가장 진한 노랑.
    ///   3) 사람 눈에는 "여러 칸이 동시에 익어가다가 한 칸이 부각되는" 모양으로 보인다.
    ///   4) 모든 iteration 이 끝나면 best 를 골라 O(빨강) 로 둔다.
    ///
    /// ▶ 색상 의미
    ///     옅은 회색       : 빈 칸
    ///     파랑           : X (사람)
    ///     빨강           : O (AI, MCTS)
    ///     노랑 (강도 가변): MCTS 가 *지금까지* 그 칸을 살펴본 비율 (visits / max visits)
    ///     초록 깜빡       : 게임 종료 시 승리 라인
    ///
    /// ▶ 핵심 학습 포인트
    ///   - `Iterations` 를 100 → 1000 → 5000 으로 올려보며 답이 *수렴* 하는지 확인.
    ///     적은 iteration 에서는 운에 따라 약한 수가 잠깐 1 위가 되기도 한다.
    ///   - `Random Seed` 로 재현성 확보. 같은 시드 + 같은 iteration → 같은 답.
    ///   - 콘솔 로그에서 후보별 (Visits, WinRate) 가 표시된다 → 통계적 의사결정의 흔적.
    ///   - `Ai Vs Ai` 모드에서 양쪽 다 MCTS → 보통 무승부 (Minimax 와 같은 결과, 다른 길).
    /// </summary>
    public class MCTSVisualizer : MonoBehaviour, IAlgorithmDemo
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
        [Tooltip("켜면 사람 입력을 받지 않고 AI 끼리 대국.")]
        [SerializeField] private bool aiVsAi = false;

        [Header("MCTS 설정")]
        [Tooltip("한 수당 수행할 MCTS iteration 수. 100~5000 권장. 클수록 강하지만 느림.")]
        [SerializeField] private int iterations = 1000;
        [Tooltip("0 이면 비결정적(매 게임 다른 답). 0 이 아니면 시드 고정 → 재현성 확보.")]
        [SerializeField] private int randomSeed = 0;
        [Tooltip("시각화를 위해 iteration 을 몇 단계로 쪼갤지. 클수록 부드러운 애니메이션.")]
        [Range(1, 50)]
        [SerializeField] private int visualizationSteps = 20;
        [Tooltip("각 chunk 사이 대기 시간(초). 0 으로 두면 즉시 결과만 표시.")]
        [SerializeField] private float chunkDelay = 0.06f;
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

        private readonly Dictionary<(int r, int c), GameObject> _cellObjects = new();
        private TicTacToeBoard _board;
        private Player _aiPlayer;
        private Player _humanPlayer;
        private bool _aiThinking;
        private bool _gameOver;
        private Camera _camera;
        private System.Random _rng;

        // ─────────────────────────────────────────────────────────────
        // Unity 라이프사이클
        // ─────────────────────────────────────────────────────────────

        private void Start() => Restart();

        public void Restart()
        {
            // [1] 진행 중인 코루틴 중단 + 이전 셀 정리.
            StopAllCoroutines();
            foreach (Transform child in transform) Destroy(child.gameObject);
            _cellObjects.Clear();

            _board       = new TicTacToeBoard();
            _aiPlayer    = aiPlaysFirst ? Player.X : Player.O;
            _humanPlayer = aiPlaysFirst ? Player.O : Player.X;
            _aiThinking  = false;
            _gameOver    = false;
            _camera      = Camera.main;
            _rng         = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);

            BuildBoardCells();
            StartCoroutine(GameLoopKick());
        }

        // ─────────────────────────────────────────────────────────────
        // 1. 보드 셀 생성 (TicTacToeVisualizer 와 동일 패턴)
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
        // 2. 게임 루프 (TicTacToeVisualizer 와 동일 구조)
        // ─────────────────────────────────────────────────────────────

        private IEnumerator GameLoopKick()
        {
            if (aiVsAi || _board.Current == _aiPlayer)
            {
                yield return StartCoroutine(AiTurn());
            }
        }

        private void Update()
        {
            if (_gameOver || _aiThinking || aiVsAi) return;
            if (_board.Current != _humanPlayer) return;

            // 신규 Input System 사용. Player Settings 의 Active Input Handling 이
            // "Input System Package (New)" 또는 "Both" 일 때만 동작.
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (TryGetClickedCell(mouse, out var coord))
                {
                    if (_board.IsEmpty(coord.r, coord.c))
                    {
                        PlaceMove(coord.r, coord.c, _humanPlayer);
                        if (!_gameOver) StartCoroutine(AiTurn());
                    }
                }
            }
        }

        private bool TryGetClickedCell(Mouse mouse, out (int r, int c) coord)
        {
            coord = (-1, -1);
            if (_camera == null) return false;

            Vector2 screenPos = mouse.position.ReadValue();
            var ray = _camera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit))
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
        // 3. AI 차례 — chunk 단위 MCTS + 진행도 시각화
        // ─────────────────────────────────────────────────────────────

        private IEnumerator AiTurn()
        {
            _aiThinking = true;

            // aiVsAi 면 게임 끝날 때까지 두 AI 가 번갈아 둔다.
            do
            {
                if (_gameOver) break;

                Player movingAi = _board.Current;
                var stats = new MCTSAlgorithm.SearchStats();

                // [1] Searcher 생성 — 현재 보드 상태에서 새 트리를 시작한다.
                //     (실전 봇은 '이전 트리 재사용' 으로 더 빠르게 가지만, 학습용으론 매 수마다 fresh 트리가 명확.)
                var searcher = new MCTSAlgorithm.Searcher(_board, movingAi, _rng);

                // [2] 전체 iterations 를 visualizationSteps 등분해서 chunk 단위로 돌린다.
                //     매 chunk 가 끝날 때 후보별 visit 비율을 노란색 강도로 표시.
                int totalIters = Mathf.Max(1, iterations);
                int steps      = Mathf.Max(1, visualizationSteps);
                int baseChunk  = totalIters / steps;
                int remainder  = totalIters % steps;
                var snapshot   = new List<MCTSAlgorithm.ScoredMove>();

                for (int s = 0; s < steps; s++)
                {
                    // 마지막 chunk 에 남는 iteration 을 몰아 넣는다 (반올림 오차 흡수).
                    int thisChunk = baseChunk + (s < remainder ? 1 : 0);
                    if (thisChunk <= 0) continue;

                    // [2-a] chunk 만큼 iteration 추가.
                    searcher.RunIterations(thisChunk, stats);

                    // [2-b] 현재 후보 통계 읽기.
                    searcher.GetRootCandidates(snapshot);

                    // [2-c] visit 의 max 로 정규화 → 0..1 의 t 값 → 회색→노랑 보간.
                    int maxVisits = 0;
                    foreach (var c in snapshot) if (c.Visits > maxVisits) maxVisits = c.Visits;
                    foreach (var c in snapshot)
                    {
                        if (!_board.IsEmpty(c.Row, c.Col)) continue;
                        float t = maxVisits == 0 ? 0f : (float)c.Visits / maxVisits;
                        var col = Color.Lerp(colorEmpty, colorThinking, t);
                        PaintCell(c.Row, c.Col, col);
                    }

                    if (chunkDelay > 0f) yield return new WaitForSeconds(chunkDelay);
                }

                // [3] 최종 best 결정 + 콘솔 로그 (학습용 정보).
                var bestMove = searcher.BestMove();

                Debug.Log(
                    $"[MCTS] {movingAi} 차례 — Best=({bestMove.row},{bestMove.col}), " +
                    $"Iterations={stats.Iterations}, RolloutMoves={stats.TotalRolloutMoves}, " +
                    $"TreeNodes={stats.TreeNodeCount}");
                foreach (var c in snapshot)
                {
                    Debug.Log(
                        $"  · ({c.Row},{c.Col}) → visits={c.Visits}, winRate={c.WinRate * 100f:F1}%");
                }

                // [4] 시각화 색 해제 — 빈 칸들을 다시 옅은 회색으로.
                foreach (var c in snapshot)
                {
                    if (_board.IsEmpty(c.Row, c.Col)) PaintCell(c.Row, c.Col, colorEmpty);
                }

                // [5] 실제 수.
                if (bestMove.row < 0) break;
                PlaceMove(bestMove.row, bestMove.col, movingAi);

                yield return new WaitForSeconds(postMoveDelay);
            }
            while (aiVsAi && !_gameOver);

            _aiThinking = false;
        }

        // ─────────────────────────────────────────────────────────────
        // 4. 수 적용 + 종료 검사 (TicTacToeVisualizer 와 동일)
        // ─────────────────────────────────────────────────────────────

        private void PlaceMove(int row, int col, Player player)
        {
            _board.MakeMove(row, col);
            PaintCell(row, col, player == Player.X ? colorX : colorO);

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

            var line = _board.WinningLine();
            if (line.Count > 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    foreach (var (r, c) in line) PaintCell(r, c, colorWinningLine);
                    yield return new WaitForSeconds(0.25f);
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
        // 5. 헬퍼
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
            if (cell.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = color;
            }
        }
    }
}
