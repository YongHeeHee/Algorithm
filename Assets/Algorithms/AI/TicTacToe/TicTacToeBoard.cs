using System.Collections.Generic;

namespace Algorithms.AI
{
    /// <summary>플레이어 식별자. None 은 빈 칸.</summary>
    public enum Player { None, X, O }

    /// <summary>게임 종료 상태. Ongoing = 아직 진행 중.</summary>
    public enum GameResult { Ongoing, XWins, OWins, Draw }

    /// <summary>
    /// =====================================================================
    ///  TicTacToeBoard — 3×3 틱택토 국면(상태) 표현
    /// =====================================================================
    ///
    /// ▶ 왜 별도 파일로 분리했는가
    ///   Minimax 알고리즘 본체(MinimaxAlgorithm.cs)는 *어떤 게임이든* 같은 골격으로 동작한다.
    ///   바뀌는 건 "국면이 어떻게 표현되고", "어떤 수가 가능하며", "누가 이겼는지" 뿐이다.
    ///   이 파일은 그 *게임 고유 부분* 을 한 곳에 모아 두어, 알고리즘 본체와의 경계를 분명히 한다.
    ///
    /// ▶ 핵심 설계 결정 — Make/Undo 패턴 (vs Clone)
    ///   Minimax 는 게임 트리를 DFS 로 깊게 탐색한다. 매 노드마다 "수를 두고 → 재귀 → 되돌리기" 가
    ///   수만 번 일어난다. 두 가지 선택지가 있다:
    ///     1) Clone : 매번 새 보드 객체를 만든다 → 구현은 단순하나 GC 압박이 크다.
    ///     2) Make/Undo : 보드 한 개를 in-place 로 수정하고, 끝나면 정확히 되돌린다 → 빠르고 GC 부담 없음.
    ///   체스/바둑 같은 무거운 게임에서는 Make/Undo 가 사실상 필수. 틱택토에선 둘 다 OK 지만
    ///   *학습 목적상* Make/Undo 패턴을 보여주는 것이 좋다 (재귀 후 정확한 상태 복원).
    ///
    /// ▶ 좌표 규칙
    ///   (row, col), 0-based, 좌상단이 (0,0). 화면 좌표가 아닌 *논리 좌표* 임.
    ///   Visualizer 는 이걸 World 좌표로 매핑할 책임을 갖는다.
    /// </summary>
    public class TicTacToeBoard
    {
        /// <summary>한 변의 길이. 표준 틱택토는 3.</summary>
        public const int Size = 3;

        // 3x3 셀 배열. None / X / O 중 하나.
        // [r, c] = r 행 c 열의 칸.
        private readonly Player[,] _cells = new Player[Size, Size];

        /// <summary>지금 둘 차례인 플레이어. 표준 규칙: X 가 먼저 시작.</summary>
        public Player Current { get; private set; } = Player.X;

        /// <summary>지금까지 둔 수의 총 개수. 무승부 판정과 깊이 추적에 유용.</summary>
        public int MoveCount { get; private set; }

        /// <summary>한 칸의 현재 점유 상태를 읽는다.</summary>
        public Player Get(int row, int col) => _cells[row, col];

        /// <summary>해당 칸이 비어 있는가.</summary>
        public bool IsEmpty(int row, int col) => _cells[row, col] == Player.None;

        /// <summary>
        /// 현재 플레이어로 (row, col) 에 수를 둔다.
        /// 호출 측은 반드시 IsEmpty 로 사전 검증. (이중 안전장치를 두지 않는 이유:
        /// Minimax 의 hot path 에서 매번 검증하면 느려진다. 잘못 쓰면 즉시 드러나는 게 학습에 좋음.)
        /// </summary>
        public void MakeMove(int row, int col)
        {
            // [1] 칸에 현재 플레이어의 표식을 적는다.
            _cells[row, col] = Current;

            // [2] 차례를 상대에게 넘긴다. X ↔ O 토글.
            Current = (Current == Player.X) ? Player.O : Player.X;

            // [3] 수 카운터 증가.
            MoveCount++;
        }

        /// <summary>
        /// 가장 마지막에 둔 수를 정확히 되돌린다 (Minimax 재귀의 핵심).
        /// 호출 측이 좌표를 직접 넘겨주는 이유: 호출 스택에서 이미 알고 있으므로 별도 history 가 불필요.
        /// (히스토리 스택을 만들면 메모리 + 푸시/팝 비용이 추가됨.)
        /// </summary>
        public void UndoMove(int row, int col)
        {
            // [1] 칸을 비운다.
            _cells[row, col] = Player.None;

            // [2] 차례를 *되돌려* 받는다. (방금 둔 사람 = 직전의 Current)
            Current = (Current == Player.X) ? Player.O : Player.X;

            // [3] 수 카운터 감소.
            MoveCount--;
        }

