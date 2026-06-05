// ============================================================================
// 파일:    UI/TurnUI.cs
// 프로젝트: Project SEVERANCE
// 용도: 턴 컨트롤을 위한 심플 HUD 컴포넌트 (턴 카운터, 다음 턴 진행 버튼, 자동 진행 토글).
// ============================================================================

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Severance
{
    /// <summary>
    /// 현재 누적 완료된 턴 번호를 보여주고, 턴 수동 진행 버튼 및 자동 턴 진행 기능 토글 버튼을 관리하는 UI 스크립트입니다.
    /// </summary>
    public class TurnUI : MonoBehaviour
    {
        #region Inspector Fields

        [Header("UI 컴포넌트 참조")]
        [Tooltip("현재 진행 중인 턴 번호를 출력하는 텍스트 컴포넌트.")]
        [SerializeField] private TextMeshProUGUI turnCountText;

        [Tooltip("수동으로 즉시 다음 턴을 진행하게 하는 버튼.")]
        [SerializeField] private Button nextTurnButton;

        [Tooltip("자동 턴 진행 모드를 토글하는 버튼.")]
        [SerializeField] private Button autoPlayButton;

        [Header("자동 진행 버튼 텍스트 라벨")]
        [Tooltip("자동 진행 모드 상태 문자열 출력을 위한 UI 라벨 텍스트 컴포넌트.")]
        [SerializeField] private TextMeshProUGUI autoPlayButtonText;

        [Tooltip("전력 및 광맥 자원 현황을 출력할 텍스트 컴포넌트.")]
        [SerializeField] private TextMeshProUGUI resourceText;

        [Tooltip("전력량을 표시할 선택형 슬라이더.")]
        [SerializeField] private Slider powerSlider;

        [Tooltip("승리/패배 결과를 출력할 텍스트 컴포넌트.")]
        [SerializeField] private TextMeshProUGUI resultText;

        [Tooltip("결과 화면에서 게임을 다시 시작하는 버튼.")]
        [SerializeField] private Button restartButton;

        [Header("배치 UI")]
        [SerializeField] private Button directionButton;
        [SerializeField] private TextMeshProUGUI directionButtonText;
        [SerializeField] private Button levelButton;
        [SerializeField] private TextMeshProUGUI levelButtonText;
        [SerializeField] private Button placeLevel1Button;
        [SerializeField] private Button placeLevel2Button;
        [SerializeField] private Button placeLevel3Button;
        [SerializeField] private TextMeshProUGUI placementModeText;
        [SerializeField] private RectTransform directionOptionsPanel;
        [SerializeField] private RectTransform levelOptionsPanel;
        [SerializeField] private RectTransform resultPanel;

        [Header("속도/툴팁 UI")]
        [SerializeField] private Button speedButton;
        [SerializeField] private TextMeshProUGUI speedButtonText;
        [SerializeField] private TextMeshProUGUI tileInfoText;
        [SerializeField] private TextMeshProUGUI warningText;

        [Header("UI 텍스트 크기 & 스타일 설정")]
        [SerializeField] private float turnCountFontSize = 24f;
        [SerializeField] private float resourceFontSize = 16f;
        [SerializeField] private float placementModeFontSize = 15f;
        [SerializeField] private float tileInfoFontSize = 14f;
        [SerializeField] private float resultFontSize = 34f;
        [SerializeField] private float buttonLabelFontSize = 15f;

        [Header("UI 텍스트 포맷")]
        [SerializeField] private string turnPrefixText = "턴: ";
        [SerializeField] private string processingLabelText = "턴 진행 중…";

        #endregion

        #region Runtime State

        private RectTransform _runtimeResultPanel;
        private RectTransform _directionOptionsPanel;
        private RectTransform _levelOptionsPanel;
        private Coroutine _warningCoroutine;
        private bool _sceneOptionButtonsBound;

        #endregion

        #region Constants;

        private static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.07f, 0.82f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.22f, 0.30f, 0.96f);
        private static readonly Color ButtonHighlightColor = new Color(0.24f, 0.34f, 0.46f, 1f);
        private static readonly Color ButtonPressedColor = new Color(0.09f, 0.13f, 0.18f, 1f);
        private static readonly Color TextColor = new Color(0.92f, 0.96f, 1f, 1f);
        private static readonly Color AccentColor = new Color(0.24f, 0.58f, 0.95f, 1f);
        private static readonly Color NeutralInfoColor = new Color(0.74f, 0.78f, 0.84f, 1f);
        private static readonly Color PlayerInfoColor = new Color(0.36f, 0.72f, 1f, 1f);
        private static readonly Color EnemyInfoColor = new Color(1f, 0.34f, 0.34f, 1f);
        private static readonly Color ResourceInfoColor = new Color(0.45f, 1f, 0.62f, 1f);
        private static readonly Color EmitterInfoColor = new Color(1f, 0.86f, 0.32f, 1f);

        private readonly EmitterDirection[] _directionOptions =
        {
            EmitterDirection.Up,
            EmitterDirection.Down,
            EmitterDirection.Left,
            EmitterDirection.Right,
            EmitterDirection.TShape,
            EmitterDirection.Cross
        };

        private int _directionIndex = 5;
        private int _selectedLevel = 1;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            InitializeSceneUI();

            // 게임 내 이벤트 구독
            GameEvents.OnTurnStarted += HandleTurnStarted;
            GameEvents.OnTurnEnded += HandleTurnEnded;
            GameEvents.OnResourceChanged += HandleResourceChanged;
            GameEvents.OnGameStateChanged += HandleGameStateChanged;
            InputHandler.OnTileHovered += HandleTileHovered;
            GameEvents.OnEmitterToggled += HandleEmitterToggled;

            // UI 버튼 조작 콜백 함수 연결
            if (nextTurnButton != null)
            {
                nextTurnButton.onClick.AddListener(OnNextTurnClicked);
            }

            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
            if (directionButton != null) directionButton.onClick.AddListener(ToggleDirectionOptions);
            if (levelButton != null) levelButton.onClick.AddListener(ToggleLevelOptions);

            if (autoPlayButton != null) autoPlayButton.gameObject.SetActive(false);
            if (speedButton != null) speedButton.gameObject.SetActive(false);
            if (powerSlider != null) powerSlider.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            // 게임 내 이벤트 구독 해제
            GameEvents.OnTurnStarted -= HandleTurnStarted;
            GameEvents.OnTurnEnded -= HandleTurnEnded;
            GameEvents.OnResourceChanged -= HandleResourceChanged;
            GameEvents.OnGameStateChanged -= HandleGameStateChanged;
            InputHandler.OnTileHovered -= HandleTileHovered;
            GameEvents.OnEmitterToggled -= HandleEmitterToggled;

            // UI 버튼 콜백 해제
            if (nextTurnButton != null)
            {
                nextTurnButton.onClick.RemoveListener(OnNextTurnClicked);
            }

            if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartClicked);
            if (directionButton != null) directionButton.onClick.RemoveListener(ToggleDirectionOptions);
            if (levelButton != null) levelButton.onClick.RemoveListener(ToggleLevelOptions);
        }

        private void Start()
        {
            InitializeSceneUI();

            // 초기 디스플레이 상태 갱신
            UpdateTurnDisplay(0);
            UpdateResourceDisplay();
            UpdateResultDisplay(GameState.Playing);
            UpdatePlacementLabel();
        }

        #endregion

        #region Scene UI

        private void InitializeSceneUI()
        {
            _directionOptionsPanel = directionOptionsPanel;
            _levelOptionsPanel = levelOptionsPanel;
            _runtimeResultPanel = resultPanel;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                Debug.LogWarning("[TurnUI] 씬에 EventSystem이 없습니다. Canvas 버튼 입력을 위해 EventSystem을 배치해 주세요.");
            }

            ValidateRequiredReferences();
            BindSceneOptionButtons();
        }

        private void ValidateRequiredReferences()
        {
            if (turnCountText == null ||
                nextTurnButton == null ||
                resourceText == null ||
                resultText == null ||
                restartButton == null ||
                directionButton == null ||
                directionButtonText == null ||
                levelButton == null ||
                levelButtonText == null ||
                placementModeText == null ||
                tileInfoText == null)
            {
                Debug.LogWarning("[TurnUI] 씬에 배치된 TurnUI의 UI 컴포넌트 참조가 일부 비어 있습니다. 인스펙터에서 연결해 주세요.");
            }
        }

        private void BindSceneOptionButtons()
        {
            if (_sceneOptionButtonsBound)
            {
                return;
            }

            BindDirectionOptionButtons();
            BindLevelOptionButtons();
            _sceneOptionButtonsBound = true;
        }

        private void BindDirectionOptionButtons()
        {
            if (_directionOptionsPanel == null)
            {
                return;
            }

            Button[] buttons = _directionOptionsPanel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (i >= _directionOptions.Length)
                {
                    buttons[i].gameObject.SetActive(false);
                    continue;
                }

                int optionIndex = i;
                buttons[i].gameObject.SetActive(true);
                buttons[i].onClick.AddListener(() => SelectDirection(optionIndex));
            }

            _directionOptionsPanel.gameObject.SetActive(false);
        }

        private void BindLevelOptionButtons()
        {
            if (_levelOptionsPanel == null)
            {
                return;
            }

            Button[] buttons = _levelOptionsPanel.GetComponentsInChildren<Button>(true);
            int count = Mathf.Min(buttons.Length, GetMaxSelectableLevel());
            for (int i = 0; i < count; i++)
            {
                int optionLevel = i + 1;
                buttons[i].onClick.AddListener(() => SelectLevel(optionLevel));

                if (optionLevel == 1 && placeLevel1Button == null) placeLevel1Button = buttons[i];
                if (optionLevel == 2 && placeLevel2Button == null) placeLevel2Button = buttons[i];
                if (optionLevel == 3 && placeLevel3Button == null) placeLevel3Button = buttons[i];
            }

            _levelOptionsPanel.gameObject.SetActive(false);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 새로운 턴이 시작될 때 - 연산 전입 락을 위해 버튼을 비활성화하고 처리 상태 문자열을 띄웁니다.
        /// </summary>
        private void HandleTurnStarted(int turnNumber)
        {
            if (nextTurnButton != null)
            {
                nextTurnButton.interactable = false;
            }

            if (turnCountText != null)
            {
                turnCountText.text = processingLabelText;
            }
        }

        /// <summary>
        /// 턴 연산 처리가 끝났을 때 - 수동 버튼을 복귀시키고 신규 턴 숫자를 UI에 재출력합니다.
        /// </summary>
        private void HandleTurnEnded(int turnNumber)
        {
            TurnManager tm = TurnManager.Instance;
            int displayTurn = (tm != null) ? tm.CurrentTurn : turnNumber + 1;

            UpdateTurnDisplay(displayTurn);

            if (nextTurnButton != null)
            {
                bool isPlaying = !GameManager.HasInstance || GameManager.Instance.CurrentState == GameState.Playing;
                nextTurnButton.interactable = isPlaying;
            }
        }

        private void HandleResourceChanged()
        {
            UpdateResourceDisplay();
            UpdatePlacementLabel();
        }

        private void HandleGameStateChanged(GameState state)
        {
            UpdateResultDisplay(state);

            bool isPlaying = state == GameState.Playing;
            if (nextTurnButton != null)
            {
                nextTurnButton.interactable = isPlaying;
            }
            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(!isPlaying);
            }
        }

        private void HandleTileHovered(Vector2Int position)
        {
            if (tileInfoText == null || !GridManager.HasInstance)
            {
                return;
            }

            TileData tile = GridManager.Instance.GetTile(position);
            if (tile == null)
            {
                tileInfoText.text = string.Empty;
                tileInfoText.color = TextColor;
                return;
            }

            string emitterInfo = tile.Emitter != null
                ? $" / 진원 Lv{tile.Emitter.Level} {tile.Emitter.Direction} {(tile.Emitter.IsOn ? "ON" : "OFF")}"
                : string.Empty;
            string resourceInfo = tile.ResourceType != TileResourceType.None
                ? $" / {GameConfig.GetResourceLabel(tile.ResourceType)} (+{tile.ResourceYield})"
                : string.Empty;

            tileInfoText.text = $"{tile.Owner} Lv{tile.Level}{resourceInfo}{emitterInfo}";
            tileInfoText.color = GetTileInfoColor(tile);
        }

        private static Color GetTileInfoColor(TileData tile)
        {
            if (tile.IsOccupiedByEmitter)
            {
                return EmitterInfoColor;
            }

            if (tile.ResourceType != TileResourceType.None)
            {
                return ResourceInfoColor;
            }

            switch (tile.Owner)
            {
                case Owner.Player:
                    return PlayerInfoColor;
                case Owner.Enemy:
                    return EnemyInfoColor;
                default:
                    return NeutralInfoColor;
            }
        }

        #endregion

        #region Button Callbacks

        /// <summary>
        /// 수동 다음 턴 진행 버튼 클릭 이벤트 콜백.
        /// </summary>
        private void OnNextTurnClicked()
        {
            if (ResourceManager.HasInstance)
            {
                int load = ResourceManager.Instance.GetPlayerNextTurnPowerLoad();
                int capacity = ResourceManager.Instance.GetTotalPowerCapacity();
                if (load > capacity)
                {
                    ShowWarningMessage("전력이 부족합니다. 진원을 꺼 전력 소모를 줄이세요.");
                    return;
                }
            }

            TurnManager tm = TurnManager.Instance;
            if (tm != null)
            {
                tm.NextTurn();
            }
        }

        private void HandleEmitterToggled(Emitter emitter)
        {
            UpdateResourceDisplay();

            if (InputHandler.HasInstance)
            {
                HandleTileHovered(InputHandler.Instance.LastHoverPos);
            }
        }

        public void ShowWarningMessage(string message)
        {
            if (_warningCoroutine != null)
            {
                StopCoroutine(_warningCoroutine);
            }
            _warningCoroutine = StartCoroutine(FadeWarningCoroutine(message));
        }

        private IEnumerator FadeWarningCoroutine(string message)
        {
            if (warningText == null) yield break;

            warningText.gameObject.SetActive(true);
            warningText.enabled = true;
            warningText.text = message;

            float t = 0f;
            Color color = warningText.color;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                color.a = Mathf.Lerp(0f, 1f, t / 0.2f);
                warningText.color = color;
                yield return null;
            }
            color.a = 1f;
            warningText.color = color;

            yield return new WaitForSeconds(1.8f);

            t = 0f;
            while (t < 1.0f)
            {
                t += Time.deltaTime;
                color.a = Mathf.Lerp(1f, 0f, t / 1.0f);
                warningText.color = color;
                yield return null;
            }
            color.a = 0f;
            warningText.color = color;
            warningText.enabled = false;
            _warningCoroutine = null;
        }

        private void OnRestartClicked()
        {
            if (GameManager.HasInstance)
            {
                GameManager.Instance.RestartGame();
            }
        }

        private void ToggleDirectionOptions()
        {
            bool nextVisible = _directionOptionsPanel != null && !_directionOptionsPanel.gameObject.activeSelf;
            if (_directionOptionsPanel != null) _directionOptionsPanel.gameObject.SetActive(nextVisible);
            if (_levelOptionsPanel != null) _levelOptionsPanel.gameObject.SetActive(false);
        }

        private void ToggleLevelOptions()
        {
            bool nextVisible = _levelOptionsPanel != null && !_levelOptionsPanel.gameObject.activeSelf;
            if (_levelOptionsPanel != null) _levelOptionsPanel.gameObject.SetActive(nextVisible);
            if (_directionOptionsPanel != null) _directionOptionsPanel.gameObject.SetActive(false);
        }

        private void SelectDirection(int index)
        {
            _directionIndex = Mathf.Clamp(index, 0, _directionOptions.Length - 1);
            if (_directionOptionsPanel != null) _directionOptionsPanel.gameObject.SetActive(false);
            ApplyPlacementSelection();
            UpdatePlacementLabel();
        }

        private void SelectLevel(int level)
        {
            _selectedLevel = Mathf.Clamp(level, 1, GetMaxSelectableLevel());
            if (_levelOptionsPanel != null) _levelOptionsPanel.gameObject.SetActive(false);
            ApplyPlacementSelection();
            UpdatePlacementLabel();
        }

        private void ApplyPlacementSelection()
        {
            if (!InputHandler.HasInstance)
            {
                return;
            }

            InputHandler.Instance.SetPlacementParameters(
                Owner.Player,
                _directionOptions[_directionIndex],
                _selectedLevel);
        }

        private string GetLevelOptionLabel(int level)
        {
            EmitterDirection direction = _directionOptions[_directionIndex];
            return $"Lv{level} / {FormatCostText(direction, level, compact: true)}";
        }

        private static string GetDirectionOptionLabel(EmitterDirection direction)
        {
            return $"{GetDirectionLabel(direction)} / {FormatCostText(direction, 1, compact: true)}";
        }

        private static string FormatCostText(EmitterDirection direction, int level = 1, bool compact = false)
        {
            string costText = GetCostText(direction, level, compact);
            bool canAfford = !ResourceManager.HasInstance ||
                             ResourceManager.Instance.CanAffordEmitterPlacement(direction, level);
            string color = canAfford ? "#52FF7A" : "#FF5656";
            return $"<color={color}>{costText}</color>";
        }

        private static string GetCostText(EmitterDirection direction, int level, bool compact)
        {
            if (!ResourceManager.HasInstance)
            {
                return "비용 확인 중";
            }

            ResourceManager resourceManager = ResourceManager.Instance;
            TileResourceType resource = resourceManager.GetRequiredResource(direction);
            int cost = resourceManager.GetEmitterPlacementCost(direction, level);
            if (resource == TileResourceType.None || cost <= 0)
            {
                return "비용 없음";
            }

            string resourceLabel = GameConfig.GetResourceLabel(resource);
            return compact ? $"{resourceLabel}×{cost}" : $"{resourceLabel} x{cost}";
        }

        private static int GetMaxSelectableLevel()
        {
            if (GameManager.HasInstance && GameManager.Instance.Config != null)
            {
                return Mathf.Max(1, GameManager.Instance.Config.MaxLevel);
            }

            return 5;
        }

        private static string GetDirectionLabel(EmitterDirection direction)
        {
            switch (direction)
            {
                case EmitterDirection.Up:
                    return "위";
                case EmitterDirection.Down:
                    return "아래";
                case EmitterDirection.Left:
                    return "왼쪽";
                case EmitterDirection.Right:
                    return "오른쪽";
                case EmitterDirection.TShape:
                    return "T자";
                case EmitterDirection.Cross:
                    return "십자";
                default:
                    return direction.ToString();
            }
        }

        #endregion

        #region Display Helpers

        /// <summary>
        /// 인자로 들어온 정수 턴 숫자를 가독화하여 텍스트에 적용합니다.
        /// </summary>
        /// <param name="turn">현재의 턴 수.</param>
        private void UpdateTurnDisplay(int turn)
        {
            if (turnCountText != null)
            {
                turnCountText.text = turnPrefixText + turn;
            }
        }

        private void UpdateResourceDisplay()
        {
            if (resourceText == null)
            {
                return;
            }

            if (!ResourceManager.HasInstance)
            {
                resourceText.text = "전력: -";
                return;
            }

            ResourceManager rm = ResourceManager.Instance;
            int load = rm.GetPlayerNextTurnPowerLoad();
            int capacity = rm.GetTotalPowerCapacity();
            resourceText.text =
                $"전력 {load}/{capacity}  철 {rm.Iron}  구리 {rm.Copper}\n" +
                $"<size=75%> 소모/생산    +{rm.IronProductionPerTurn}/턴     +{rm.CopperProductionPerTurn}/턴</size>";
        }

        private void UpdateResultDisplay(GameState state)
        {
            bool showResult = state == GameState.Victory || state == GameState.Defeat;

            if (_runtimeResultPanel != null)
            {
                _runtimeResultPanel.gameObject.SetActive(showResult);
            }

            if (resultText != null)
            {
                switch (state)
                {
                    case GameState.Victory:
                        resultText.text = "승리";
                        break;
                    case GameState.Defeat:
                        resultText.text = "패배";
                        break;
                    default:
                        resultText.text = string.Empty;
                        break;
                }
            }

            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(showResult);
            }
        }

        private void UpdatePlacementLabel()
        {
            EmitterDirection direction = _directionOptions[_directionIndex];
            if (directionButtonText != null)
            {
                directionButtonText.text = GetDirectionLabel(direction);
            }

            if (levelButtonText != null)
            {
                levelButtonText.text = $"Lv{_selectedLevel}";
            }

            SetButtonLabel(placeLevel1Button, GetLevelOptionLabel(1));
            SetButtonLabel(placeLevel2Button, GetLevelOptionLabel(2));
            SetButtonLabel(placeLevel3Button, GetLevelOptionLabel(3));
            UpdateDirectionOptionLabels();
            UpdateLevelOptionLabels();

            if (InputHandler.HasInstance &&
                InputHandler.Instance.CurrentMode == InputMode.PlaceEmitter &&
                (InputHandler.Instance.SelectedDirection != direction ||
                 InputHandler.Instance.SelectedLevel != _selectedLevel))
            {
                ApplyPlacementSelection();
            }

            if (placementModeText != null)
            {
                string costText = ResourceManager.HasInstance
                    ? ResourceManager.Instance.GetEmitterPlacementCostText(direction, _selectedLevel)
                    : "비용 확인 중";
                bool canAfford = !ResourceManager.HasInstance ||
                                 ResourceManager.Instance.CanAffordEmitterPlacement(direction, _selectedLevel);

                placementModeText.text = $"배치: {GetDirectionLabel(direction)} Lv{_selectedLevel} / {costText}";
                placementModeText.color = canAfford ? TextColor : EnemyInfoColor;
            }
        }

        private static void SetButtonLabel(Button button, string text)
        {
            if (button == null)
            {
                return;
            }

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = text;
            }
        }

        private void UpdateDirectionOptionLabels()
        {
            if (_directionOptionsPanel == null)
            {
                return;
            }

            Button[] buttons = _directionOptionsPanel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (i >= _directionOptions.Length)
                {
                    buttons[i].gameObject.SetActive(false);
                    continue;
                }

                buttons[i].gameObject.SetActive(true);
                SetButtonLabel(buttons[i], GetDirectionOptionLabel(_directionOptions[i]));
            }
        }

        private void UpdateLevelOptionLabels()
        {
            if (_levelOptionsPanel == null)
            {
                return;
            }

            Button[] buttons = _levelOptionsPanel.GetComponentsInChildren<Button>(true);
            int count = Mathf.Min(buttons.Length, GetMaxSelectableLevel());
            for (int i = 0; i < count; i++)
            {
                SetButtonLabel(buttons[i], GetLevelOptionLabel(i + 1));
            }
        }

        #endregion
    }
}
