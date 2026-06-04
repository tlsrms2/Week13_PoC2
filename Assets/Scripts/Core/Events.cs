using System;
using UnityEngine;

namespace Severance
{
    /// <summary>
    /// 게임 시스템 간의 느슨한 결합 통신을 지원하는 정적 이벤트 버스 클래스입니다.
    /// 모든 이벤트는 <see cref="System.Action"/> 델리게이트를 사용합니다.
    /// OnEnable/Awake에서 이벤트를 구독하고, OnDisable/OnDestroy에서 구독을 해제하십시오.
    /// </summary>
    public static class GameEvents
    {
        #region Turn Events

        /// <summary>
        /// 새로운 턴이 시작되는 극초기에 발생합니다.
        /// 매개변수: 턴 번호 (0부터 시작).
        /// </summary>
        public static event Action<int> OnTurnStarted;

        /// <summary>
        /// 턴 진행 과정 중 새로운 단계(Phase)로 전입될 때 발생합니다.
        /// 매개변수: 방금 활성화된 새로운 단계.
        /// </summary>
        public static event Action<GamePhase> OnTurnPhaseChanged;

        /// <summary>
        /// 한 턴의 모든 단계 처리가 정상적으로 완결된 시점에 발생합니다.
        /// 매개변수: 방금 끝난 턴 번호.
        /// </summary>
        public static event Action<int> OnTurnEnded;

        #endregion

        #region Grid / Tile Events

        /// <summary>
        /// 타일의 속성(소유자, 레벨, 자원 등)이 하나라도 변경되었을 때 발생합니다.
        /// 매개변수: 상태가 변경된 타일의 그리드 좌표.
        /// </summary>
        public static event Action<Vector2Int> OnTileChanged;

        #endregion

        #region Emitter Events

        /// <summary>
        /// 그리드 상에 새로운 이미터가 성공적으로 배치되었을 때 발생합니다.
        /// 매개변수: 배치된 이미터 객체.
        /// </summary>
        public static event Action<Emitter> OnEmitterPlaced;

        /// <summary>
        /// 이미터 활성화/비활성화(ON/OFF) 상태가 토글될 때 발생합니다.
        /// 매개변수: 상태가 변경된 이미터 객체.
        /// </summary>
        public static event Action<Emitter> OnEmitterToggled;

        #endregion

        #region Resource Events

        /// <summary>
        /// 플레이어의 자원 총량이 변동되었을 때 발생합니다.
        /// </summary>
        public static event Action OnResourceChanged;

        #endregion

        #region Game State Events

        /// <summary>
        /// 전체적인 게임 고수준 상태(진행 중, 승리, 패배 등)가 변경되었을 때 발생합니다.
        /// 매개변수: 새로운 게임 상태.
        /// </summary>
        public static event Action<GameState> OnGameStateChanged;

        #endregion

        #region Fog of War Events

        /// <summary>
        /// 전장의 안개(Fog of War) 가시성 맵이 재계산되었을 때 발생합니다.
        /// </summary>
        public static event Action OnFogUpdated;

        #endregion

        #region Raise Helpers

        /// <summary><see cref="OnTurnStarted"/> 이벤트를 안전하게 호출합니다.</summary>
        public static void RaiseTurnStarted(int turnNumber)
        {
            OnTurnStarted?.Invoke(turnNumber);
        }

        /// <summary><see cref="OnTurnPhaseChanged"/> 이벤트를 안전하게 호출합니다.</summary>
        public static void RaiseTurnPhaseChanged(GamePhase phase)
        {
            OnTurnPhaseChanged?.Invoke(phase);
        }

        /// <summary><see cref="OnTurnEnded"/> 이벤트를 안전하게 호출합니다.</summary>
        public static void RaiseTurnEnded(int turnNumber)
        {
            OnTurnEnded?.Invoke(turnNumber);
        }

        /// <summary><see cref="OnTileChanged"/> 이벤트를 안전하게 호출합니다.</summary>
        public static void RaiseTileChanged(Vector2Int position)
        {
            OnTileChanged?.Invoke(position);
        }

        /// <summary><see cref="OnEmitterPlaced"/> 이벤트를 안전하게 호출합니다.</summary>
        public static void RaiseEmitterPlaced(Emitter emitter)
        {
            OnEmitterPlaced?.Invoke(emitter);
        }

        /// <summary><see cref="OnEmitterToggled"/> 이벤트를 안전하게 호출합니다.</summary>
        public static void RaiseEmitterToggled(Emitter emitter)
        {
            OnEmitterToggled?.Invoke(emitter);
        }

        /// <summary><see cref="OnResourceChanged"/> 이벤트를 안전하게 호출합니다.</summary>
        public static void RaiseResourceChanged()
        {
            OnResourceChanged?.Invoke();
        }

        /// <summary><see cref="OnGameStateChanged"/> 이벤트를 안전하게 호출합니다.</summary>
        public static void RaiseGameStateChanged(GameState state)
        {
            OnGameStateChanged?.Invoke(state);
        }

        /// <summary><see cref="OnFogUpdated"/> 이벤트를 안전하게 호출합니다.</summary>
        public static void RaiseFogUpdated()
        {
            OnFogUpdated?.Invoke();
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// 모든 이벤트의 구독 리스너를 완전히 해제합니다.
        /// 메인 메뉴로 귀환하거나 게임 세션을 종료할 때 메모리 누수 방지를 위해 호출합니다.
        /// </summary>
        public static void ClearAll()
        {
            OnTurnStarted = null;
            OnTurnPhaseChanged = null;
            OnTurnEnded = null;
            OnTileChanged = null;
            OnEmitterPlaced = null;
            OnEmitterToggled = null;
            OnResourceChanged = null;
            OnGameStateChanged = null;
            OnFogUpdated = null;
        }

        #endregion
    }
}