        /// <summary>
        /// 현재 보드에서 둘 수 있는 모든 합법수를 열거한다.
        /// 틱택토는 "빈 칸이면 어디든 OK" 라 단순하지만, 체스라면 여기에 룰 검증이 잔뜩 들어간다.
        ///
        /// ※ Minimax 의 가지(branch) 가 곧 이 메서드의 결과 — Alpha-Beta 가지치기는
        ///   여기서 *유망한 수가 먼저* 나오도록 정렬해 두면 효율이 극적으로 좋아진다 (move ordering).
        ///   틱택토에선 정렬해도 미미하므로 별도 정렬 없이 좌상단부터 순서대로 돌린다.
        /// </summary>
        public IEnumerable<(int row, int col)> LegalMoves()
        {
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    if (_cells[r, c] == Player.None)
                    {
                        yield return (r, c);
                    }
                }
            }
        }

        /// <summary>
        /// 현재 국면의 결과를 판정한다.
        ///   Ongoing : 아직 진행 중
        ///   XWins / OWins : 한 줄을 완성한 플레이어 승
        ///   Draw    : 빈 칸이 없고 승자도 없음 = 무승부
        ///
        /// 모든 가로 / 세로 / 대각선 8 줄을 검사. O(1) 시간 (Size 가 상수이므로).
        /// </summary>
        public GameResult Result()
        {
            // [1] 가로 3 줄 검사. 한 줄의 세 칸이 None 이 아니고 모두 같으면 그 플레이어 승.
            for (int r = 0; r < Size; r++)
            {
                if (_cells[r, 0] != Player.None &&
                    _cells[r, 0] == _cells[r, 1] &&
                    _cells[r, 1] == _cells[r, 2])
                {
                    return ToWinResult(_cells[r, 0]);
                }
            }

            // [2] 세로 3 줄 검사.
            for (int c = 0; c < Size; c++)
            {
                if (_cells[0, c] != Player.None &&
                    _cells[0, c] == _cells[1, c] &&
                    _cells[1, c] == _cells[2, c])
                {
                    return ToWinResult(_cells[0, c]);
                }
            }

            // [3] 대각선 2 줄 검사 (좌상→우하, 우상→좌하).
            if (_cells[0, 0] != Player.None &&
                _cells[0, 0] == _cells[1, 1] &&
                _cells[1, 1] == _cells[2, 2])
            {
                return ToWinResult(_cells[0, 0]);
            }
            if (_cells[0, 2] != Player.None &&
                _cells[0, 2] == _cells[1, 1] &&
                _cells[1, 1] == _cells[2, 0])
            {
                return ToWinResult(_cells[0, 2]);
            }

            // [4] 승자가 없고 빈 칸도 없으면 무승부.
            //     빈 칸이 남아 있으면 아직 진행 중.
            if (MoveCount >= Size * Size)
            {
                return GameResult.Draw;
            }
            return GameResult.Ongoing;
        }

        /// <summary>
        /// 승리한 줄을 이루는 세 셀의 좌표를 반환한다 (없으면 빈 배열).
        /// 시각화에서 승리 라인을 강조하는 데 사용.
        /// </summary>
        public IReadOnlyList<(int row, int col)> WinningLine()
        {
            // 가로
            for (int r = 0; r < Size; r++)
            {
                if (_cells[r, 0] != Player.None &&
                    _cells[r, 0] == _cells[r, 1] &&
                    _cells[r, 1] == _cells[r, 2])
                {
                    return new[] { (r, 0), (r, 1), (r, 2) };
                }
            }
            // 세로
            for (int c = 0; c < Size; c++)
            {
                if (_cells[0, c] != Player.None &&
                    _cells[0, c] == _cells[1, c] &&
                    _cells[1, c] == _cells[2, c])
                {
                    return new[] { (0, c), (1, c), (2, c) };
                }
            }
            // 대각선
            if (_cells[0, 0] != Player.None &&
                _cells[0, 0] == _cells[1, 1] &&
                _cells[1, 1] == _cells[2, 2])
            {
                return new[] { (0, 0), (1, 1), (2, 2) };
            }
            if (_cells[0, 2] != Player.None &&
                _cells[0, 2] == _cells[1, 1] &&
                _cells[1, 1] == _cells[2, 0])
            {
                return new[] { (0, 2), (1, 1), (2, 0) };
            }
            return System.Array.Empty<(int row, int col)>();
        }

        /// <summary>
        /// 보드를 초기 상태로 되돌린다. 새 게임 시작 시 호출.
        /// </summary>
        public void Reset()
        {
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    _cells[r, c] = Player.None;
                }
            }
            Current   = Player.X;
            MoveCount = 0;
        }

        /// <summary>
        /// 현재 보드 상태의 *독립된 복사본* 을 반환한다.
        ///
        /// MCTS 의 시뮬레이션 단계는 무작위 수를 끝까지 두면서 결과만 본다.
        /// Make/Undo 로 되돌리는 게 가능하긴 하지만 코드가 복잡해지고, 시뮬레이션 깊이가 매번 달라
        /// 깔끔하지 않다. 이럴 땐 *iteration 시작 시 한 번 clone* 하고 그 사본을 마구 변경하는 편이 단순.
        ///
        /// (체스/바둑처럼 보드가 무거운 게임에서는 clone 비용이 커 Make/Undo 가 우세하지만,
        ///  3×3 틱택토에선 차이가 무시 가능 → 가독성 우선.)
        /// </summary>
        public TicTacToeBoard Clone()
        {
            var copy = new TicTacToeBoard();
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    copy._cells[r, c] = _cells[r, c];
                }
            }
            copy.Current   = Current;
            copy.MoveCount = MoveCount;
            return copy;
        }

        // 내부 헬퍼: Player 표식을 GameResult 의 승리값으로 변환.
        // X / O 외의 값이 들어오는 경우는 호출 측에서 차단되므로 별도 검증 없이 매핑만 한다.
        private static GameResult ToWinResult(Player p) =>
            p == Player.X ? GameResult.XWins : GameResult.OWins;
    }
}
