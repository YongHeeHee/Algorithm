using System.Collections.Generic;

namespace Algorithms.AI
{
    /// <summary>
    /// =====================================================================
    ///  MCTS (Monte Carlo Tree Search) — Minimax 의 통계적 대안
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// Minimax 와 같은 문제(2 인 게임에서 최선의 수 찾기)를 *정반대 방식* 으로 푼다.
    ///
    /// Minimax  : 게임 트리를 *완전 탐색* 한다.
    ///            → 분기가 폭발하는 게임(바둑 b≈250)에선 사실상 불가능.
    ///            → 비-잎 노드를 평가하려면 도메인 지식 기반 평가 함수 필요.
    ///
    /// MCTS     : 트리를 *통계적으로 샘플링* 한다.
    ///            → 유망한 가지만 깊게, 비유망한 가지는 얕게 본다.
    ///            → "끝까지 무작위로 둬보고 진짜 승/패만 본다" → 평가 함수 불필요.
    ///            → 시간이 더 길수록 *부드럽게* 좋아진다 (anytime 알고리즘).
    ///
    /// "평가 함수를 만들 수 없는 게임에서, 끝까지 가본 결과만으로 의사결정한다"
    /// — 이게 MCTS 가 Minimax 의 대안이 되는 핵심. AlphaGo 의 뼈대.
    ///
    /// 2. 동작 흐름 — 4 단계 반복
    /// ---------------------------------------------------------------------
    /// 한 iteration 은 4 단계로 구성된다 (이름 첫 글자를 따 'SEEB' 라 외워도 좋다):
    ///
    ///   ① Selection (선택)
    ///      루트부터 시작해, 자식 중 *가장 유망해 보이는 쪽* 으로 내려간다.
    ///      "유망함" 의 기준은 UCB1 (Upper Confidence Bound) 공식:
    ///          UCB1(child) = winRate + c · sqrt( ln(N) / n )
    ///                        ─────── 활용(exploit)   ─────── 탐험(explore)
    ///        N = 부모 방문 횟수, n = 자식 방문 횟수, c = 탐험 상수 (보통 √2)
    ///      방문이 적은 자식엔 탐험 보너스가 커서 한 번씩 가본다 → 유망하지만 덜 본 가지를 발굴.
    ///      방문이 많은 자식이라도 승률이 높으면 계속 선택 → 유망한 가지를 깊게 판다.
    ///
    ///      "확장 가능한 자식이 남아 있는 노드" 또는 "잎 노드(터미널)" 에 도달할 때까지 반복.
    ///
    ///   ② Expansion (확장)
    ///      위에서 멈춘 노드에 *아직 시도 안 한 자식 수* 가 있으면, 그 중 하나를 무작위로 골라 자식 노드로 추가.
    ///      (한 iteration 에서 자식을 *하나* 만 확장하는 것이 표준 — 트리가 점진적으로 자란다.)
    ///
    ///   ③ Simulation (시뮬레이션 / Rollout / Playout)
    ///      그 자식에서 출발해 *게임이 끝날 때까지 무작위로* 둔다.
    ///      양쪽 다 무작위 → 도메인 지식이 필요 없는 이유.
    ///      게임이 끝났을 때 누가 이겼는지(승/패/무)만 본다.
    ///
    ///   ④ Backpropagation (역전파)
    ///      이번 iteration 에서 지나온 트리 경로의 모든 노드에 결과를 누적.
    ///        - visits += 1  (방문 횟수)
    ///        - winSum += reward  (승=1, 무=0.5, 패=0 — 보통 그 노드를 만든 *플레이어* 입장에서)
    ///      이걸 부모로 거슬러 루트까지 갱신.
    ///
    /// 시간 한도가 끝나면 *루트의 자식 중 가장 방문이 많은 쪽* 을 최종 선택.
    /// (가장 승률이 높은 자식이 아닌 *가장 많이 방문된* 자식을 고른다 — 이게 표준.
    ///  이유: UCB1 이 충분히 수렴했다면 두 기준이 일치하지만, 방문 횟수가 더 robust.)
    ///
    /// 3. Minimax 와의 결정적 차이
    /// ---------------------------------------------------------------------
    ///   - 트리 생성   : Minimax 는 깊이 우선으로 *전부* 펼친다. MCTS 는 유망한 가지만 *점진적* 으로 자란다.
    ///   - 평가 방법   : Minimax 는 평가 함수(eval). MCTS 는 random rollout 의 결과(승/패).
    ///   - 가지치기   : Minimax 는 Alpha-Beta 로 잘라낸다. MCTS 는 *애초에 안 본다* (UCB1 이 자연스럽게 가지치기).
    ///   - 종료 조건   : Minimax 는 트리 다 봐야 끝. MCTS 는 *언제든 멈춰도* 그 시점의 best 를 답으로 쓸 수 있음.
    ///   - 결정성     : Minimax 는 결정적(같은 입력 → 같은 답). MCTS 는 random 시드에 따라 답이 달라질 수 있음.
    ///
    /// 4. 시간 / 공간 복잡도
    /// ---------------------------------------------------------------------
    ///   - 시간 : O(iterations × averageGameLength)
    ///        iterations 는 사용자가 정한다. 시간이 많으면 더 많이 돌리면 됨.
    ///   - 공간 : O(트리에 추가된 노드 수) ≤ O(iterations)
    ///        한 iteration 당 노드 1 개 확장 → 최악의 경우 iterations 만큼.
    ///        실제론 같은 경로가 여러 번 선택되므로 트리는 훨씬 작게 자란다.
    ///
    /// 5. 게임에서의 사용처
    /// ---------------------------------------------------------------------
    ///   - 바둑(Go)             : AlphaGo / AlphaZero 의 의사결정 핵심. 분기가 너무 커 Minimax 무력화.
    ///   - Hex / Settlers of Catan : 평가 함수 만들기 어려운 추상 전략 게임.
    ///   - General Game Playing : 규칙만 알고 휴리스틱이 없는 임의 게임 봇.
    ///   - 실시간 전략(RTS) 의 일부 : 행동 후보 평가 (다만 분기가 너무 커서 부분 적용).
    ///
    /// 6. 사용 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   - Tree Node 클래스    : Visits, WinSum, 자식 List, UntriedMoves List, Parent 참조.
    ///                          트리가 *부분적으로* 자라므로 명시적 노드 객체가 필요.
    ///   - System.Random       : 시뮬레이션의 무작위 수 선택. 시드 고정 시 재현성 확보.
    ///   - Math.Log / Sqrt     : UCB1 공식의 로그·제곱근. 노드 선택 시마다 호출됨.
    ///   - TicTacToeBoard.Clone(): 시뮬레이션이 보드를 끝까지 변경하므로 iteration 시작 시 사본 생성.
    ///                          Make/Undo 도 가능하지만 시뮬레이션 깊이 가변 → clone 이 단순.
    /// </summary>
    public static class MCTSAlgorithm
    {
        /// <summary>
        /// MCTS 통계. 학습용.
        /// "iterations 늘릴수록 정말 좋아지는가?" 를 측정하는 데 쓴다.
        /// </summary>
        public class SearchStats
        {
            /// <summary>실제로 수행된 iteration 수.</summary>
            public int Iterations;
            /// <summary>모든 simulation 에서 둔 random 수의 총합 (부하 측정용).</summary>
            public int TotalRolloutMoves;
            /// <summary>트리에 만들어진 총 노드 수 (루트 포함).</summary>
            public int TreeNodeCount;

