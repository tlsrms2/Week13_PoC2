// ============================================================================
// 파일:    Core/Enums.cs
// 프로젝트: Project SEVERANCE
// 용도: 모든 게임 시스템에서 사용하는 핵심 열거형(Enum) 정의.
// ============================================================================

namespace Severance
{
    /// <summary>
    /// 타일 또는 이미터의 소유권을 식별합니다.
    /// </summary>
    public enum Owner
    {
        /// <summary>소유자 없음 - 기본/중립 상태.</summary>
        None = 0,
        /// <summary>소유자 없음 - 기본/중립 상태.</summary>
        Neutral = 0,
        /// <summary>플레이어 소유.</summary>
        Player = 1,
        /// <summary>적(오염 물질) 소유.</summary>
        Enemy = 2
    }

    /// <summary>
    /// 타일에 존재할 수 있는 자원 유형입니다.
    /// </summary>
    public enum TileResourceType
    {
        /// <summary>자원 없음.</summary>
        None = 0,
        /// <summary>에너지 노드 - 매 턴 지속적으로 에너지를 생산합니다.</summary>
        PowerNode = 1,
        /// <summary>철광석 광맥 - 일회성 건설 자원.</summary>
        VeinIron = 2,
        /// <summary>구리 광맥 - 일회성 건설 자원.</summary>
        VeinCopper = 3,
        /// <summary>구 버전 호환용 값입니다. 런타임에서는 구리 광맥으로 정규화됩니다.</summary>
        VeinSilicon = 4
    }

    /// <summary>
    /// 이미터가 영토를 확장하는 방향 템플릿입니다.
    /// </summary>
    public enum EmitterDirection
    {
        /// <summary>위쪽 방향으로만 확장 (+Y).</summary>
        Up = 0,
        /// <summary>아래쪽 방향으로만 확장 (-Y).</summary>
        Down = 1,
        /// <summary>왼쪽 방향으로만 확장 (-X).</summary>
        Left = 2,
        /// <summary>오른쪽 방향으로만 확장 (+X).</summary>
        Right = 3,
        /// <summary>T자 모양 확장: 위, 왼쪽, 오른쪽.</summary>
        TShape = 4,
        /// <summary>십자(+) 모양 확장: 4가지 인접 방향.</summary>
        Cross = 5,
        /// <summary>이전 저장 데이터 호환용 값. 런타임에서는 십자 확장으로 정규화됩니다.</summary>
        EightWay = 6
    }

    /// <summary>
    /// 단일 턴 내에서 순차적으로 처리되는 게임 단계(Phase)들입니다.
    /// </summary>
    public enum GamePhase
    {
        /// <summary>플레이어 입력 대기 / 대기 상태.</summary>
        Idle = 0,
        /// <summary>플레이어(아군) 이미터가 영토를 확장하는 단계.</summary>
        AllyExpansion = 1,
        /// <summary>적 이미터가 영토를 확장하는 단계.</summary>
        EnemyExpansion = 2,
        /// <summary>충돌하는 영토 간의 관계를 해결하는 단계.</summary>
        CollisionResolution = 3,
        /// <summary>연쇄 몰락을 방지하기 위한 연결성 및 종속성 확인 단계.</summary>
        DependencyCheck = 4,
        /// <summary>에너지 및 자원 정산 단계.</summary>
        ResourceUpdate = 5
    }

    /// <summary>
    /// 게임 흐름 제어를 위한 고수준 게임 상태입니다.
    /// </summary>
    public enum GameState
    {
        /// <summary>메인 메뉴 또는 게임 시작 전 대기 상태.</summary>
        Menu = 0,
        /// <summary>실제 게임 플레이 중.</summary>
        Playing = 1,
        /// <summary>턴이 진행 중인 상태 (입력 잠금).</summary>
        Processing = 2,
        /// <summary>게임 일시정지.</summary>
        Paused = 3,
        /// <summary>플레이어 승리.</summary>
        Victory = 4,
        /// <summary>플레이어 패배.</summary>
        Defeat = 5
    }
}
