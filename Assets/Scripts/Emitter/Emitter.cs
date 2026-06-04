// ============================================================================
// 파일:    Emitter/Emitter.cs
// 프로젝트: Project SEVERANCE
// 용도: 단일 이미터 유닛의 정보를 관리하는 순수 C# 데이터/로직 클래스.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Severance
{
    /// <summary>
    /// MonoBehaviour를 상속받지 않는 순수 C# 클래스로, 그리드 영토 확장의 주체인 이미터(Emitter)를 대변합니다.
    /// 이미터는 고정 좌표 위치, 소유 세력, 자체 레벨, 세력 확장 템플릿 정보를 가지고 있습니다.
    /// </summary>
    public class Emitter
    {
        #region Constants

        /// <summary>방향 오프셋: 위 (+Y).</summary>
        private static readonly Vector2Int DirUp = new Vector2Int(0, 1);

        /// <summary>방향 오프셋: 아래 (-Y).</summary>
        private static readonly Vector2Int DirDown = new Vector2Int(0, -1);

        /// <summary>방향 오프셋: 왼쪽 (-X).</summary>
        private static readonly Vector2Int DirLeft = new Vector2Int(-1, 0);

        /// <summary>방향 오프셋: 오른쪽 (+X).</summary>
        private static readonly Vector2Int DirRight = new Vector2Int(1, 0);

        /// <summary>방향 오프셋: 좌측 상단 대각선.</summary>
        private static readonly Vector2Int DirUpLeft = new Vector2Int(-1, 1);

        /// <summary>방향 오프셋: 우측 상단 대각선.</summary>
        private static readonly Vector2Int DirUpRight = new Vector2Int(1, 1);

        /// <summary>방향 오프셋: 좌측 하단 대각선.</summary>
        private static readonly Vector2Int DirDownLeft = new Vector2Int(-1, -1);

        /// <summary>방향 오프셋: 우측 하단 대각선.</summary>
        private static readonly Vector2Int DirDownRight = new Vector2Int(1, -1);

        #endregion

        #region Read-Only Properties

        /// <summary>이미터가 배치된 그리드 위치 좌표.</summary>
        public Vector2Int Position { get; }

        /// <summary>이 이미터를 통제하는 소유 세력.</summary>
        public Owner Owner { get; }

        /// <summary>이미터 레벨 (자신이 생성하고 확장하는 영토 타일의 레벨 척도).</summary>
        public int Level { get; }

        /// <summary>이미터 확장 영토 방향성을 제어하는 확장 방향 유형 템플릿.</summary>
        public EmitterDirection Direction { get; }

        /// <summary>플레이어 시작 코어처럼 일반 제거 대상이 아닌 핵심 진원인지 여부.</summary>
        public bool IsCore { get; }

        /// <summary>게임 시작 시 생성된 오염 프리셋 진원인지 여부.</summary>
        public bool IsInitialPreset { get; }

        /// <summary>외부 침투처럼 하위 기반 없이 유지되는 특수 적 진원인지 여부.</summary>
        public bool BypassesBaseRequirement { get; }

        #endregion

        #region Mutable State

        /// <summary>
        /// 현재 이미터 전원 동작 상태 (ON/OFF). 
        /// 비활성화(false) 상태인 이미터는 턴 단계 진행 시 영토를 추가 확장하지 못합니다.
        /// </summary>
        public bool IsOn { get; private set; } = true;

        /// <summary>
        /// 이 이미터가 확장 주장하여 실질적 점유 지배를 획득한 영토 타일 좌표 목록.
        /// 이미터 본연의 원천 좌표는 목록에 포함되지 않습니다.
        /// </summary>
        public List<Vector2Int> OwnedTiles { get; } = new List<Vector2Int>();

        /// <summary>
        /// 방향별로 확장 도달 거리를 기록하는 확장 프론티어(경계면) 맵.
        /// Key = 단위 방향 오프셋, Value = 이미터 중심으로부터의 최종 도달 칸 수.
        /// 0으로 시작하여 작동 모드일 때 매 턴마다 1씩 증가해 뻗어나갑니다.
        /// </summary>
        public Dictionary<Vector2Int, int> ExpansionFrontier { get; } = new Dictionary<Vector2Int, int>();

        #endregion

        #region Constructor

        /// <summary>
        /// 명시된 매개변수로 영토 확장 이미터를 생성하고, 템플릿 방향별 프론티어 누적 값을 0으로 설정합니다.
        /// </summary>
        /// <param name="position">배치 지점 좌표.</param>
        /// <param name="owner">점유 세력.</param>
        /// <param name="level">이미터 강도 레벨 (최소 1 이상).</param>
        /// <param name="direction">영토 확장 방향 형태 템플릿.</param>
        public Emitter(Vector2Int position, Owner owner, int level, EmitterDirection direction,
                       bool isCore = false, bool isInitialPreset = false,
                       bool bypassesBaseRequirement = false)
        {
            Position = position;
            Owner = owner;
            Level = Mathf.Max(1, level);
            Direction = direction;
            IsCore = isCore;
            IsInitialPreset = isInitialPreset;
            BypassesBaseRequirement = bypassesBaseRequirement;

            // 이미터 템플릿에서 정의된 모든 방향별 프론티어 거리를 0으로 초기 설정
            List<Vector2Int> directions = GetExpansionDirections();
            foreach (Vector2Int dir in directions)
            {
                ExpansionFrontier[dir] = 0;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 이미터 동작 상태를 켜거나 끕니다 (ON / OFF 토글).
        /// </summary>
        public void Toggle()
        {
            IsOn = !IsOn;
        }

        /// <summary>
        /// 설정된 확장 템플릿 유형(<see cref="Direction"/>)을 분석해 유효하게 작동할 확장 방향 오프셋 벡터 세트를 가져옵니다.
        /// </summary>
        /// <returns>확장 전개가 작동할 방향 단위 오프셋 벡터 리스트.</returns>
        public List<Vector2Int> GetExpansionDirections()
        {
            switch (Direction)
            {
                case EmitterDirection.Up:
                    return new List<Vector2Int> { DirUp };

                case EmitterDirection.Down:
                    return new List<Vector2Int> { DirDown };

                case EmitterDirection.Left:
                    return new List<Vector2Int> { DirLeft };

                case EmitterDirection.Right:
                    return new List<Vector2Int> { DirRight };

                case EmitterDirection.TShape:
                    return new List<Vector2Int> { DirUp, DirLeft, DirRight };

                case EmitterDirection.Cross:
                    return new List<Vector2Int> { DirUp, DirDown, DirLeft, DirRight };

                case EmitterDirection.EightWay:
                    return new List<Vector2Int>
                    {
                        DirUp, DirDown, DirLeft, DirRight,
                        DirUpLeft, DirUpRight, DirDownLeft, DirDownRight
                    };

                default:
                    Debug.LogWarning($"[Emitter] 미지원 확장 이미터 템플릿 정의: {Direction}");
                    return new List<Vector2Int>();
            }
        }

        /// <summary>
        /// 특정 방향으로 확장 전개 시 다음 턴에 침투하여 소유 주장을 던질 목표 타일 좌표를 계산합니다 (프론티어 1칸 외부 좌표).
        /// </summary>
        /// <param name="direction">확장할 방향 오프셋.</param>
        /// <returns>해당 방향에서 다음 점유 후보가 될 타일 좌표.</returns>
        public Vector2Int GetNextExpansionTile(Vector2Int direction)
        {
            int currentDistance = ExpansionFrontier.ContainsKey(direction)
                ? ExpansionFrontier[direction]
                : 0;

            return Position + direction * (currentDistance + 1);
        }

        /// <summary>
        /// 특정 방향으로 한 스텝 영토 지배에 성공했을 때 확장 프론티어 값을 1칸 가산 누적합니다.
        /// </summary>
        /// <param name="direction">갱신할 방향 오프셋.</param>
        public void AdvanceFrontier(Vector2Int direction)
        {
            if (ExpansionFrontier.ContainsKey(direction))
            {
                ExpansionFrontier[direction]++;
            }
            else
            {
                // 방어적 예외 처리: 만일 방향 캐싱 딕셔너리에 없다면 임의 가산 가동
                ExpansionFrontier[direction] = 1;
            }
        }

        #endregion

        #region Object Overrides

        /// <summary>
        /// 디버그 로그 및 출력을 돕는 정보 포맷 오버라이드.
        /// </summary>
        public override string ToString()
        {
            string state = IsOn ? "ON" : "OFF";
            return $"Emitter[{Owner} Lv{Level} {Direction} @{Position} 상태:{state}]";
        }

        #endregion
    }
}