            public void Reset()
            {
                Iterations         = 0;
                TotalRolloutMoves  = 0;
                TreeNodeCount      = 0;
            }
        }

        /// <summary>
        /// 루트 자식 후보의 통계. 시각화에서 "AI 가 어느 칸을 얼마나 살펴봤고 승률은 얼마인가" 를 표시.
        /// (Minimax 의 ScoredMove 와 같은 역할이지만 점수 대신 방문 횟수 + 승률을 노출.)
        /// </summary>
        public readonly struct ScoredMove
        {
            public readonly int Row;
            public readonly int Col;
            public readonly int Visits;
            public readonly float WinRate; // 0..1, 그 자식 노드를 만든 플레이어(= AI) 입장.
            public ScoredMove(int r, int c, int v, float w) { Row = r; Col = c; Visits = v; WinRate = w; }
        }

        // UCB1 의 탐험 상수. √2 는 보상이 0..1 범위일 때의 표준 권장값.
        // 작게 하면 활용(exploit) 위주, 크게 하면 탐험(explore) 위주.
        private const double ExplorationConstant = 1.41421356; // ≈ √2

        // ─────────────────────────────────────────────────────────────
        // 정적 진입점 — Minimax.FindBestMove 와 같은 시그니처 흐름.
        // 시각화가 필요 없는 호출자는 이걸로 충분하다.
        // 시각화 측은 아래 Searcher 를 직접 만들어 chunk 단위로 돌릴 수 있다.
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 현재 국면에서 aiPlayer 입장의 *최선의 수* 를 MCTS 로 결정한다.
        /// iterations 만큼 트리를 키운 뒤 가장 방문이 많은 루트 자식을 반환.
        /// </summary>
        public static (int row, int col) FindBestMove(
            TicTacToeBoard board,
            Player aiPlayer,
            int iterations,
            List<ScoredMove> candidates = null,
            SearchStats stats = null,
            System.Random rng = null)
        {
            var searcher = new Searcher(board, aiPlayer, rng);
            searcher.RunIterations(iterations, stats);
            searcher.GetRootCandidates(candidates);
            return searcher.BestMove();
        }

