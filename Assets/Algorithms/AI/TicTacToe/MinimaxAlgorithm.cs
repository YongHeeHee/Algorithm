using System.Collections.Generic;

namespace Algorithms.AI
{
    /// <summary>
    /// =====================================================================
    ///  Minimax + Alpha-Beta Pruning (게임 트리 탐색)
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// 2 인 제로섬 완전정보 게임 (체스, 체커, 오목, 틱택토 …) 에서 "최선의 수" 를 찾는 알고리즘.
    ///
    /// 게임의 모든 가능한 진행을 *트리* 로 펼친다.
    ///   · 노드(Node) = 한 시점의 국면(board state)
    ///   · 간선(Edge) = 그 국면에서 가능한 한 수
    ///   · 잎(Leaf)   = 게임이 끝났거나 탐색 깊이 한도에 도달한 국면
    ///
    /// 두 종류의 노드가 번갈아 나온다:
    ///   - MAX 노드 : 내 차례 → 점수를 *최대화* 하는 자식을 고른다.
    ///   - MIN 노드 : 상대 차례 → 상대는 내 점수를 *최소화* 하는 자식을 고른다 (= 자기 입장 최대화).
    ///
    /// 잎 노드의 점수를 평가 함수로 매기고, 위로 올라가며 MAX/MIN 으로 접어 가면
    /// 루트의 best 점수와 그 점수를 만든 자식 수가 곧 "최선의 수" 다.
    ///
    /// 전제: **상대도 최선을 둔다**. Minimax 가 비관적으로 보이는 이유 — "상대 실수" 는 가정하지 않는다.
    ///
    /// BFS / DFS 와의 관계:
    ///   - 본질은 DFS. 게임 트리를 깊이 우선으로 끝까지 내려갔다가 점수를 들고 올라온다.
    ///   - 차이 : DFS 는 *방문 순서/경로* 를 반환하지만, Minimax 는 *점수와 그것을 만든 수* 를 반환한다.
    ///   - 차이 : DFS 의 그래프는 외부에 있지만, Minimax 의 트리는 *둘 수를 펼쳐가며 즉석에서 생성* 된다.
    ///
    /// 2. 동작 흐름 (재귀 DFS — 표준 형태)
    /// ---------------------------------------------------------------------
    ///   ① 종료 검사: 게임이 끝났거나 깊이 한도라면, 평가 함수로 잎 점수를 매기고 반환.
    ///   ② 합법수 열거: 현재 국면에서 둘 수 있는 모든 수를 가져온다.
    ///   ③ 각 합법수 m 에 대해:
    ///        a. 보드에 m 을 둔다 (MakeMove).
    ///        b. 재귀 호출로 자식 점수를 받는다 (이때 차례가 바뀌므로 isMax 가 반전).
    ///        c. 보드를 되돌린다 (UndoMove) — 다음 형제 가지가 깨끗한 보드에서 시작하도록.
    ///        d. MAX 노드면 best = max(best, score), MIN 노드면 best = min(best, score).
    ///   ④ best 반환.
    ///
    /// 3. Alpha-Beta 가지치기 (가속의 핵심)
    /// ---------------------------------------------------------------------
    /// Minimax 만으로는 분기 b, 깊이 d 에서 O(b^d) — 체스(b≈35, d=10) 에서는 비현실적.
    /// Alpha-Beta 는 *결과에 영향 없이* 명백히 의미 없는 가지를 잘라 낸다.
    ///
    ///   alpha = "MAX 가 지금까지 보장받은 최저 점수" (= MAX 의 안전선)
    ///   beta  = "MIN 이 지금까지 보장받은 최대 점수" (= MIN 의 안전선)
    ///
    /// 가지치기 규칙:
    ///   - MAX 노드에서 자식 점수를 받았는데 alpha &gt;= beta 가 되면 → 남은 자식은 보지 않아도 된다.
    ///     (왜? "이 가지의 결과는 MIN 이 절대 허용하지 않을 만큼 좋다" → 상대가 위 분기에서 이 길을 차단함.)
    ///   - MIN 노드도 대칭. alpha &gt;= beta 면 컷.
    ///
    /// 핵심 효과:
    ///   - 자식을 *유망한 순서로* 정렬(move ordering)하면 최선의 경우 O(b^(d/2)).
    ///   - 즉, 같은 시간에 *2 배 깊이* 탐색 가능. 체스 봇이 실용적이 되는 결정적 이유.
    ///
    /// 4. 시간 / 공간 복잡도 (b = 분기 수, d = 깊이)
    /// ---------------------------------------------------------------------
    ///   - Minimax 단독          : 시간 O(b^d) , 공간 O(d)  (호출 스택 깊이만)
    ///   - Alpha-Beta (최선)     : 시간 O(b^(d/2)) , 공간 O(d)
    ///   - Alpha-Beta (최악)     : Minimax 와 동일 — 정렬이 나쁠 때
    ///   ※ 공간이 O(d) 인 이유 : DFS 라 트리를 통째로 들고 다닐 필요가 없다 (BFS 류와의 큰 차이).
    ///
    ///   틱택토 (b ≤ 9, d ≤ 9) 의 경우:
    ///     - 빈 보드에서 Minimax 단독은 약 549,946 노드 (= 9!) 탐색.
    ///     - Alpha-Beta 를 쓰면 보통 수만 노드로 줄어든다 (정렬 안 해도 큰 효과).
    ///
    /// 5. 게임에서의 사용처
    /// ---------------------------------------------------------------------
    ///   - 체스 / 체커          : 클래식 적용. 딥블루 시절 핵심.
    ///   - 오목 / 틱택토 / 커넥트포 : 학습 데모로 가장 흔함. 틱택토는 완전 탐색 가능.
    ///   - 턴제 보드게임 봇      : Othello, Backgammon, 추상 전략 게임.
    ///   - 정보 비공개 게임에는 부적합 : 포커류 → CFR / 신경망.
    ///   - 분기 폭발 게임에는 부적합 : 바둑(b≈250), RTS → MCTS / 신경망.
    ///
    /// 6. 사용 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   - 호출 스택(재귀)       : DFS 의 자연스러운 표현. 깊이가 d 이므로 O(d) 공간.
    ///                          반복 버전(직접 Stack) 도 가능하지만 코드가 복잡해져 학습용으론 재귀가 적합.
    ///   - TicTacToeBoard       : Make/Undo 패턴 — 매 노드에서 객체를 새로 만들지 않아 GC 부담 없음.
    ///                          자세한 이유는 TicTacToeBoard.cs 의 헤더 참고.
    ///   - int 점수 (int)       : 평가 함수의 반환 타입. 잎 점수를 (10 - depth) 같은 정수로 표현.
    ///                          float 도 가능하지만 정수가 비교 안정적이고 디버깅하기 쉽다.
    ///   - SearchStats          : 노드 수 / 가지치기 횟수를 누적해 학습용 통계 제공.
    /// </summary>
    public static class MinimaxAlgorithm
    {
        /// <summary>
        /// Minimax 탐색 통계. 학습/디버깅용.
        /// "Alpha-Beta 가 얼마나 절약했는가?" 를 두 번 돌려 비교하는 데 쓴다.
        /// </summary>
        public class SearchStats
        {
            /// <summary>탐색한 노드 수 (재귀 호출 횟수).</summary>
            public int NodesExplored;
            /// <summary>Alpha-Beta 가지치기로 잘려나간 가지의 수.</summary>
            public int Cutoffs;

