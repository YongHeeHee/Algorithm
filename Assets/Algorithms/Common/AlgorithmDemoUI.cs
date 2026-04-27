using UnityEngine;
using UnityEngine.UI;

namespace Algorithms.Common
{
    /// <summary>
    /// =====================================================================
    ///  AlgorithmDemoUI — 알고리즘 데모용 공용 UI 컨트롤러
    /// =====================================================================
    ///
    /// ▶ 역할
    ///   "Restart" 버튼과 IAlgorithmDemo 구현체(BFS/DFS Visualizer 등)를
    ///   Inspector 에서 묶어주는 다리. 버튼 클릭 → 알고리즘 처음부터 다시 실행.
    ///
    /// ▶ 사용 방법
    ///   1) Canvas 안에 Button 을 하나 만든다 (UI > Button).
    ///   2) 빈 GameObject 에 이 컴포넌트를 붙인다.
    ///   3) Inspector 에서:
    ///        - Restart Button 슬롯에 위에서 만든 Button 을 드래그
    ///        - Demo Component 슬롯에 IAlgorithmDemo 를 구현한 Visualizer 를 드래그
    ///          (예: BFSGridVisualizer, DFSGridVisualizer)
    ///   4) Play → 버튼 클릭 → 알고리즘 재시작.
    ///
    /// ▶ 왜 demoComponent 가 MonoBehaviour 타입인가
    ///   Unity Inspector 는 인터페이스 타입의 SerializeField 를 직접 노출하지 못한다.
    ///   그래서 일단 MonoBehaviour 로 받은 뒤, 런타임에 IAlgorithmDemo 로 캐스팅한다.
    ///   인터페이스 미구현 컴포넌트가 잘못 연결되면 Awake 에서 명확한 에러 메시지를 띄운다.
    /// </summary>
    public class AlgorithmDemoUI : MonoBehaviour
    {
        [Header("연결할 UI / 알고리즘")]
        [Tooltip("재시작 버튼 (Canvas 의 Button 컴포넌트)")]
        [SerializeField] private Button restartButton;

        [Tooltip("IAlgorithmDemo 를 구현한 Visualizer (예: BFSGridVisualizer, DFSGridVisualizer)")]
        [SerializeField] private MonoBehaviour demoComponent;

        // 캐스팅된 데모 — Awake 에서 1회 캐싱.
        private IAlgorithmDemo _demo;

        private void Awake()
        {
            // [1] 필수 참조 검증 — 누락된 Inspector 슬롯은 즉시 알려줘야 디버깅이 쉬움.
            if (restartButton == null)
            {
                Debug.LogError($"[{nameof(AlgorithmDemoUI)}] Restart Button 이 연결되지 않았습니다.", this);
                return;
            }
            if (demoComponent == null)
            {
                Debug.LogError($"[{nameof(AlgorithmDemoUI)}] Demo Component 가 연결되지 않았습니다.", this);
                return;
            }

            // [2] 인터페이스 캐스팅. demoComponent 가 IAlgorithmDemo 를 구현하지 않으면 명시적 오류.
            _demo = demoComponent as IAlgorithmDemo;
            if (_demo == null)
            {
                Debug.LogError(
                    $"[{nameof(AlgorithmDemoUI)}] 연결된 컴포넌트 '{demoComponent.GetType().Name}' 가 " +
                    $"IAlgorithmDemo 를 구현하지 않습니다.",
                    this);
                return;
            }

            // [3] 버튼 클릭 → Restart() 연결.
            //     람다가 아닌 메서드 그룹으로 등록하면 OnDestroy 에서 정확히 떼어낼 수 있다.
            restartButton.onClick.AddListener(OnRestartClicked);
        }

        private void OnDestroy()
        {
            // 씬 전환 시 리스너 누수를 막기 위해 정리.
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }
        }

        private void OnRestartClicked()
        {
            // null 체크는 Awake 에서 이미 한 번 했지만, 런타임 중 demo 가 Destroy 됐을 수도 있으므로 방어적 검사.
            if (_demo == null) return;
            _demo.Restart();
        }
    }
}
