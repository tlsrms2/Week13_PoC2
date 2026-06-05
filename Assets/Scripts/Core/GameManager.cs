using UnityEngine;
using UnityEngine.SceneManagement;

namespace Severance
{
    /// <summary>
    /// 게임 최고 레벨 오케스트레이터 및 관리자 컴포넌트입니다.
    /// 모든 핵심 시스템을 초기화하고, 전역 <see cref="GameConfig"/> 설정 에셋 데이터를 참조하며,
    /// 전역적인 <see cref="GameState"/>(게임 상태) 변화를 총괄합니다.
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        #region Serialized Fields

        [Header("게임 설정")]
        [Tooltip("GameConfig ScriptableObject 에셋을 여기에 할당하세요.")]
        [SerializeField] private GameConfig gameConfig;

        [Header("씬 오브젝트 참조")]
        [Tooltip("씬 내에 배치된 GridVisualizer 컴포넌트.")]
        [SerializeField] private GridVisualizer gridVisualizer;

        [Tooltip("씬 Canvas에 미리 배치한 TurnUI.")]
        [SerializeField] private TurnUI turnUI;

        #endregion

        #region Runtime State

        /// <summary>현재 진행 중인 고수준 게임 상태.</summary>
        private GameState _currentState = GameState.Playing;

        /// <summary>현재 턴 수 (1부터 시작).</summary>
        private int _currentTurn;

        /// <summary>런타임에 확정된 플레이어 코어 좌표.</summary>
        private Vector2Int _playerCorePosition;

        #endregion

        #region Properties

        /// <summary>전역 게임 구성 설정 데이터 에셋 참조.</summary>
        public GameConfig Config
        {
            get
            {
                if (gameConfig == null)
                {
                    Debug.LogError("[GameManager] GameConfig가 인스펙터 상에 할당되지 않았습니다!");
                }
                return gameConfig;
            }
        }

        /// <summary>현재 게임 상태.</summary>
        public GameState CurrentState => _currentState;

        /// <summary>현재 진행 중인 턴 번호 (게임 시작 전에는 0).</summary>
        public int CurrentTurn => _currentTurn;

        /// <summary>씬 내 그리드 시각화 렌더러 참조.</summary>
        public GridVisualizer Visualizer => gridVisualizer;

        #endregion

        #region Unity Lifecycle

        /// <inheritdoc />
        protected override void Awake()
        {
            base.Awake();

            // 중요 데이터 에셋 할당 조기 검사
            if (gameConfig == null)
            {
                Debug.LogError("[GameManager] GameConfig가 인스펙터 상에 할당되지 않았습니다!");
            }
        }

        /// <summary>
        /// Awake 콜백이 완료된 후, 게임 플레이 시작을 준비하며 핵심 시스템을 초기화합니다.
        /// </summary>
        private void Start()
        {
            InitializeSystems();
            ValidateSceneTurnUI();
            StartGame();
        }

        /// <summary>
        /// 유니티 종료 시 이벤트 누수를 방지하기 위해 상위 소멸 처리를 수행합니다.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// <see cref="GameConfig"/>의 가로/세로 설정을 바탕으로 그리드 데이터 및 시각화 그리드를 초기화합니다.
        /// </summary>
        private void InitializeSystems()
        {
            if (gameConfig == null)
            {
                Debug.LogError("[GameManager] GameConfig가 누락되어 초기화 과정을 속행할 수 없습니다.");
                return;
            }

            int width = gameConfig.GridWidth;
            int height = gameConfig.GridHeight;

            // --- 그리드 논리 데이터 초기화 ---
            GridManager.Instance.Initialize(width, height);

            // --- 자원/적 초기 상태 준비 ---
            ResourceManager.Instance.Initialize(gameConfig);
            ResourceManager.Instance.SeedResources();
            PlacePlayerCore();
            EnemyManager.Instance.Initialize(gameConfig);
            FogOfWarManager.Instance.Initialize(gameConfig);

            // --- 그리드 씬 비주얼 스폰 ---
            if (gridVisualizer != null)
            {
                gridVisualizer.SpawnGrid(width, height);
                FogOfWarManager.Instance.Refresh();
            }
            else
            {
                Debug.LogWarning("[GameManager] GridVisualizer가 할당되지 않아 시각적 그리드가 스폰되지 않습니다.");
            }

            Debug.Log("[GameManager] 모든 핵심 시스템 초기화 완료.");
        }