            public void Reset()
            {
                NodesExplored = 0;
                Cutoffs       = 0;
            }
        }

        /// <summary>
        /// 루트 평가에서 자주 쓰는 보조 결과: (수, 그 수의 점수).
        /// 시각화 측이 "후보 수마다 점수 표시" 를 하기 위해 사용.
        /// </summary>
        public readonly struct ScoredMove
        {
            public readonly int Row;
            public readonly int Col;
            public readonly int Score;
            public ScoredMove(int r, int c, int s) { Row = r; Col = c; Score = s; }
        }

        // 평가에 쓰는 대표 값.
        // 실제 게임 결과(승/패/무) 만 따지기 때문에 이 한 쌍의 큰 값으로 충분.
        // 잎 점수에서 depth 를 빼면 *빠른 승리/늦은 패배* 를 선호하게 된다 (학습 포인트 핵심).
        private const int WinScore  =  1000;
        private const int LoseScore = -1000;
        private const int DrawScore =     0;

        /// <summary>
        /// 현재 국면에서 aiPlayer 입장의 *최선의 수* 를 반환한다.
        ///
        /// 동시에 모든 후보 수의 점수도 candidates 에 채워주므로,
        /// 시각화 측은 "AI 가 어느 칸을 얼마로 평가했는지" 를 그대로 보여줄 수 있다.
        /// (이게 Minimax 학습의 가장 큰 깨달음 — '왜 그 수를 두는가' 가 점수로 드러난다.)
        /// </summary>
        public static (int row, int col) FindBestMove(
            TicTacToeBoard board,
            Player aiPlayer,
            bool useAlphaBeta = true,
            List<ScoredMove> candidates = null,
            SearchStats stats = null)
        {
            // [1] 안전 검사: 이미 끝난 게임에는 둘 수가 없다.
            if (board.Result() != GameResult.Ongoing)
            {
                return (-1, -1);
            }

            int bestScore = int.MinValue;
            (int row, int col) bestMove = (-1, -1);

            // [2] Alpha-Beta 의 초기 안전선.
            //     루트에서 alpha = -∞, beta = +∞ 로 시작.
            //     자식 호출이 깊어질수록 점점 좁혀진다.
            int alpha = int.MinValue;
            int beta  = int.MaxValue;

            // [3] 루트의 모든 합법수를 순회 — 이 단계가 곧 "AI 가 후보 수를 평가" 하는 단계.
            //     (board.LegalMoves 의 결과를 미리 List 로 받는 이유 : MakeMove 로 보드가 바뀌면
            //      enumerator 가 무효화될 수 있으므로 안전하게 스냅샷.)
            var moves = new List<(int r, int c)>();
            foreach (var m in board.LegalMoves()) moves.Add(m);

            foreach (var (r, c) in moves)
            {
                // [3-a] 후보 수를 두고 들어간다.
                board.MakeMove(r, c);

                // [3-b] 자식 점수 = "이 수를 둔 뒤 상대가 최선을 두면 결국 누구한테 유리?"
                //       자식 호출은 *상대 차례* 이므로 isMax = false.
                //       depth=1 부터 시작 (루트가 0).
                int score = Search(
                    board,
                    depth: 1,
                    alpha: alpha,
                    beta: beta,
                    isMaxPlayer: false,
                    aiPlayer: aiPlayer,
                    useAlphaBeta: useAlphaBeta,
                    stats: stats);

                // [3-c] 보드를 *반드시* 원상복구. 이걸 빼먹으면 다음 후보가 더러운 보드에서 평가됨.
                board.UndoMove(r, c);

                // 후보 점수 기록 (시각화용).
                candidates?.Add(new ScoredMove(r, c, score));

                // [3-d] 더 좋은 점수면 best 갱신.
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove  = (r, c);
                }

                // [3-e] 루트에서도 alpha 갱신 → 다음 형제 가지가 더 좁은 윈도우로 출발.
                //     루트 자체는 cutoff 가 거의 안 일어나지만, 가지치기 효과는 자식 깊이에서 누적된다.
                if (useAlphaBeta)
                {
                    alpha = System.Math.Max(alpha, bestScore);
                    // 루트는 보통 컷이 발생하지 않는 위치 (alpha < beta = +∞) 이므로
                    // 여기서 break 검사를 강하게 걸지는 않는다.
                }
            }

