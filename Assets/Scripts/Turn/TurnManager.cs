// ============================================================================
// 파일:    Turn/TurnManager.cs
// 프로젝트: Project SEVERANCE
// 용도: 턴 단위 진행 수명(Tick) 단계를 시퀀스 순서로 제어하고
//          수동 진행 및 자동 실행 모드를 지원하는 싱글톤 턴 관리자.
// ============================================================================

using System.Collections;
using UnityEngine;

namespace Severance
{
    /// <summary>
    /// 매 턴(Tick) 주기의 진행 루프를 제어하고 담당하는 싱글톤 매니저입니다.
    /// 각 턴은 정의된 여러 <see cref="GamePhase"/> 단계들을 순차 이행하며,
    /// 비동기 이벤트들의 반응을 대기 및 보장하기 위해 각 단계 사이마다 1프레임의 양보(Yield) 딜레이를 제공합니다.
    /// </summary>
    public class TurnManager : Singleton<TurnManager>
    {
        #region Inspector Fields

        [Header("턴 설정")]
        [Tooltip("전역 게임 밸런스 설정 ScriptableObject 에셋 참조.")]
        [SerializeField] private GameConfig gameConfig;

        #endregion

        #region Public Properties

        /// <summary>
        /// 0부터 누적 카운트되는 현재 완료된 누적 턴 수.
        /// 매 턴 진행 처리가 정상 완료되는 최종 시점에 1씩 가산됩니다.
        /// </summary>
        public int CurrentTurn { get; private set; }

        /// <summary>
        /// 현재 단일 턴 루프 처리가 실행 중인지 여부. 
        /// 턴 전입 처리 중 중복 호출 방지를 위한 동작 보호 Lock 플래그입니다.
        /// </summary>
        public bool IsProcessing { get; private set; }

        /// <summary>
        /// <summary>
        /// 현재 실행 작동 중인 턴 내 세부 단계 정보.
        /// 진행 중인 턴 단계가 없는 경우 <see cref="GamePhase.Idle"/>이 유지됩니다.
        /// </summary>
        public GamePhase CurrentPhase { get; private set; } = GamePhase.Idle;

        #endregion

        #region Private State

        /// <summary>턴 연산 프로세스를 진행하는 코루틴 레퍼런스.</summary>
        private Coroutine _turnCoroutine;

        #endregion

        #region Public API

        /// <summary>
        /// 실행 중인 기존 턴이 없을 때 한 단계 더 다음 턴 가동 루틴을 촉발합니다.
        /// UI 조작 콜백 등에서 호출하기에 안전하며, 턴 동작 중의 중복 트리거는 안전히 무시됩니다.
        /// </summary>
        public void NextTurn()
        {
            if (GameManager.HasInstance && GameManager.Instance.CurrentState != GameState.Playing)
            {
                Debug.LogWarning("[TurnManager] 게임이 Playing 상태가 아니므로 턴 진행을 무시합니다.");
                return;
            }

            if (IsProcessing)
            {
                Debug.LogWarning("[TurnManager] 이미 턴 진행 연산이 수행 중입니다. 요청을 무시합니다.");
                return;
            }

            _turnCoroutine = StartCoroutine(ProcessTurn());
        }

        #endregion

        #region Turn Processing Coroutine

        /// <summary>
        /// 단일 턴의 세부 단계들을 시퀀스화하여 순서대로 실시간 가동하는 코루틴 로직입니다.
        /// </summary>
        private IEnumerator ProcessTurn()
        {
            IsProcessing = true;

            // --- 턴 시작 단계 통지 ---
            Debug.Log($"[TurnManager] ===== 턴 {CurrentTurn} 시작 =====");
            GameEvents.RaiseTurnStarted(CurrentTurn);
            yield return null;

            // --- 1단계: 플레이어 영토 확장 ---
            SetPhase(GamePhase.AllyExpansion);
            EmitterManager emitterMgr = EmitterManager.Instance;
            if (emitterMgr != null)
            {
                emitterMgr.ExpandAll(Owner.Player);
            }
            yield return null;

            // --- 2단계: 적 세력 오염 오염 전개 ---
            SetPhase(GamePhase.EnemyExpansion);
            if (EnemyManager.HasInstance)
            {
                EnemyManager.Instance.TickEnemySpawner(CurrentTurn);
            }
            if (emitterMgr != null)
            {
                emitterMgr.ExpandAll(Owner.Enemy);
            }
            yield return null;

            // --- 3단계: 세력 영토 충돌 해결 (Step 4 구현부) ---
            SetPhase(GamePhase.CollisionResolution);
            if (emitterMgr != null)
            {
                emitterMgr.ResolveCollisions();
            }
            if (GameManager.HasInstance && GameManager.Instance.CurrentState != GameState.Playing)
            {
                yield return FinishTurnEarly();
                yield break;
            }
            yield return null;

            // --- 4단계: 영토 연결성/종속성 연쇄 검사 (Step 6 구현부) ---
            SetPhase(GamePhase.DependencyCheck);
            if (emitterMgr != null)
            {
                yield return StartCoroutine(emitterMgr.PerformDependencyCheckCoroutine());
            }
            if (GameManager.HasInstance && GameManager.Instance.CurrentState != GameState.Playing)
            {
                yield return FinishTurnEarly();
                yield break;
            }
            yield return null;

            // --- 5단계: 에너지/생산 자원 총계 갱신 (Step 7 구현부) ---
            SetPhase(GamePhase.ResourceUpdate);
            if (ResourceManager.HasInstance)
            {
                ResourceManager.Instance.UpdateResources();
            }
            if (FogOfWarManager.HasInstance)
            {
                FogOfWarManager.Instance.Refresh();
            }
            if (GameManager.HasInstance)
            {
                GameManager.Instance.EvaluateEndConditions();
            }
            yield return null;

            // --- 턴 루프 연산 종료 ---
            CurrentTurn++;
            SetPhase(GamePhase.Idle);

            Debug.Log($"[TurnManager] ===== 턴 {CurrentTurn - 1} 완료 (차기 예정 턴: {CurrentTurn}) =====");
            GameEvents.RaiseTurnEnded(CurrentTurn - 1);

            IsProcessing = false;
            _turnCoroutine = null;
        }

        private IEnumerator FinishTurnEarly()
        {
            CurrentTurn++;
            SetPhase(GamePhase.Idle);
            GameEvents.RaiseTurnEnded(CurrentTurn - 1);
            IsProcessing = false;
            _turnCoroutine = null;
            yield return null;
        }

        /// <summary>
        /// 세부 단계를 갱신하고 상태 변경 이벤트를 외부 통지합니다.
        /// </summary>
        /// <param name="phase">신규 적용할 진행 단계.</param>
        private void SetPhase(GamePhase phase)
        {
            CurrentPhase = phase;
            Debug.Log($"[TurnManager] 단계 전환 → {phase}");
            GameEvents.RaiseTurnPhaseChanged(phase);
        }

        #endregion
    }
}