        private void PlacePlayerCore()
        {
            if (!EmitterManager.HasInstance && GridManager.HasInstance)
            {
                _ = EmitterManager.Instance;
            }

            Vector2Int corePos = gameConfig.PlayerCorePosition;
            if (!GridManager.Instance.IsInBounds(corePos))
            {
                corePos = new Vector2Int(
                    Mathf.Clamp(corePos.x, 0, gameConfig.GridWidth - 1),
                    Mathf.Clamp(corePos.y, 0, gameConfig.GridHeight - 1));
            }
            _playerCorePosition = corePos;

            if (EmitterManager.Instance.TryPlaceEmitter(
                    corePos,
                    Owner.Player,
                    1,
                    gameConfig.PlayerStartDirection,
                    isCore: true,
                    ignorePlacementRules: true,
                    isInitialPreset: true,
                    bypassesBaseRequirement: true))
            {
                EmitterManager.Instance.SeedStartingArea(EmitterManager.Instance.GetEmitterAt(corePos));
            }
        }

        private void ValidateSceneTurnUI()
        {
            if (turnUI == null)
            {
                turnUI = FindAnyObjectByType<TurnUI>();
            }

            if (turnUI == null)
            {
                Debug.LogWarning("[GameManager] 씬에 TurnUI가 없습니다. Canvas 아래에 TurnUI를 배치하고 UI 참조를 연결해 주세요.");
            }
        }

        #endregion

        #region Game Flow

        /// <summary>
        /// 새로운 게임 플레이 세션을 초기화하고 가동합니다.
        /// 턴 카운트를 0으로 초기화하고 상태를 Playing으로 변환합니다.
        /// </summary>
        public void StartGame()
        {
            _currentTurn = 0;
            SetGameState(GameState.Playing);

            Debug.Log("[GameManager] 게임 세션 시작.");
        }

        /// <summary>
        /// 게임의 전역 상태를 전환하고 시스템에 알리는 이벤트를 발생시킵니다.
        /// </summary>
        /// <param name="newState">바꾸고자 하는 신규 상태.</param>
        public void SetGameState(GameState newState)
        {
            if (_currentState == newState) return;

            _currentState = newState;
            GameEvents.RaiseGameStateChanged(_currentState);

            Debug.Log($"[GameManager] 상태 전환 → {_currentState}");
        }

        /// <summary>
        /// 턴 종료 시 승패 조건을 평가합니다.
        /// </summary>
        public void EvaluateEndConditions()
        {
            if (_currentState != GameState.Playing)
            {
                return;
            }

            GridManager grid = GridManager.Instance;
            if (grid == null || !grid.IsInitialized)
            {
                return;
            }

            TileData coreTile = grid.GetTile(_playerCorePosition);
            if (coreTile == null || coreTile.Emitter == null || !coreTile.Emitter.IsCore || coreTile.Owner != Owner.Player)
            {
                SetGameState(GameState.Defeat);
                return;
            }

            bool hasEnemy = false;
            foreach (TileData tile in grid.AllTiles)
            {
                if (tile.Owner == Owner.Enemy)
                {
                    hasEnemy = true;
                    break;
                }
            }

            if (!hasEnemy && EmitterManager.HasInstance && EmitterManager.Instance.GetEmitters(Owner.Enemy).Count > 0)
            {
                hasEnemy = true;
            }

            if (!hasEnemy)
            {
                SetGameState(GameState.Victory);
            }
        }

        public void RestartGame()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.buildIndex >= 0)
            {
                SceneManager.LoadScene(activeScene.buildIndex);
                return;
            }

            SceneManager.LoadScene(activeScene.name);
        }

        /// <summary>
        /// 다음 턴으로 전입하며 관련 시작 및 종료 이벤트를 알립니다.
        /// (실제로는 TurnManager에 의해 제어되는 턴 루프의 기본 프레임워크입니다)
        /// </summary>
        public void AdvanceTurn()
        {
            if (_currentState != GameState.Playing)
            {
                Debug.LogWarning("[GameManager] 게임이 Playing 상태가 아니므로 턴을 진행할 수 없습니다.");
                return;
            }

            _currentTurn++;
            GameEvents.RaiseTurnStarted(_currentTurn);

            // 턴 단계별 실제 동작은 TurnManager에 의해 제어됩니다.
            // 여기서는 테스트를 위해 단순 턴 종료 전입 처리를 임시로 병행합니다.
            GameEvents.RaiseTurnEnded(_currentTurn);
        }

        #endregion
    }
}
