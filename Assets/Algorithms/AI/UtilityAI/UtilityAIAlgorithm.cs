using System;
using System.Collections.Generic;

namespace Algorithms.AI
{
    /// <summary>
    /// =====================================================================
    ///  Utility AI — 점수 함수 기반 의사결정 (Score-based Decision Making)
    /// =====================================================================
    ///
    /// 1. 알고리즘 개요
    /// ---------------------------------------------------------------------
    /// Utility AI 는 *디자이너가 트리를 그리는* Behavior Tree 와도 다르고,
    /// *상태 공간을 탐색하는* GOAP 와도 다르다.
    /// 디자이너가 만들어 두는 건 *각 행동의 점수 함수* 뿐 — 매 tick 모든 행동의
    /// 점수를 계산해서 *가장 높은* 점수의 행동을 선택한다. 그게 전부다.
    ///
    /// "의사결정 = 점수 비교" 라는 단순한 골격이지만, 점수 함수를 고려사항(Consideration)
    /// 의 *곱* 으로 분해하면서 표현력이 폭발적으로 늘어난다. The Sims, RimWorld, Kingdoms
    /// of Amalur, Skyrim Radiant AI 등 *욕구 / 상황 가중 NPC* 류에서 사실상 표준.
    ///
    ///   행동의 점수 = (Consideration_1 점수) × (Consideration_2 점수) × ... × CompensationFactor
    ///
    /// 각 Consideration 은 *0~1 정규화된 입력* (예: hunger / 100) 을 *Response Curve* 에
    /// 통과시켜 *0~1 출력 점수* 를 만든다. 곱셈이라는 게 핵심: 한 고려사항이라도 0 이면
    /// 행동의 전체 점수가 0 — "배고프지 않으면 먹기 점수도 0" 이 자연스럽게 표현된다.
    ///
    /// 2. 동작 흐름 (매 tick 의사결정)
    /// ---------------------------------------------------------------------
    ///   ① 각 Action 별로:
    ///        a. 각 Consideration 의 입력값을 가져온다 (예: hunger, distance).
    ///        b. 입력을 [0,1] 로 정규화 (Range 로 클램프).
    ///        c. Response Curve 에 통과시켜 [0,1] 출력 점수.
    ///        d. 모든 Consideration 점수를 곱한다 → 원시 점수.
    ///        e. CompensationFactor 적용 — 곱이 너무 빨리 작아지는 걸 보정.
    ///        f. 행동 가중치(Weight) 와 곱한다 → 최종 점수.
    ///   ② 가장 높은 점수의 Action 을 반환.
    ///   ③ (선택) 진행 중인 Action 에 인터럽트 임계값 적용 — 새 후보가 충분히 더 높을 때만 전환.
    ///
    ///   ※ 핵심 차이점 (vs GOAP):
    ///     GOAP : "지금 → 목표" *사슬을 미리 계획* → 계획대로 움직이는 동안엔 환경 변화에 둔감.
    ///     UAI  : *매 tick 처음부터 다시 결정* → 환경이 바뀌면 즉각 반응. 대신 *멀리 보지 못함*.
    ///
    /// 3. 시간 / 공간 복잡도
    /// ---------------------------------------------------------------------
    ///   - 시간 (per tick) : O(A × C)   A = 행동 수, C = 행동당 Consideration 수 (보통 둘 다 작음)
    ///   - 공간            : O(A × C)   카탈로그 자체 + 디버그 스냅샷
    ///
    ///   GOAP 의 O(N · (|A| + log N)) 와 비교하면 *압도적으로 빠르다* — Utility AI 는 매 프레임
    ///   돌려도 부담 없는 수준이고, 실제로 The Sims 류는 매 프레임 평가한다.
    ///
    /// 4. 사용 자료구조와 그 이유
    /// ---------------------------------------------------------------------
    ///   - Consideration (입력 함수 + ResponseCurve)
    ///       : "어떤 변수가 어떤 모양으로 점수에 반영되는가" 를 *데이터로 분리*. 같은 hunger 라도
    ///         "선형으로 증가" 와 "70 % 넘어가면 급증" 은 디자이너가 곡선만 바꿔서 표현 가능.
    ///   - UtilityAction (Considerations + Weight)
    ///       : 행동 = 고려사항들의 곱 + 행동 자체의 무게(Weight). 같은 점수라도 Weight 로 우선순위
    ///         미세 조정 가능.
    ///   - UtilityContext (행동들이 공유하는 입력 dict)
    ///       : Consideration 이 입력을 가져올 때 사용. Unity 의존을 격리하는 *순수 데이터* 컨테이너.
    ///   - DecisionSnapshot (디버그 결과)
    ///       : 매 tick 모든 행동의 점수를 그대로 보관 → 우측 OnGUI 패널이 막대 그래프로 그릴 수 있음.
    ///         *왜 이 행동이 선택됐는가* 를 사후 검증할 때 핵심.
    ///
    /// 5. 다른 의사결정 알고리즘과의 관계
    /// ---------------------------------------------------------------------
    ///   - Behavior Tree : *결정론적* 트리 평가. UAI 와 달리 "조건 → 행동" 의 사다리 형태.
    ///                     설계와 디버깅이 쉽지만, 우선순위 미세 조정이 어렵다 (트리 구조 자체를
    ///                     다시 그려야 할 수 있음).
    ///   - GOAP          : 사슬을 *계획*. 멀리 보는 데는 강하지만 환경 변화에 둔감.
    ///                     UAI 는 멀리 못 봐도 *반응 속도* 가 압도적.
    ///   - FSM           : 상태 + 전이. UAI 와 결이 비슷하지만 *전이를 일일이 정의* 해야 한다.
    ///                     UAI 는 모든 상태로 *항상* 전이 가능 — 점수만 보고.
    ///   - Minimax/MCTS  : *상대* 가 있는 게임용. UAI 는 상대를 가정하지 않는 단일 에이전트 결정.
    ///
    /// ▶ 주요 변형 (관심 있다면 검색용 키워드)
    ///   - IAUS (Infinite Axis Utility System) — Dave Mark 의 표준 설계.
    ///   - Compensation Factor                  — 곱셈 점수의 조기 0 수렴을 보정.
    ///   - Inertia / Hysteresis Bonus           — 진행 중 행동에 보너스를 줘 flip-flop 방지 (이 데모도 사용).
    ///   - Dual-utility (rank + score)          — 행동을 그룹으로 묶고 그룹 안에서만 비교.
    /// </summary>
    public enum ResponseCurve
    {
        /// <summary>y = x — 입력에 그대로 비례. 가장 단순한 곡선.</summary>
        Linear,
        /// <summary>y = x² — 입력이 클수록 *빠르게* 증가. 임계값 근처에서 폭발.</summary>
        Quadratic,
        /// <summary>y = √x — 입력이 작을 때 *빠르게* 증가, 클수록 둔화. "조금만 있어도 충분".</summary>
        SquareRoot,
        /// <summary>S 자형 (sigmoid). 중앙에서 급격, 양 끝에서 평평. *임계 통과* 의 의미를 잘 표현.</summary>
        Logistic,
        /// <summary>y = 1 - x — 입력이 클수록 *낮은* 점수. 거리·위협처럼 "작을수록 좋은" 입력.</summary>
        Inverse,
        /// <summary>y = (1 - x)² — Inverse 의 가속 버전. 가까울수록 폭발적으로 좋음.</summary>
        InverseQuadratic,
    }