            return bestMove;
        }

        /// <summary>
        /// Minimax 의 본체 — 재귀 함수.
        /// 반환값은 *aiPlayer 입장* 에서의 점수.
        /// MAX 노드는 aiPlayer 의 차례, MIN 노드는 상대 차례라는 뜻.
        /// </summary>
        private static int Search(
            TicTacToeBoard board,
            int depth,
            int alpha,
            int beta,
            bool isMaxPlayer,
            Player aiPlayer,
            bool useAlphaBeta,
            SearchStats stats)
        {
            // 통계 누적: 매 노드마다 +1.
            if (stats != null) stats.NodesExplored++;

            // ───────────────────────────────────────────────────────────
            // [1] 종료 검사 (Terminal Test)
            //     게임이 끝났으면 그 결과를 점수로 환산해 반환.
            //     틱택토는 깊이가 9 를 넘지 않으므로 별도의 depth 한도는 두지 않는다.
            //     (체스/오목 같은 깊은 게임이라면 'depth >= maxDepth' 도 검사해야 한다.)
            // ───────────────────────────────────────────────────────────
            var result = board.Result();
            if (result != GameResult.Ongoing)
            {
                return Evaluate(result, aiPlayer, depth);
            }

            // ───────────────────────────────────────────────────────────
            // [2] MAX 노드 — aiPlayer 차례 → 점수 최대화
            // ───────────────────────────────────────────────────────────
            if (isMaxPlayer)
            {
                int best = int.MinValue;

                foreach (var (r, c) in SnapshotMoves(board))
                {
                    // [2-a] 한 수 두고 자식 호출 → 차례가 바뀌므로 다음은 MIN 노드.
                    board.MakeMove(r, c);
                    int score = Search(board, depth + 1, alpha, beta, false, aiPlayer, useAlphaBeta, stats);
                    board.UndoMove(r, c);

                    // [2-b] best 갱신.
                    if (score > best) best = score;

                    // [2-c] Alpha-Beta: alpha 갱신 → 'MAX 가 지금까지 보장받은 최저' 가 올라감.
                    if (useAlphaBeta)
                    {
                        if (best > alpha) alpha = best;

                        // [2-d] β-cutoff: alpha >= beta 면 남은 형제는 무의미하므로 즉시 종료.
                        //       의미: "여기서 더 좋은 점수가 나온다 한들, 위층의 MIN 이 이 가지를 절대 안 고를 것."
                        if (alpha >= beta)
                        {
                            if (stats != null) stats.Cutoffs++;
                            break;
                        }
                    }
                }
                return best;
            }
            // ───────────────────────────────────────────────────────────
            // [3] MIN 노드 — 상대 차례 → 점수 최소화 (= 상대 입장에선 최대화)
            //     MAX 와 정확히 대칭이다.
            // ───────────────────────────────────────────────────────────
            else
            {
                int best = int.MaxValue;

                foreach (var (r, c) in SnapshotMoves(board))
                {
                    // [3-a] 한 수 두고 자식 호출 → 다음은 MAX 노드.
                    board.MakeMove(r, c);
                    int score = Search(board, depth + 1, alpha, beta, true, aiPlayer, useAlphaBeta, stats);
                    board.UndoMove(r, c);

                    // [3-b] best 갱신 (최소화).
                    if (score < best) best = score;

                    // [3-c] Alpha-Beta: beta 갱신 → 'MIN 이 지금까지 보장받은 최대' 가 내려감.
                    if (useAlphaBeta)
                    {
                        if (best < beta) beta = best;

                        // [3-d] α-cutoff: alpha >= beta 면 즉시 종료.
                        //       의미: "여기서 더 낮은 점수가 나와도, 위층의 MAX 가 이 가지를 절대 안 고를 것."
                        if (alpha >= beta)
                        {
                            if (stats != null) stats.Cutoffs++;
                            break;
                        }
                    }
                }
                return best;
            }
        }

