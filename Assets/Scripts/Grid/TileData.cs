// ============================================================================
// 파일:    Grid/TileData.cs
// 프로젝트: Project SEVERANCE
// 용도: 그리드 타일 하나의 상태를 나타내는 순수 C# 데이터 클래스.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Severance
{
    /// <summary>
    /// MonoBehaviour를 상속받지 않는 순수 C# 데이터 클래스로, 
    /// 단일 그리드 타일의 결정 권한 상태(소유자, 레벨, 자원 유형, 배치된 이미터 정보)를 관리합니다.
    /// </summary>
    public class TileData
    {
        #region Properties

        /// <summary>이 타일의 그리드 좌표 좌표계 위치.</summary>
        public Vector2Int Position { get; }

        /// <summary>이 타일을 현재 점유/소유하고 있는 세력(Faction).</summary>
        public Owner Owner { get; private set; }

        /// <summary>타일의 현재 레벨 (테크 티어 요구 사항 등에 반영).</summary>
        public int Level { get; private set; }

        /// <summary>이 타일에 배치된 자원 노드 종류 (자원이 없으면 None).</summary>
        public TileResourceType ResourceType { get; set; }

        /// <summary>자원 타일이 생산 주기마다 제공하는 생산량 (최대 4).</summary>
        public int ResourceYield { get; private set; }

        /// <summary>일회성 광맥 자원이 이미 수거되었는지 여부.</summary>
        public bool IsResourceCollected { get; private set; }

        /// <summary>
        /// 이 타일 위치에 물리적으로 설치된 이미터 (최대 1개).
        /// 이미터가 설치되어 있지 않으면 <c>null</c>입니다.
        /// </summary>
        public Emitter Emitter { get; set; }

        /// <summary>
        /// 이 타일을 자신의 확장 범위로 포섭하고 있는 부모 이미터 세트.
        /// 종속성 체크 및 중첩 레벨 합산 계산에 사용됩니다.
        /// </summary>
        public HashSet<Emitter> ParentEmitters { get; } = new HashSet<Emitter>();

        /// <summary>초기 오염 프리셋 링이 지정한 표시 레벨을 유지해야 하는지 여부.</summary>
        private bool _preservePresetLevel;

        /// <summary>이 타일에 이미터가 물리적으로 설치되어 있는지 여부.</summary>
        public bool IsOccupiedByEmitter => Emitter != null;

        /// <summary>
        /// 겹쳐진 부모 개수 등을 반영하여 타일의 색상 그라데이션을 표현할 때 쓰는 가상 시각 레벨입니다.
        /// </summary>
        public int VisualLevel
        {
            get
            {
                return Owner == Owner.Neutral ? 0 : Level;
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// 지정된 그리드 좌표에 기본 중립 상태의 새로운 타일 인스턴스를 생성합니다.
        /// </summary>
        /// <param name="position">그리드 좌표.</param>
        public TileData(Vector2Int position)
        {
            Position = position;
            Owner = Owner.Neutral;
            Level = 0;
            ResourceType = TileResourceType.None;
            ResourceYield = 0;
            IsResourceCollected = false;
            Emitter = null;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 타일의 소유권과 레벨을 변경하며, 선택적으로 이 소유권을 주장한 부모 이미터를 기록합니다.
        /// </summary>
        /// <param name="owner">새로운 소유 세력.</param>
        /// <param name="level">할당할 타일 레벨 (최소 1 이상으로 제한).</param>
        /// <param name="source">
        /// 소유권 변화를 야기한 이미터 객체. 
        /// 시스템(예: 초기 맵 배치)에 의해 직접 소유권이 바뀔 때는 <c>null</c>을 전달합니다.
        /// </param>
        public void SetOwnership(Owner owner, int level, Emitter source)
        {
            _preservePresetLevel = false;

            if (IsOccupiedByEmitter)
            {
                if (Emitter.Owner != owner)
                {
                    Debug.LogWarning(
                        $"[TileData] 이미터가 설치된 {Position} 타일은 상대 세력 소유권으로 직접 변경할 수 없습니다.");
                    return;
                }

                Owner = Emitter.Owner;

                if (source != null)
                {
                    ParentEmitters.Add(source);
                }

                Level = CalculateParentLevel();
                GameEvents.RaiseTileChanged(Position);
                return;
            }

            Owner = owner;
            Level = Mathf.Max(1, level);

            if (source != null)
            {
                ParentEmitters.Add(source);
            }

            GameEvents.RaiseTileChanged(Position);
        }

        /// <summary>
        /// 초기 오염 프리셋 타일을 지정 레벨로 고정 배치합니다.
        /// 일반 확장이나 탈취가 발생하면 <see cref="SetOwnership"/>을 통해 자동 해제됩니다.
        /// </summary>
        public void SetPresetOwnership(Owner owner, int level, Emitter source)
        {
            if (IsOccupiedByEmitter)
            {
                return;
            }

            Owner = owner;
            Level = Mathf.Clamp(level, 1, 5);
            _preservePresetLevel = true;

            if (source != null)
            {
                ParentEmitters.Add(source);
            }

            GameEvents.RaiseTileChanged(Position);
        }

        /// <summary>
        /// 타일을 레벨 0의 중립 타일로 초기화하고, 연결된 모든 부모 이미터 레퍼런스 및 배치된 이미터(있을 경우)를 해제합니다.
        /// </summary>
        public void ClearOwnership()
        {
            Owner = Owner.Neutral;
            Level = 0;
            _preservePresetLevel = false;
            ParentEmitters.Clear();
            Emitter = null;

            GameEvents.RaiseTileChanged(Position);
        }

        public void SetResource(TileResourceType type, int yield)
        {
            ResourceType = NormalizeResourceType(type);
            ResourceYield = ResourceType == TileResourceType.None ? 0 : Mathf.Clamp(yield, 1, 4);
            GameEvents.RaiseTileChanged(Position);
        }

        public static TileResourceType NormalizeResourceType(TileResourceType type)
        {
            return type == TileResourceType.VeinSilicon ? TileResourceType.VeinCopper : type;
        }

        /// <summary>
        /// 일회성 광맥 자원을 수거 완료 상태로 표시합니다.
        /// </summary>
        public void MarkResourceCollected()
        {
            if (IsResourceCollected)
            {
                return;
            }

            IsResourceCollected = true;
            GameEvents.RaiseTileChanged(Position);
        }

        /// <summary>
        /// 타일에 연결된 활성 부모 이미터 상태와 교차 여부를 기반으로 영역 레벨을 재계산합니다.
        /// </summary>
        public void RecalculateLevel()
        {
            if (IsOccupiedByEmitter)
            {
                Owner previousOwner = Owner;
                int previousLevel = Level;

                Owner = Emitter.Owner;
                Level = CalculateParentLevel();

                if (previousOwner != Owner || previousLevel != Level)
                {
                    GameEvents.RaiseTileChanged(Position);
                }

                return;
            }

            if (Owner == Owner.Neutral)
            {
                if (Level != 0 || Owner != Owner.Neutral)
                {
                    ClearOwnership();
                }

                return;
            }

            if (_preservePresetLevel)
            {
                return;
            }

            if (ParentEmitters.Count == 0)
            {
                ClearOwnership();
                return;
            }

            int parentLevel = CalculateParentLevel();
            if (parentLevel == 0)
            {
                SetLevelIfChanged(0);
                return;
            }

            if (Level != parentLevel)
            {
                SetLevelIfChanged(parentLevel);
            }
        }

        private int CalculateParentLevel()
        {
            int levelSum = 0;
            foreach (Emitter emitter in ParentEmitters)
            {
                if (emitter == null || emitter.Owner != Owner)
                {
                    continue;
                }

                levelSum += emitter.Level;
            }

            if (IsOccupiedByEmitter &&
                Emitter != null &&
                Emitter.Owner == Owner &&
                !ParentEmitters.Contains(Emitter))
            {
                levelSum += Emitter.Level;
            }

            return levelSum > 0 ? Mathf.Clamp(levelSum, 1, 5) : 0;
        }

        private void SetLevelIfChanged(int level)
        {
            int newLevel = Mathf.Clamp(level, 0, 5);
            if (Level == newLevel)
            {
                return;
            }

            Level = newLevel;
            GameEvents.RaiseTileChanged(Position);
        }

        #endregion
    }
}