    /// <summary>
    /// Utility AI 가 행동 점수 계산에 참조하는 *공유 입력 컨테이너*. 키 → float 단순 dict.
    /// Visualizer 가 매 tick 갱신해서 Brain.Decide 에 넘긴다.
    /// 단순 dict 인 이유: Unity 의존을 알고리즘 쪽에서 *완전히 떼어내기* 위함. UAI 본체는 입력이
    /// 어디서 왔는지 (NPC 위치 / 적 위치 / 시간) 전혀 모른다 — 그냥 키-값을 본다.
    /// </summary>
    public sealed class UtilityContext
    {
        private readonly Dictionary<string, float> _values = new();

        public void Set(string key, float value) => _values[key] = value;

        /// <summary>없는 키는 0 으로 간주 — 안전한 기본값.</summary>
        public float Get(string key) => _values.TryGetValue(key, out var v) ? v : 0f;

        public bool Has(string key) => _values.ContainsKey(key);

        public IEnumerable<KeyValuePair<string, float>> All => _values;
    }

    /// <summary>
    /// 단일 고려사항. *어떤 입력을 가져와서 어떤 곡선으로 0~1 점수로 만들 것인가*.
    ///
    /// ▶ 입력 흐름:
    ///     raw = inputProvider(ctx)              ← 컨텍스트에서 raw 값 추출 (예: hunger, distance)
    ///     normalized = (raw - InputMin) / (InputMax - InputMin), [0,1] 클램프
    ///     score = Curve(normalized)             ← Response Curve 통과
    ///
    /// ▶ 디자이너는 *세 가지만* 정한다:
    ///     1) 어떤 변수를 보는지 (inputProvider)
    ///     2) 그 변수의 입력 범위 (InputMin/Max)
    ///     3) 어떤 곡선 (Curve)
    /// </summary>
    public sealed class Consideration
    {
        public string Label = "(noname)";
        public Func<UtilityContext, float> InputProvider;
        public float InputMin;
        public float InputMax = 1f;
        public ResponseCurve Curve = ResponseCurve.Linear;