        /// <summary>
        /// 잎 노드(게임 종료 국면) 의 점수를 매긴다 — Minimax 의 *기준점*.
        ///
        /// 핵심 트릭: 점수에서 depth 를 빼서(승) 또는 더해서(패) 두 가지 효과를 동시에 얻는다.
        ///   - 빠른 승리 선호 : 같은 승리라도 일찍 끝낼 수 있는 수를 더 높게 평가.
        ///   - 늦은 패배 선호 : 어차피 진다면 최대한 시간을 끌어 상대 실수 기회를 늘림.
        /// 이 보정이 없으면 Minimax 가 "어차피 이기는 길 두 개 중 9 수 뒤 승리" 를 골라
        /// 사람이 보기에 멍청해 보인다.
        /// </summary>
        private static int Evaluate(GameResult result, Player aiPlayer, int depth)
        {
            if (result == GameResult.Draw) return DrawScore;

            // result 가 Ongoing 이 아니라는 가정이 들어 있다 (호출 측이 사전에 검사함).
            bool aiWins =
                (aiPlayer == Player.X && result == GameResult.XWins) ||
                (aiPlayer == Player.O && result == GameResult.OWins);

            // depth 보정 — 빠른 승 / 늦은 패 선호.
            return aiWins ? (WinScore - depth) : (LoseScore + depth);
        }

        /// <summary>
        /// 합법수를 List 로 미리 스냅샷. 재귀 도중 보드가 바뀌어 enumerator 가
        /// 깨지는 사고를 막기 위함. 틱택토는 합법수가 최대 9 개라 비용은 무시 가능.
        /// (체스 같은 게임에서는 List 풀(pool) 을 쓰는 등 별도 최적화가 필요하다.)
        /// </summary>
        private static List<(int r, int c)> SnapshotMoves(TicTacToeBoard board)
        {
            var list = new List<(int r, int c)>();
            foreach (var m in board.LegalMoves()) list.Add(m);
            return list;
        }
    }
}