        // ─────────────────────────────────────────────────────────────
        // Searcher — 점진적 (chunk 단위) 탐색을 지원하는 stateful 객체.
        // 시각화에서 "현재 트리 상태 → 잠깐 보여주기 → 더 돌리기" 패턴을 가능하게 한다.
        // ─────────────────────────────────────────────────────────────

        public class Searcher
        {
            private readonly Node _root;
            private readonly TicTacToeBoard _rootBoard;
            private readonly Player _aiPlayer;
            private readonly System.Random _rng;
            private int _treeNodeCount;
            private int _totalRolloutMoves;

            public Searcher(TicTacToeBoard board, Player aiPlayer, System.Random rng = null)
            {
                _rootBoard = board.Clone();
                _aiPlayer  = aiPlayer;
                _rng       = rng ?? new System.Random();

                // 루트 노드: PlayerJustMoved = 상대 (왜? 루트 보드는 상대의 마지막 수가 끝난 직후 상태이므로).
                _root = new Node(
                    parent: null,
                    moveFromParent: (-1, -1),
                    playerJustMoved: OtherPlayer(_rootBoard.Current),
                    untriedMoves: SnapshotMoves(_rootBoard));
                _treeNodeCount = 1;
            }

            /// <summary>n 번의 MCTS iteration 을 추가로 수행한다.</summary>
            public void RunIterations(int n, SearchStats stats = null)
            {
                for (int i = 0; i < n; i++)
                {
                    RunOneIteration();
                }
                if (stats != null)
                {
                    stats.Iterations        += n;
                    stats.TotalRolloutMoves += _totalRolloutMoves;
                    stats.TreeNodeCount      = _treeNodeCount;
                    _totalRolloutMoves = 0; // 다음 RunIterations 의 누적이 중복되지 않도록 리셋.
                }
            }

            /// <summary>
            /// 루트 자식들의 통계를 출력 리스트에 채운다.
            /// 시각화는 이 정보로 "어느 칸이 얼마나 살펴봐졌는지" 를 표시.
            /// </summary>
            public void GetRootCandidates(List<ScoredMove> output)
            {
                if (output == null) return;
                output.Clear();
                foreach (var ch in _root.Children)
                {
                    float winRate = ch.Visits == 0 ? 0f : (float)(ch.WinSum / ch.Visits);
                    output.Add(new ScoredMove(ch.MoveFromParent.r, ch.MoveFromParent.c, ch.Visits, winRate));
                }
            }

            /// <summary>현재까지의 트리에서 가장 방문이 많은 루트 자식의 수를 반환.</summary>
            public (int row, int col) BestMove()
            {
                Node best = null;
                foreach (var ch in _root.Children)
                {
                    if (best == null || ch.Visits > best.Visits) best = ch;
                }
                return best == null ? (-1, -1) : best.MoveFromParent;
            }

            // ───────────────────────────────────────────────────────
            // 한 iteration = Selection → Expansion → Simulation → Backpropagation
            // ───────────────────────────────────────────────────────
            private void RunOneIteration()
            {
                // [0] 사본 보드. 이 iteration 에서만 자유롭게 변경한다 (rootBoard 는 보존).
                var sandbox = _rootBoard.Clone();

                // ───────────────────────────────────────────────────
                // [1] Selection — 루트부터 UCB1 으로 자식을 골라 내려간다.
                //     멈추는 조건: 확장 가능한 자식이 남아 있거나, 게임이 끝난 노드.
                // ───────────────────────────────────────────────────
                Node node = _root;
                while (node.UntriedMoves.Count == 0 && node.Children.Count > 0)
                {
                    node = SelectByUCB1(node);
                    sandbox.MakeMove(node.MoveFromParent.r, node.MoveFromParent.c);
                }

                // ───────────────────────────────────────────────────
                // [2] Expansion — 미시도 수가 남아 있고 게임이 안 끝났다면, 자식을 하나 추가한다.
                //     (게임이 끝난 노드면 확장 불가 → 곧장 [3] 시뮬레이션을 건너뛰고 [4] 로 간다.)
                // ───────────────────────────────────────────────────
                if (node.UntriedMoves.Count > 0)
                {
                    int idx = _rng.Next(node.UntriedMoves.Count);
                    var move = node.UntriedMoves[idx];
                    node.UntriedMoves.RemoveAt(idx);

                    // 이 수를 둘 사람 = 그 노드 확장 후 부모가 된다.
                    Player movingPlayer = sandbox.Current;
                    sandbox.MakeMove(move.r, move.c);

                    var child = new Node(
                        parent: node,
                        moveFromParent: move,
                        playerJustMoved: movingPlayer,
                        untriedMoves: SnapshotMoves(sandbox));
                    node.Children.Add(child);
                    _treeNodeCount++;
                    node = child;
                }

                // ───────────────────────────────────────────────────
                // [3] Simulation (Rollout) — 게임이 끝날 때까지 *완전 무작위* 로 둔다.
                //     이 단계는 '도메인 지식 없음' 이 핵심 — 그래서 평가 함수가 필요 없다.
                //     (실전 MCTS 는 '약한 휴리스틱' 으로 rollout 을 살짝 가이드하기도 한다.)
                // ───────────────────────────────────────────────────
                while (sandbox.Result() == GameResult.Ongoing)
                {
                    var legal = SnapshotMoves(sandbox);
                    var pick  = legal[_rng.Next(legal.Count)];
                    sandbox.MakeMove(pick.r, pick.c);
                    _totalRolloutMoves++;
                }
                var result = sandbox.Result();

                // ───────────────────────────────────────────────────
                // [4] Backpropagation — 결과를 트리 경로에 누적.
                //     보상은 *각 노드의 PlayerJustMoved* 입장에서 환산한다.
                //     (그 노드는 그 플레이어가 만든 결정 → 그 플레이어가 이겼는지를 평가해야 함.)
                // ───────────────────────────────────────────────────
                Node walker = node;
                while (walker != null)
                {
                    walker.Visits++;
                    walker.WinSum += RewardFor(result, walker.PlayerJustMoved);
                    walker = walker.Parent;
                }
            }