        /// <summary>0~1 점수를 계산. raw 값과 normalized 도 같이 반환 — 디버그 패널 표시용.</summary>
        public ConsiderationScore Evaluate(UtilityContext ctx)
        {
            // [1-a] raw 입력 추출.
            float raw = InputProvider != null ? InputProvider(ctx) : 0f;

            // [1-b] [0,1] 정규화 + 클램프 — 입력 범위 밖이어도 안전하게.
            float t;
            if (InputMax - InputMin <= 0f)
            {
                t = 0f;
            }
            else
            {
                t = (raw - InputMin) / (InputMax - InputMin);
                if (t < 0f) t = 0f;
                else if (t > 1f) t = 1f;
            }

            // [1-c] Response Curve 통과.
            float score = Curve switch
            {
                ResponseCurve.Linear           => t,
                ResponseCurve.Quadratic        => t * t,
                ResponseCurve.SquareRoot       => (float)Math.Sqrt(t),
                // 중심 0.5, 기울기 12 의 sigmoid — 임계 통과 모양.
                ResponseCurve.Logistic         => 1f / (1f + (float)Math.Exp(-12f * (t - 0.5f))),
                ResponseCurve.Inverse          => 1f - t,
                ResponseCurve.InverseQuadratic => (1f - t) * (1f - t),
                _ => t,
            };

            return new ConsiderationScore { Label = Label, Raw = raw, Normalized = t, Score = score };
        }
    }

    /// <summary>한 Consideration 의 평가 결과 (디버그 패널용).</summary>
    public struct ConsiderationScore
    {
        public string Label;
        public float Raw;
        public float Normalized;
        public float Score;
    }

    /// <summary>
    /// 행동. *Considerations 의 곱* + Weight 로 최종 점수를 만든다.
    ///
    /// ▶ Compensation Factor (Dave Mark, IAUS)
    ///   곱셈이라는 특성상 Consideration 이 N 개일 때 각 점수가 0.9 라도 결과가
    ///   0.9^N 으로 빠르게 작아진다. 이를 보정해 "곱셈은 그대로 유지하되 N 이 클수록
    ///   덜 깎이게" 만드는 표준 트릭:
    ///       compensated = raw + (1 - raw) × ( (1 - 1/N) × (1 - raw) )
    ///   이 보정은 *원시 점수의 의미를 보존하면서* (0 은 0, 1 은 1) 중간 영역만 끌어올린다.
    ///
    /// ▶ Weight
    ///   Consideration 곱 외의 *행동 자체의 우선순위*. 1.0 이 기본. 같은 곱 점수라도
    ///   "이 행동은 평소에 1.5 배 가중" 같은 미세 조정이 가능.
    /// </summary>
    public sealed class UtilityAction
    {
        public string Name = "(noname)";
        public List<Consideration> Considerations = new();
        public float Weight = 1f;

        /// <summary>
        /// 점수와 디버그 정보를 한꺼번에 반환. *원시*와 *보정*을 모두 노출해 우측 패널에서
        /// "이 행동이 왜 N 점인지" 가 보이도록 한다.
        /// </summary>
        public ActionScore Evaluate(UtilityContext ctx)
        {
            var result = new ActionScore { Name = Name, Weight = Weight };
            result.Considerations = new List<ConsiderationScore>(Considerations.Count);

            // [1-d] Consideration 곱 — 하나라도 0 이면 결과는 0.
            float product = 1f;
            int n = Considerations.Count;
            foreach (var c in Considerations)
            {
                var cs = c.Evaluate(ctx);
                result.Considerations.Add(cs);
                product *= cs.Score;
                // 조기 종료 — 0 이 한 번 나오면 더 곱해도 의미 없음. 디버그 점수는 계속 채운다.
            }

            result.RawScore = product;

            // [1-e] CompensationFactor 적용. n=1 일 땐 항등 (1 - 1/1 = 0).
            float compensated = product;
            if (n > 1 && product > 0f && product < 1f)
            {
                float modFactor = 1f - (1f / n);
                float makeup    = (1f - product) * modFactor;
                compensated     = product + makeup * product;
            }
            result.CompensatedScore = compensated;

            // [1-f] Weight 적용 → 최종.
            result.FinalScore = compensated * Weight;
            return result;
        }
    }

