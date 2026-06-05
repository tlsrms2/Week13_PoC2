// ============================================================================
// 파일:    Input/InputHandler.cs
// 프로젝트: Project SEVERANCE
// 용도: 마우스 포인터 입력을 타일 선택, 이미터 배치, 토글 연산 등
//          실질적 인게임 액션 명령으로 변환 전파하는 싱글톤 입력 시스템.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Severance
{
    /// <summary>
    /// 타일 클릭 행동을 해석 및 가공하기 위한 현재의 입력 모드 유형입니다.
    /// </summary>
    public enum InputMode
    {
        /// <summary>타일을 클릭했을 때 정보 확인 혹은 해당 타일 위 이미터를 토글 전원 조작합니다.</summary>
        Select = 0,
        /// <summary>타일을 클릭했을 때 지정 사양의 새로운 이미터 설치 배치를 가동합니다.</summary>
        PlaceEmitter = 1
    }

    /// <summary>
    /// 실시간 마우스 클릭을 가로채고 화면 투사 레이캐스트를 연산하여
    /// 영토 정보 선택 명령 또는 건설 배치를 분기 전파하는 싱글톤 입력 컴포넌트입니다.
    /// </summary>
    public class InputHandler : Singleton<InputHandler>
    {
        #region Static Events

        /// <summary>
        /// 입력 처리 모드와 무관하게, 마우스 유효 타일 클릭 검출 시 무조건 발생합니다.
        /// 매개변수: 클릭된 대상 타일의 그리드 좌표.
        /// </summary>
        public static event Action<Vector2Int> OnTileClicked;

        public static event Action<Vector2Int> OnTileHovered;

        #endregion

        #region Inspector Fields

        [Header("입력 카메라 설정")]
        [Tooltip("마우스 투사 변환 연산에 사용할 씬 카메라. 미할당 시 Camera.main을 기본 대입합니다.")]
        [SerializeField] private Camera inputCamera;

        private Vector2Int? _lastPreviewPos;
        private Vector2Int _lastHoverPos = new Vector2Int(int.MinValue, int.MinValue);
        private readonly List<Vector2Int> _hoverOutlinePositions = new List<Vector2Int>();

        #endregion

        #region Public Properties

        /// <summary>
        /// 현재 마우스 포인터가 가리키고 있는 마지막 그리드 좌표입니다.
        /// </summary>
        public Vector2Int LastHoverPos => _lastHoverPos;

        /// <summary>
        /// 현재 활성 입력 모드. <see cref="SetMode"/> 또는
        /// <see cref="SetPlacementParameters"/>를 통해 제어합니다.
        /// </summary>
        public InputMode CurrentMode { get; private set; } = InputMode.Select;

        /// <summary>
        /// 신규 이미터 배치 시 기본 설계로 적용할 이미터 확장 전개 방향 규격.
        /// <see cref="CurrentMode"/>가 <see cref="InputMode.PlaceEmitter"/> 상태일 때 유효하게 사용됩니다.
        /// </summary>
        public EmitterDirection SelectedDirection { get; private set; } =
            EmitterDirection.Cross;

        /// <summary>
        /// 신규 이미터 배치 시 지정해 넣을 이미터 레벨 등급.
        /// <see cref="CurrentMode"/>가 <see cref="InputMode.PlaceEmitter"/> 상태일 때 유효하게 사용됩니다.
        /// </summary>
        public int SelectedLevel { get; private set; } = 1;

        /// <summary>
        /// 신규 이미터 배치 시 지정해 넣을 이미터 소유 진영 (Player 또는 Enemy).
        /// </summary>
        public Owner SelectedOwner { get; private set; } = Owner.Player;

        #endregion

        #region Public API

        /// <summary>
        /// 마우스 입력 모드를 강제 전환합니다.
        /// </summary>
        /// <param name="mode">전환 대상 모드.</param>
        public void SetMode(InputMode mode)
        {
            CurrentMode = mode;
            Debug.Log($"[InputHandler] 입력 모드 변경 → {mode}");
        }

        /// <summary>
        /// 설치용 매개변수를 사전 설정하고 모드를 즉시 <see cref="InputMode.PlaceEmitter"/>(배치 모드)로 강제 변환합니다.
        /// </summary>
        /// <param name="direction">이미터 확장 방향 형태.</param>
        /// <param name="level">설치 이미터 강도 레벨 등급 (최소 1 이상).</param>
        public void SetPlacementParameters(EmitterDirection direction, int level)
        {
            SetPlacementParameters(Owner.Player, direction, level);
        }

        /// <summary>
        /// 설치 세력까지 지정하여 매개변수를 사전 설정하고 모드를 즉시 <see cref="InputMode.PlaceEmitter"/>(배치 모드)로 강제 변환합니다.
        /// </summary>
        /// <param name="owner">설치할 이미터 소유 세력.</param>
        /// <param name="direction">이미터 확장 방향 형태.</param>
        /// <param name="level">설치 이미터 강도 레벨 등급 (최소 1 이상).</param>
        public void SetPlacementParameters(Owner owner, EmitterDirection direction, int level)
        {
            SelectedOwner = owner;
            SelectedDirection = direction;
            SelectedLevel = Mathf.Max(1, level);
            CurrentMode = InputMode.PlaceEmitter;
            Debug.Log(
                $"[InputHandler] 이미터 설치 모드 사전 셋업: {owner} {direction} Lv{SelectedLevel}");
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            UpdateHoverAndPlacementPreview();

            // [디버그용 단축키]
            // S 키를 누르면 다시 일반 타일 선택/토글 모드로 변경 (디버깅용 편의 키)
            if (Input.GetKeyDown(KeyCode.S))
            {
                SetMode(InputMode.Select);
            }

            // Only process on left mouse button down
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            // Don't process clicks during turn processing
            TurnManager tm = TurnManager.Instance;
            if (tm != null && tm.IsProcessing)
            {
                return;
            }

            // Check if click is over UI — skip if so
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            ProcessClick();
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 화면 클릭 스크린 픽셀 좌표를 월드 벡터로 투사하여 그리드 타일 단위 정수형 좌표로 환산 분석합니다.
        /// </summary>
        private void ProcessClick()
        {
            if (!TryGetMouseGridPosition(out Vector2Int gridPos))
            {
                return;
            }

            // 전체 타일 클릭 고유 정적 이벤트 발생
            OnTileClicked?.Invoke(gridPos);

            // 모드 분기에 따라 조작 함수 호출
            switch (CurrentMode)
            {
                case InputMode.Select:
                    HandleSelectMode(gridPos);
                    break;

                case InputMode.PlaceEmitter:
                    HandlePlaceEmitterMode(gridPos);
                    break;
            }
        }

        /// <summary>
        /// Select(선택 모드)인 경우: 타일에 이미터가 있다면 전원 토글을 수행하고, 없으면 디버깅 정보를 로그 출력합니다.
        /// </summary>
        private void HandleSelectMode(Vector2Int gridPos)
        {
            EmitterManager emitterMgr = EmitterManager.Instance;
            if (emitterMgr == null)
            {
                return;
            }

            Emitter emitter = emitterMgr.GetEmitterAt(gridPos);
            if (emitter != null)
            {
                // 이미터 전원 활성 ON / OFF 제어 실행
                emitterMgr.ToggleEmitter(emitter);
                Debug.Log(
                    $"[InputHandler] {gridPos} 좌표 이미터 조작 가동 → " +
                    $"{(emitter.IsOn ? "ON" : "OFF")}");
            }
            else
            {
                TileData tile = GridManager.Instance?.GetTile(gridPos);
                if (tile != null)
                {
                    Debug.Log(
                        $"[InputHandler] 타일 선택 {gridPos}: " +
                        $"소유자={tile.Owner}, 레벨={tile.Level}, " +
                        $"배치 자원={tile.ResourceType}");
                }
            }
        }

        /// <summary>
        /// PlaceEmitter(설치 모드)인 경우: 설정된 세력 소유의 이미터 배치를 규격 수식에 맞춰 연산하며, 성공 시 Select 모드로 원복합니다.
        /// </summary>
        private void HandlePlaceEmitterMode(Vector2Int gridPos)
        {
            EmitterManager emitterMgr = EmitterManager.Instance;
            if (emitterMgr == null)
            {
                return;
            }

            bool placed = emitterMgr.TryPlaceEmitter(
                gridPos,
                SelectedOwner,
                SelectedLevel,
                SelectedDirection);

            if (placed)
            {
                Debug.Log(
                    $"[InputHandler] {SelectedOwner} {SelectedDirection} 템플릿 레벨 Lv{SelectedLevel} 이미터 배치 성공 @{gridPos}");

                // 배치 후 일반 탐색(선택 모드)으로 원복 처리
                SetMode(InputMode.Select);
            }
            else
            {
                Debug.LogWarning(
                    $"[InputHandler] {gridPos} 타일 조건 불충족으로 이미터 설치에 실패했습니다.");
            }
        }

        private void UpdateHoverAndPlacementPreview()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                ClearPlacementPreview();
                if (_lastHoverPos != new Vector2Int(int.MinValue, int.MinValue))
                {
                    ClearHoveredEmitterAreaOutline();
                    _lastHoverPos = new Vector2Int(int.MinValue, int.MinValue);
                    OnTileHovered?.Invoke(_lastHoverPos);
                }
                return;
            }

            if (!TryGetMouseGridPosition(out Vector2Int gridPos))
            {
                ClearPlacementPreview();
                if (_lastHoverPos != new Vector2Int(int.MinValue, int.MinValue))
                {
                    ClearHoveredEmitterAreaOutline();
                    _lastHoverPos = new Vector2Int(int.MinValue, int.MinValue);
                    OnTileHovered?.Invoke(_lastHoverPos);
                }
                return;
            }

            if (gridPos != _lastHoverPos)
            {
                UpdateHoveredEmitterAreaOutline(gridPos);
                _lastHoverPos = gridPos;
                OnTileHovered?.Invoke(gridPos);
            }

            if (CurrentMode != InputMode.PlaceEmitter)
            {
                ClearPlacementPreview();
                return;
            }

            GridVisualizer visualizer = GameManager.HasInstance ? GameManager.Instance.Visualizer : null;
            EmitterManager emitterMgr = EmitterManager.HasInstance ? EmitterManager.Instance : null;
            if (visualizer == null || emitterMgr == null)
            {
                ClearPlacementPreview();
                return;
            }

            if (_lastPreviewPos.HasValue && _lastPreviewPos.Value != gridPos)
            {
                TileView previous = visualizer.GetView(_lastPreviewPos.Value);
                if (previous != null)
                {
                    previous.SetPlacementPreview(false, false, SelectedDirection);
                }
            }

            bool canPlace = emitterMgr.CanPlaceEmitter(
                gridPos,
                SelectedOwner,
                SelectedLevel,
                SelectedDirection);

            TileView current = visualizer.GetView(gridPos);
            if (current != null)
            {
                current.SetPlacementPreview(true, canPlace, SelectedDirection);
                _lastPreviewPos = gridPos;
            }
        }

        private void ClearPlacementPreview()
        {
            if (!_lastPreviewPos.HasValue || !GameManager.HasInstance || GameManager.Instance.Visualizer == null)
            {
                _lastPreviewPos = null;
                return;
            }

            TileView previous = GameManager.Instance.Visualizer.GetView(_lastPreviewPos.Value);
            if (previous != null)
            {
                previous.SetPlacementPreview(false, false, SelectedDirection);
            }

            _lastPreviewPos = null;
        }

        private void UpdateHoveredEmitterAreaOutline(Vector2Int gridPos)
        {
            ClearHoveredEmitterAreaOutline();

            if (!GridManager.HasInstance ||
                !GameManager.HasInstance ||
                GameManager.Instance.Visualizer == null)
            {
                return;
            }

            GridManager grid = GridManager.Instance;
            TileData hoverTile = grid.GetTile(gridPos);
            Emitter emitter = hoverTile != null ? hoverTile.Emitter : null;
            if (emitter == null)
            {
                return;
            }

            GridVisualizer visualizer = GameManager.Instance.Visualizer;
            if (TryShowHoveredEmitterSupportAreas(visualizer, grid, hoverTile, emitter))
            {
                return;
            }

            HashSet<Vector2Int> areaTiles = new HashSet<Vector2Int>();
            AddEmitterAreaTile(areaTiles, grid, emitter, emitter.Position);

            foreach (Vector2Int ownedPos in emitter.OwnedTiles)
            {
                AddEmitterAreaTile(areaTiles, grid, emitter, ownedPos);
            }

            ShowAreaOutline(visualizer, areaTiles);
        }

        private bool TryShowHoveredEmitterSupportAreas(
            GridVisualizer visualizer,
            GridManager grid,
            TileData hoverTile,
            Emitter emitter)
        {
            if (visualizer == null ||
                grid == null ||
                hoverTile == null ||
                emitter == null ||
                !EmitterManager.HasStableBaseSupport(hoverTile, emitter))
            {
                return false;
            }

            List<Emitter> supporters = EmitterManager.GetCriticalBaseSupportEmitters(hoverTile, emitter);
            if (supporters.Count == 0)
            {
                return false;
            }

            HashSet<Vector2Int> supportAreaTiles = new HashSet<Vector2Int>();
            foreach (Emitter supporter in supporters)
            {
                if (supporter == null)
                {
                    continue;
                }

                AddEmitterAreaTile(supportAreaTiles, grid, supporter, supporter.Position);
                foreach (Vector2Int ownedPos in supporter.OwnedTiles)
                {
                    AddEmitterAreaTile(supportAreaTiles, grid, supporter, ownedPos);
                }
            }

            ShowAreaOutline(visualizer, supportAreaTiles);
            return supportAreaTiles.Count > 0;
        }

        private void ShowAreaOutline(GridVisualizer visualizer, HashSet<Vector2Int> areaTiles)
        {
            foreach (Vector2Int pos in areaTiles)
            {
                TileView view = visualizer.GetView(pos);
                if (view == null)
                {
                    continue;
                }

                bool up = !areaTiles.Contains(pos + Vector2Int.up);
                bool right = !areaTiles.Contains(pos + Vector2Int.right);
                bool down = !areaTiles.Contains(pos + Vector2Int.down);
                bool left = !areaTiles.Contains(pos + Vector2Int.left);

                view.SetEmitterAreaOutline(up, right, down, left);
                _hoverOutlinePositions.Add(pos);
            }
        }

        private static void AddEmitterAreaTile(HashSet<Vector2Int> areaTiles, GridManager grid, Emitter emitter, Vector2Int pos)
        {
            TileData tile = grid.GetTile(pos);
            if (tile == null || tile.Owner != emitter.Owner)
            {
                return;
            }

            if (pos == emitter.Position ||
                tile.Emitter == emitter ||
                tile.ParentEmitters.Contains(emitter))
            {
                areaTiles.Add(pos);
            }
        }

        private void ClearHoveredEmitterAreaOutline()
        {
            if (_hoverOutlinePositions.Count == 0 ||
                !GameManager.HasInstance ||
                GameManager.Instance.Visualizer == null)
            {
                _hoverOutlinePositions.Clear();
                return;
            }

            GridVisualizer visualizer = GameManager.Instance.Visualizer;
            foreach (Vector2Int pos in _hoverOutlinePositions)
            {
                TileView view = visualizer.GetView(pos);
                if (view != null)
                {
                    view.ClearEmitterAreaOutline();
                }
            }

            _hoverOutlinePositions.Clear();
        }

        private bool TryGetMouseGridPosition(out Vector2Int gridPos)
        {
            gridPos = Vector2Int.zero;

            Camera cam = (inputCamera != null) ? inputCamera : Camera.main;
            if (cam == null || !GridManager.HasInstance || !GameManager.HasInstance)
            {
                return false;
            }

            GridManager grid = GridManager.Instance;
            GameConfig config = GameManager.Instance.Config;
            if (grid == null || config == null)
            {
                return false;
            }

            Vector3 worldPos = cam.ScreenToWorldPoint(Input.mousePosition);
            float pitch = config.TileSize + config.TileSpacing;
            float offsetX = (grid.Width - 1) * pitch * 0.5f;
            float offsetY = (grid.Height - 1) * pitch * 0.5f;

            gridPos = new Vector2Int(
                Mathf.RoundToInt((worldPos.x + offsetX) / pitch),
                Mathf.RoundToInt((worldPos.y + offsetY) / pitch));

            return grid.IsInBounds(gridPos);
        }

        #endregion
    }
}