            // ───────────────────────────────────────────────────────
            // UCB1 자식 선택. 부모의 자식 중 UCB1 이 가장 큰 자식을 반환.
            // ───────────────────────────────────────────────────────
            private Node SelectByUCB1(Node parent)
            {
                Node best = null;
                double bestScore = double.NegativeInfinity;
                double lnN = System.Math.Log(parent.Visits + 1); // +1 로 ln(0) 방지

                foreach (var ch in parent.Children)
                {
                    // 방문이 0 인 자식은 사실상 무한대 우선 (탐험). 실전에선 expansion 단계에서
                    // 확장 직후 visits=1 이 되므로 여기 도달하는 일은 거의 없다.
                    double ucb;
                    if (ch.Visits == 0)
                    {
                        ucb = double.PositiveInfinity;
                    }
                    else
                    {
                        double winRate = ch.WinSum / ch.Visits;
                        double explore = ExplorationConstant * System.Math.Sqrt(lnN / ch.Visits);
                        ucb = winRate + explore;
                    }

                    if (ucb > bestScore)
                    {
                        bestScore = ucb;
                        best      = ch;
                    }
                }
                return best;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 내부 트리 노드 — public 으로 노출하지 않는다 (캡슐화).
        // ─────────────────────────────────────────────────────────────

        private class Node
        {
            public Node Parent;
            public List<Node> Children = new();
            public List<(int r, int c)> UntriedMoves;
            public (int r, int c) MoveFromParent;

            // 이 노드가 '존재하게 된' 직접 원인 = 그 직전 수를 둔 플레이어.
            // Backpropagation 에서 reward 를 누구 입장에서 매길지 결정한다.
            public Player PlayerJustMoved;

            public int    Visits;
            public double WinSum;

            public Node(
                Node parent,
                (int r, int c) moveFromParent,
                Player playerJustMoved,
                List<(int r, int c)> untriedMoves)
            {
                Parent          = parent;
                MoveFromParent  = moveFromParent;
                PlayerJustMoved = playerJustMoved;
                UntriedMoves    = untriedMoves;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 헬퍼
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 게임 결과를 perspective 플레이어 입장의 보상(0..1) 으로 환산.
        /// 승=1, 무=0.5, 패=0 — UCB1 이 가정하는 표준 보상 범위.
        /// </summary>
        private static double RewardFor(GameResult result, Player perspective)
        {
            if (result == GameResult.Draw) return 0.5;
            bool win =
                (perspective == Player.X && result == GameResult.XWins) ||
                (perspective == Player.O && result == GameResult.OWins);
            return win ? 1.0 : 0.0;
        }

        /// <summary>합법수 스냅샷. board 가 변경돼도 안전하도록 List 로 미리 받는다.</summary>
        private static List<(int r, int c)> SnapshotMoves(TicTacToeBoard board)
        {
            var list = new List<(int r, int c)>();
            foreach (var m in board.LegalMoves()) list.Add(m);
            return list;
        }

        private static Player OtherPlayer(Player p) =>
            p == Player.X ? Player.O : (p == Player.O ? Player.X : Player.None);
    }
}