    /// <summary>한 행동의 평가 결과 (우측 OnGUI 패널이 그대로 그림).</summary>
    public sealed class ActionScore
    {
        public string Name;
        public float Weight;
        public float RawScore;          // Consideration 곱
        public float CompensatedScore;  // 보정 후
        public float FinalScore;        // × Weight
        public List<ConsiderationScore> Considerations;
    }

    /// <summary>
    /// 한 tick 의 의사결정 결과 묶음. Brain.Decide 의 반환값이며, 시각화 패널이 이 한 객체만
    /// 보고 모든 정보 (선택된 행동 / 모든 점수 / 인터럽트 여부) 를 그릴 수 있다.
    /// </summary>
    public sealed class DecisionSnapshot
    {
        public List<ActionScore> AllScores = new();
        public UtilityAction Chosen;
        public ActionScore   ChosenScore;
        public bool Interrupted;     // 진행 중 행동에서 다른 행동으로 바뀌었는가
        public string PreviousName;
        public float ChosenFinal => ChosenScore?.FinalScore ?? 0f;
    }

    /// <summary>
    /// Utility AI 의 본체. *순수 알고리즘* — Unity 의존 0.
    ///
    /// 사용법:
    ///   1) actions 카탈로그 등록.
    ///   2) 매 tick UtilityContext 채워서 Decide(ctx, currentAction) 호출.
    ///   3) 반환된 DecisionSnapshot.Chosen 행동을 수행.
    ///
    /// ▶ Hysteresis (인터럽트 임계값)
    ///   진행 중인 행동이 있으면 "새 후보의 점수가 *현재 행동 점수 + interruptThreshold* 이상" 일 때만 전환.
    ///   Utility AI 의 약점인 *flip-flop* (두 행동이 비슷한 점수일 때 매 tick 번갈아 바뀜) 을 막는 표준 트릭.
    /// </summary>
    public sealed class UtilityAIBrain
    {
        public IReadOnlyList<UtilityAction> Actions => _actions;
        private readonly List<UtilityAction> _actions = new();

        /// <summary>
        /// 진행 중 행동이 있을 때, 새 후보가 *얼마나 더 높아야* 전환할지의 임계값.
        /// 0 이면 인터럽트 없음 (가장 점수 높은 행동을 매 tick 그대로 채택).
        /// </summary>
        public float InterruptThreshold = 0.05f;

        public void AddAction(UtilityAction a) => _actions.Add(a);

        /// <summary>
        /// [2] [3] 모든 행동 평가 → 최고점 선택 → (필요 시) 인터럽트 임계값 적용.
        /// </summary>
        public DecisionSnapshot Decide(UtilityContext ctx, UtilityAction currentAction = null)
        {
            var snap = new DecisionSnapshot { PreviousName = currentAction?.Name };

            // [2] 모든 행동 평가.
            ActionScore best = null;
            UtilityAction bestAction = null;
            ActionScore currentScore = null;

            foreach (var a in _actions)
            {
                var s = a.Evaluate(ctx);
                snap.AllScores.Add(s);

                if (a == currentAction) currentScore = s;

                if (best == null || s.FinalScore > best.FinalScore)
                {
                    best = s;
                    bestAction = a;
                }
            }

            // [3] Hysteresis — 진행 중 행동이 있고, 새 후보가 같은 행동이 아니라면 임계값 검사.
            if (currentAction != null && bestAction != currentAction && currentScore != null)
            {
                if (best.FinalScore < currentScore.FinalScore + InterruptThreshold)
                {
                    // 인터럽트할 만큼 충분히 높지 않음 — 진행 중인 행동 유지.
                    bestAction  = currentAction;
                    best        = currentScore;
                    snap.Interrupted = false;
                }
                else
                {
                    snap.Interrupted = true;
                }
            }

            snap.Chosen      = bestAction;
            snap.ChosenScore = best;
            return snap;
        }
    }
}
