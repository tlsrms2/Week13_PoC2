// ============================================================================
// 파일:    Core/GameConfig.cs
// 프로젝트: Project SEVERANCE
// 용도: 전역 게임 밸런스 매개변수를 관리하는 ScriptableObject.
//       기존 BalanceConfig의 모든 필드와 메서드를 통합하여 단일 설정 에셋으로 운영합니다.
// ============================================================================

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Severance
{
    /// <summary>
    /// 게임 전역에서 조율 가능한 매개변수들을 저장하는 ScriptableObject 에셋입니다.
    /// 에디터에서 Assets → Create → Severance → GameConfig 메뉴를 통해 생성할 수 있습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Severance/GameConfig", order = 0)]
    public class GameConfig : ScriptableObject
    {
        #region Grid

        [Header("그리드 설정")]
        [Tooltip("그리드의 가로 칸 수 (열 크기).")]
        [SerializeField] private int gridWidth = 20;

        [Tooltip("그리드의 세로 칸 수 (행 크기).")]
        [SerializeField] private int gridHeight = 20;

        [Tooltip("단일 타일의 월드 규격 크기 (가로/세로 동일).")]
        [SerializeField] private float tileSize = 1.0f;

        [Tooltip("타일과 타일 사이의 추가 간격 (Spacing).")]
        [SerializeField] private float tileSpacing = 0.0f;

        /// <summary>플레이 영역 그리드의 가로 칸 수입니다.</summary>
        public int GridWidth => gridWidth;

        /// <summary>플레이 영역 그리드의 세로 칸 수입니다.</summary>
        public int GridHeight => gridHeight;

        /// <summary>타일 1칸의 가로세로 규격 크기입니다.</summary>
        public float TileSize => tileSize;

        /// <summary>타일 사이의 갭 간격 크기입니다.</summary>
        public float TileSpacing => tileSpacing;

        #endregion

        #region Data Assets

        private const string DefaultStagePath = "Assets/Data/Stage01.asset";

        [Header("데이터 에셋")]
        [Tooltip("스테이지 자원 배치 데이터. 비어 있으면 Assets/Data/Stage01.asset을 사용합니다.")]
        [SerializeField] private StageConfig stageConfig;

        [System.NonSerialized] private StageConfig _cachedDefaultStage;

        public StageConfig Stage => stageConfig != null ? stageConfig : LoadDefaultStage();

        private StageConfig LoadDefaultStage()
        {
#if UNITY_EDITOR
            if (_cachedDefaultStage == null)
            {
                _cachedDefaultStage = AssetDatabase.LoadAssetAtPath<StageConfig>(DefaultStagePath);
            }

            return _cachedDefaultStage;
#else
            return null;
#endif
        }

        #endregion

        #region Levels

        [Header("레벨 설정")]
        [Tooltip("허용되는 최대 이미터 및 타일 레벨.")]
        [SerializeField] private int maxLevel = 5;

        /// <summary>타일 및 이미터 레벨의 절대적 한계치입니다.</summary>
        public int MaxLevel => maxLevel;

        #endregion

        #region Settings Structs

        /// <summary>
        /// 개별 자원의 맵 스폰 및 생산 주기와 생산 수량을 관리하는 직렬화 가능 설정 클래스입니다.
        /// </summary>
        [System.Serializable]
        public class ResourceSetting
        {
            [Tooltip("맵에 무작위 배치할 자원 노드의 개수입니다.")]
            public int count;

            [Tooltip("자원이 생산되는 주기(턴 단위)입니다.")]
            public int productionInterval = 1;

            [Tooltip("생산 주기마다 획득하는 기본 자원량입니다.")]
            public int gainAmount = 1;
        }

        /// <summary>
        /// 이미터 방향 유형별 설치 비용 자원, 비용 개수, 최대 확장 범위를 설정하는 직렬화 가능 클래스입니다.
        /// </summary>
        [System.Serializable]
        public class EmitterSetting
        {
            [Tooltip("진원 설치에 필요한 자원 유형입니다.")]
            public TileResourceType requiredResource = TileResourceType.None;

            [Tooltip("진원 설치 비용(자원 소모량)입니다.")]
            public int cost = 1;

            [Tooltip("진원의 최대 뻗어나가는 범위(칸 수)입니다. 0 이하이면 무제한입니다.")]
            public int maxRange = 0;
        }

        #endregion

        #region Fog of War

        [Header("전장의 안개")]
        [Tooltip("전장의 안개 기능 사용 여부.")]
        [SerializeField] private bool enableFogOfWar = true;

        [Tooltip("소유한 영토 타일 주변의 가시 범위 (타일 단위 반지름).")]
        [SerializeField] private int fogOfWarRadius = 2;

        /// <summary>전장의 안개 활성화 여부입니다.</summary>
        public bool EnableFogOfWar => enableFogOfWar;

        /// <summary>소유한 영토로부터 외곽으로 플레이어가 시야를 확보할 수 있는 타일 거리입니다.</summary>
        public int FogOfWarRadius => fogOfWarRadius;

        #endregion

        #region Resources (전력 · 광맥)

        [Header("자원 및 경제 ─ 전력")]
        [Tooltip("게임 시작 시 플레이어가 보유하는 초기 전력.")]
        [SerializeField] private int initialPower = 12;

        [Tooltip("이미터가 타일을 1칸 확장할 때 소모되는 에너지 양.")]
        [SerializeField] private int powerPerExpansion = 1;

        [Header("자원 세부 설정 (자원별 개별 관리)")]
        [SerializeField] private ResourceSetting powerSetting = new ResourceSetting { count = 8, productionInterval = 1, gainAmount = 3 };
        [SerializeField] private ResourceSetting ironSetting = new ResourceSetting { count = 5, productionInterval = 1, gainAmount = 1 };
        [SerializeField] private ResourceSetting copperSetting = new ResourceSetting { count = 5, productionInterval = 1, gainAmount = 1 };

        /// <summary>게임 시작 시 플레이어 초기 전력입니다.</summary>
        public int InitialPower => initialPower;

        /// <summary>이미터가 영토를 1칸 확장할 때마다 차감되는 에너지 비용입니다.</summary>
        public int PowerPerExpansion => powerPerExpansion;

        public ResourceSetting PowerSetting => powerSetting;
        public ResourceSetting IronSetting => ironSetting;
        public ResourceSetting CopperSetting => copperSetting;

        // --- 이전 버전 호환성 프로퍼티 (의존성 최소화) ---
        public int PowerPerNode => powerSetting.gainAmount;
        public int PowerNodeCount => powerSetting.count;
        public int VeinNodeCount => ironSetting.count + copperSetting.count;
        public int VeinGainAmount => ironSetting.gainAmount;
        public int VeinProductionInterval => ironSetting.productionInterval;

        #endregion

        #region Emitter Placement Costs (진원 설치 비용)

        [Header("진원 세부 설정 (방향별 개별 관리)")]
        [SerializeField] private EmitterSetting upSetting = new EmitterSetting { requiredResource = TileResourceType.VeinIron, cost = 1, maxRange = 0 };
        [SerializeField] private EmitterSetting downSetting = new EmitterSetting { requiredResource = TileResourceType.VeinIron, cost = 1, maxRange = 0 };
        [SerializeField] private EmitterSetting leftSetting = new EmitterSetting { requiredResource = TileResourceType.VeinIron, cost = 1, maxRange = 0 };
        [SerializeField] private EmitterSetting rightSetting = new EmitterSetting { requiredResource = TileResourceType.VeinIron, cost = 1, maxRange = 0 };
        [SerializeField] private EmitterSetting tShapeSetting = new EmitterSetting { requiredResource = TileResourceType.VeinCopper, cost = 1, maxRange = 0 };
        [SerializeField] private EmitterSetting crossSetting = new EmitterSetting { requiredResource = TileResourceType.VeinCopper, cost = 1, maxRange = 0 };
        [SerializeField] private EmitterSetting eightWaySetting = new EmitterSetting { requiredResource = TileResourceType.VeinCopper, cost = 2, maxRange = 5 };

        public EmitterSetting GetEmitterSetting(EmitterDirection direction)
        {
            switch (direction)
            {
                case EmitterDirection.Up: return upSetting;
                case EmitterDirection.Down: return downSetting;
                case EmitterDirection.Left: return leftSetting;
                case EmitterDirection.Right: return rightSetting;
                case EmitterDirection.TShape: return tShapeSetting;
                case EmitterDirection.Cross: return crossSetting;
                case EmitterDirection.EightWay: return eightWaySetting;
                default: return null;
            }
        }

        #endregion

        #region Player Start

        [Header("플레이어 코어")]
        [Tooltip("플레이어 메인 코어를 설치할 시작 좌표.")]
        [SerializeField] private Vector2Int playerCorePosition = new Vector2Int(2, 2);

        [Tooltip("플레이어 코어의 초기 확장 방향.")]
        [SerializeField] private EmitterDirection playerStartDirection = EmitterDirection.Cross;

        /// <summary>플레이어 메인 코어 좌표입니다.</summary>
        public Vector2Int PlayerCorePosition => playerCorePosition;

        /// <summary>플레이어 코어의 초기 확장 방향입니다.</summary>
        public EmitterDirection PlayerStartDirection => playerStartDirection;

        #endregion

        #region Enemy AI (확률 기반 전략)

        [Header("적 AI 행동 확률")]
        [Tooltip("매 턴 적이 전략 행동을 시도할 확률.")]
        [SerializeField] private int enemyActionChance = 70;

        [Tooltip("전략 행동 성공 후 추가 행동을 한 번 더 시도할 확률.")]
        [SerializeField] private int enemyComboActionChance = 15;

        [Tooltip("턴이 지날수록 동적 적 진원 레벨이 상승하는 압력. 100이면 매 턴 1레벨씩 상승합니다.")]
        [SerializeField] private int enemyLevelGrowthChance = 8;

        [Tooltip("동적 진원 생성 시 계산된 레벨보다 1레벨 높은 진원을 시도할 확률.")]
        [SerializeField] private int enemyHighLevelSpikeChance = 10;

        [Header("적 AI 공간 기준")]
        [Tooltip("플레이어 코어 주변 침투 금지 구역. 맵 짧은 변 길이 대비 비율입니다.")]
        [SerializeField] private int enemyCoreSafeZonePercent = 22;

        [Tooltip("새 적 진원끼리 최소한으로 벌리는 거리. 맵 짧은 변 길이 대비 비율입니다.")]
        [SerializeField] private int enemyEmitterSpacingPercent = 10;

        [Header("적 AI 전략 가중치")]
        [SerializeField] private int enemyFrontlineBuildWeight = 28;
        [SerializeField] private int enemySpearheadWeight = 22;
        [SerializeField] private int enemyFanoutWeight = 18;
        [SerializeField] private int enemyHiddenIncursionWeight = 12;
        [SerializeField] private int enemyFlankIncursionWeight = 14;
        [SerializeField] private int enemyResourceRaidWeight = 12;
        [SerializeField] private int enemyConsolidateWeight = 16;

        public int EnemyActionChance => enemyActionChance;
        public int EnemyComboActionChance => enemyComboActionChance;
        public int EnemyLevelGrowthChance => enemyLevelGrowthChance;
        public int EnemyHighLevelSpikeChance => enemyHighLevelSpikeChance;
        public int EnemyCoreSafeZonePercent => enemyCoreSafeZonePercent;
        public int EnemyEmitterSpacingPercent => enemyEmitterSpacingPercent;
        public int EnemyFrontlineBuildWeight => enemyFrontlineBuildWeight;
        public int EnemySpearheadWeight => enemySpearheadWeight;
        public int EnemyFanoutWeight => enemyFanoutWeight;
        public int EnemyHiddenIncursionWeight => enemyHiddenIncursionWeight;
        public int EnemyFlankIncursionWeight => enemyFlankIncursionWeight;
        public int EnemyResourceRaidWeight => enemyResourceRaidWeight;
        public int EnemyConsolidateWeight => enemyConsolidateWeight;

        #endregion

        #region Emitter Cost Methods (진원 비용 계산)

        /// <summary>
        /// 주어진 이미터 방향에 필요한 자원 유형을 반환합니다.
        /// </summary>
        public TileResourceType GetRequiredResource(EmitterDirection direction)
        {
            EmitterSetting setting = GetEmitterSetting(direction);
            return setting != null ? setting.requiredResource : TileResourceType.None;
        }

        /// <summary>
        /// 주어진 이미터 방향의 설치 비용(자원 소모량)을 반환합니다.
        /// </summary>
        public int GetEmitterPlacementCost(EmitterDirection direction)
        {
            EmitterSetting setting = GetEmitterSetting(direction);
            return setting != null ? Mathf.Max(0, setting.cost) : 0;
        }

        /// <summary>
        /// 주어진 이미터 방향의 설치 비용을 사람이 읽기 좋은 문자열로 반환합니다.
        /// </summary>
        public string GetEmitterPlacementCostLabel(EmitterDirection direction)
        {
            TileResourceType resource = GetRequiredResource(direction);
            int cost = GetEmitterPlacementCost(direction);
            if (resource == TileResourceType.None || cost <= 0)
            {
                return "비용 없음";
            }

            return $"{GetResourceLabel(resource)} x{cost}";
        }

        /// <summary>
        /// 자원 유형을 한국어 라벨 문자열로 변환합니다.
        /// </summary>
        public static string GetResourceLabel(TileResourceType resource)
        {
            switch (resource)
            {
                case TileResourceType.PowerNode:
                    return "전력";
                case TileResourceType.VeinIron:
                    return "철";
                case TileResourceType.VeinCopper:
                    return "구리";
                case TileResourceType.VeinSilicon:
                    return "구리";
                default:
                    return "-";
            }
        }

        #endregion
    }
}
