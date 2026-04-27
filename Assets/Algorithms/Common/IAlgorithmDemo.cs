namespace Algorithms.Common
{
    /// <summary>
    /// =====================================================================
    ///  IAlgorithmDemo — 알고리즘 데모용 공통 인터페이스
    /// =====================================================================
    ///
    /// ▶ 무엇인가
    ///   알고리즘 시각화 컴포넌트(BFSGridVisualizer / DFSGridVisualizer / ...) 가
    ///   "처음부터 다시 실행" 동작을 외부에서 호출할 수 있도록 하기 위한 얇은 계약.
    ///
    /// ▶ 왜 인터페이스로 두는가
    ///   1) 공용 UI 컨트롤러(AlgorithmDemoUI) 가 어떤 알고리즘이든 받을 수 있다.
    ///      → 새 알고리즘이 추가될 때 UI 측 코드를 건드릴 필요가 없다.
    ///   2) 컴파일 타임에 "Restart() 빼먹었네" 를 알려준다 (실수 방지).
    ///   3) 인터페이스 자체에 로직이 없으므로 알고리즘의 메커니즘을 가리지 않는다.
    ///      → 학습 목적에 부합 (각 Visualizer 의 알고리즘 본체는 그대로 노출).
    ///
    /// ▶ 사용 예시
    ///   public class BFSGridVisualizer : MonoBehaviour, IAlgorithmDemo
    ///   {
    ///       public void Restart() { ... }
    ///   }
    /// </summary>
    public interface IAlgorithmDemo
    {
        /// <summary>
        /// 알고리즘 데모를 처음 상태로 되돌리고 다시 실행한다.
        /// 구현 측에서는 다음을 수행해야 한다:
        ///   1) 현재 진행 중인 코루틴/연산을 멈춘다.
        ///   2) 생성된 GameObject 와 내부 상태를 정리한다.
        ///   3) 그리드/그래프를 다시 만들고 알고리즘을 재시작한다.
        /// </summary>
        void Restart();
    }
}
