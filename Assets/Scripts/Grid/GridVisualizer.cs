using System.Collections.Generic;
using UnityEngine;

namespace Severance
{
    /// <summary>
    /// 논리 2D 그리드 레이아웃 정보를 기반으로 씬에 실제 타일 오브젝트들을 배치하고 스폰하는 시각화 관리자입니다.
    /// 생성되는 각 셀 단위 타일 프리팹은 <see cref="TileView"/> 컴포넌트를 지니며 데이터 변경을 실시간 동기화합니다.
    /// </summary>
    public class GridVisualizer : MonoBehaviour
    {
        #region Serialized Fields

        [Header("타일 프리팹")]
        [Tooltip("TileView, SpriteRenderer, BoxCollider2D 컴포넌트가 모두 포함된 프리팹 에셋.")]
        [SerializeField] private GameObject tilePrefab;

        #endregion

        #region Runtime State

        /// <summary>씬에 인스턴스화된 TileView 인스턴스들의 [x, y] 배열 공간.</summary>
        private TileView[,] _views;

        /// <summary>하이어라키 정리 정돈을 위한 하위 타일 오브젝트들의 부모 트랜스폼.</summary>
        private Transform _gridParent;

        /// <summary>스폰 시 캐싱된 크기 규격.</summary>
        private int _width;
        private int _height;

        private struct ExpansionBoundaryKey
        {
            public readonly Vector2Int Position;
            public readonly Vector2Int Direction;

            public ExpansionBoundaryKey(Vector2Int position, Vector2Int direction)
            {
                Position = position;
                Direction = direction;
            }

            public override bool Equals(object obj)
            {
                return obj is ExpansionBoundaryKey other &&
                       Position == other.Position &&
                       Direction == other.Direction;
            }

            public override int GetHashCode()
            {
                return (Position.GetHashCode() * 397) ^ Direction.GetHashCode();
            }
        }

        private struct ExpansionBoundaryPower
        {
            public int PlayerLevel;
            public int EnemyLevel;

            public void Add(Owner owner, int level)
            {
                int value = Mathf.Max(0, level);
                if (owner == Owner.Player)
                {
                    PlayerLevel = Mathf.Clamp(PlayerLevel + value, 0, 5);
                }
                else if (owner == Owner.Enemy)
                {
                    EnemyLevel = Mathf.Clamp(EnemyLevel + value, 0, 5);
                }
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// 씬에 비주얼 그리드를 실질적으로 배치 및 스폰합니다. 호출 전 이미 배치된 타일은 모두 파괴합니다.
        /// </summary>
        /// <param name="width">생성할 열 가로 크기.</param>
        /// <param name="height">생성할 행 세로 크기.</param>
        public void SpawnGrid(int width, int height)
        {
            if (tilePrefab == null)
            {
                Debug.LogError("[GridVisualizer] 타일 프리팹(tilePrefab)이 할당되지 않았습니다!");
                return;
            }

            // 기존 그리드 잔해 청소
            DestroyGrid();

            _width = width;
            _height = height;
            _views = new TileView[width, height];

            // 계층 구조 관리를 위한 그리드 타일 전용 컨테이너 생성
            var parentObj = new GameObject("GridTiles");
            parentObj.transform.SetParent(transform, false);
            _gridParent = parentObj.transform;

            // 설정 데이터로부터 타일 규격 및 간격 조회
            GameConfig config = GameManager.Instance.Config;
            float size = (config != null) ? config.TileSize : 1.0f;
            float spacing = (config != null) ? config.TileSpacing : 0.0f;
            float pitch = size + spacing;

            // 월드 기준점 (0,0)에 그리드 전체가 완벽한 중심으로 정렬되도록 오프셋 좌표 계산
            float offsetX = (width - 1) * pitch * 0.5f;
            float offsetY = (height - 1) * pitch * 0.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 worldPos = new Vector3(
                        x * pitch - offsetX,
                        y * pitch - offsetY,
                        0f
                    );

                    GameObject tileObj = Instantiate(tilePrefab, worldPos, Quaternion.identity, _gridParent);
                    tileObj.name = $"Tile_{x}_{y}";

                    // 설정된 규격 크기에 맞춰 프리팹의 가로세로 스케일 적용
                    tileObj.transform.localScale = new Vector3(size, size, 1f);

                    TileView view = tileObj.GetComponent<TileView>();
                    if (view == null)
                    {
                        Debug.LogError($"[GridVisualizer] 생성된 타일 프리팹에 TileView 컴포넌트가 존재하지 않습니다! ({x},{y})");
                        continue;
                    }

                    view.Initialize(new Vector2Int(x, y));
                    _views[x, y] = view;
                }
            }

            // 생성 직후 모든 타일의 비주얼을 데이터와 동기화
            RefreshAll();

            Debug.Log($"[GridVisualizer] {width}x{height} 규모의 비주얼 그리드 스폰 완료.");
        }

        /// <summary>
        /// 특정 2D 그리드 좌표 상의 시각적 뷰 컴포넌트를 반환하며, 경계를 벗어날 경우 null을 반환합니다.
        /// </summary>
        /// <param name="pos">그리드 좌표.</param>
        /// <returns>타일의 TileView 컴포넌트 또는 null.</returns>
        public TileView GetView(Vector2Int pos)
        {
            if (_views == null) return null;
            if (pos.x < 0 || pos.x >= _width || pos.y < 0 || pos.y >= _height) return null;

            return _views[pos.x, pos.y];
        }

        /// <summary>
        /// 그리드 내부의 모든 타일 뷰 컴포넌트에 강제 갱신 명령을 내려 최신 데이터 비주얼을 그리도록 합니다.
        /// </summary>
        public void RefreshAll()
        {
            if (_views == null) return;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (_views[x, y] != null)
                    {
                        _views[x, y].RefreshVisuals();
                    }
                }
            }

            RefreshExpansionIntentPreviews();
        }

        public void RefreshExpansionIntentPreviews()
        {
            if (_views == null)
            {
                return;
            }

            ClearExpansionIntentPreviews();

            if (!GridManager.HasInstance || !EmitterManager.HasInstance)
            {
                return;
            }

            GridManager grid = GridManager.Instance;
            if (grid == null || !grid.IsInitialized)
            {
                return;
            }

            EmitterManager emitterManager = EmitterManager.Instance;
            Dictionary<ExpansionBoundaryKey, ExpansionBoundaryPower> boundaryPowers =
                new Dictionary<ExpansionBoundaryKey, ExpansionBoundaryPower>();
            Dictionary<ExpansionBoundaryKey, HashSet<Emitter>> boundaryContributors =
                new Dictionary<ExpansionBoundaryKey, HashSet<Emitter>>();

            AddExpansionIntentPreviews(grid, emitterManager.GetEmitters(Owner.Player), boundaryPowers, boundaryContributors);
            AddExpansionIntentPreviews(grid, emitterManager.GetEmitters(Owner.Enemy), boundaryPowers, boundaryContributors);
            ApplyExpansionIntentPreviews(boundaryPowers);
        }

        private void ClearExpansionIntentPreviews()
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (_views[x, y] != null)
                    {
                        _views[x, y].ClearExpansionIntentPreview();
                    }
                }
            }
        }

        private void AddExpansionIntentPreviews(
            GridManager grid,
            List<Emitter> emitters,
            Dictionary<ExpansionBoundaryKey, ExpansionBoundaryPower> boundaryPowers,
            Dictionary<ExpansionBoundaryKey, HashSet<Emitter>> boundaryContributors)
        {
            foreach (Emitter emitter in emitters)
            {
                if (emitter == null || !emitter.IsOn)
                {
                    continue;
                }

                if (emitter.Direction == EmitterDirection.EightWay)
                {
                    AddEightWayExpansionIntentPreviews(grid, emitter, boundaryPowers, boundaryContributors);
                }
                else
                {
                    AddLinearExpansionIntentPreviews(grid, emitter, boundaryPowers, boundaryContributors);
                }
            }
        }

        private void AddLinearExpansionIntentPreviews(
            GridManager grid,
            Emitter emitter,
            Dictionary<ExpansionBoundaryKey, ExpansionBoundaryPower> boundaryPowers,
            Dictionary<ExpansionBoundaryKey, HashSet<Emitter>> boundaryContributors)
        {
            foreach (Vector2Int direction in emitter.GetExpansionDirections())
            {
                int connectedDistance = GetConnectedDistance(grid, emitter, direction);
                if (IsAtMaxRange(emitter, connectedDistance))
                {
                    continue;
                }

                Vector2Int sourcePos = emitter.Position + direction * connectedDistance;
                TileData sourceTile = grid.GetTile(sourcePos);
                if (!CanEmitterExpandFrom(emitter, sourceTile))
                {
                    continue;
                }

                Vector2Int targetPos = sourcePos + direction;
                if (!grid.IsInBounds(targetPos))
                {
                    continue;
                }

                TileData targetTile = grid.GetTile(targetPos);
                if (!CanEmitterTargetNextTile(emitter, targetTile))
                {
                    continue;
                }

                AddExpansionBoundaryPower(boundaryPowers, boundaryContributors, sourcePos, direction, emitter);
            }
        }

        private void AddEightWayExpansionIntentPreviews(
            GridManager grid,
            Emitter emitter,
            Dictionary<ExpansionBoundaryKey, ExpansionBoundaryPower> boundaryPowers,
            Dictionary<ExpansionBoundaryKey, HashSet<Emitter>> boundaryContributors)
        {
            int minConnected = int.MaxValue;
            foreach (Vector2Int direction in emitter.GetExpansionDirections())
            {
                minConnected = Mathf.Min(minConnected, GetConnectedDistance(grid, emitter, direction));
            }

            if (minConnected == int.MaxValue || IsAtMaxRange(emitter, minConnected))
            {
                return;
            }

            int nextDistance = minConnected + 1;
            for (int dx = -nextDistance; dx <= nextDistance; dx++)
            {
                for (int dy = -nextDistance; dy <= nextDistance; dy++)
                {
                    if (Mathf.Abs(dx) != nextDistance && Mathf.Abs(dy) != nextDistance)
                    {
                        continue;
                    }

                    Vector2Int targetPos = emitter.Position + new Vector2Int(dx, dy);
                    if (!grid.IsInBounds(targetPos))
                    {
                        continue;
                    }

                    if (!TryGetEightWayApproach(
                            grid,
                            emitter,
                            targetPos,
                            nextDistance,
                            out Vector2Int sourcePos,
                            out Vector2Int approach))
                    {
                        continue;
                    }

                    AddExpansionBoundaryPower(boundaryPowers, boundaryContributors, sourcePos, approach, emitter);
                }
            }
        }

        private void ApplyExpansionIntentPreviews(Dictionary<ExpansionBoundaryKey, ExpansionBoundaryPower> boundaryPowers)
        {
            foreach (KeyValuePair<ExpansionBoundaryKey, ExpansionBoundaryPower> pair in boundaryPowers)
            {
                TileView view = GetView(pair.Key.Position);
                if (view == null)
                {
                    continue;
                }

                view.SetExpansionIntent(
                    pair.Key.Direction,
                    pair.Value.PlayerLevel,
                    pair.Value.EnemyLevel);
            }
        }

        private static void AddExpansionBoundaryPower(
            Dictionary<ExpansionBoundaryKey, ExpansionBoundaryPower> boundaryPowers,
            Dictionary<ExpansionBoundaryKey, HashSet<Emitter>> boundaryContributors,
            Vector2Int sourcePos,
            Vector2Int direction,
            Emitter emitter)
        {
            if (emitter == null)
            {
                return;
            }

            if (!TryNormalizeExpansionBoundary(sourcePos, direction, out ExpansionBoundaryKey key))
            {
                return;
            }

            if (!boundaryContributors.TryGetValue(key, out HashSet<Emitter> contributors))
            {
                contributors = new HashSet<Emitter>();
                boundaryContributors[key] = contributors;
            }

            if (contributors.Add(emitter))
            {
                boundaryPowers.TryGetValue(key, out ExpansionBoundaryPower power);
                power.Add(emitter.Owner, emitter.Level);
                boundaryPowers[key] = power;
            }
        }

        private static bool TryNormalizeExpansionBoundary(
            Vector2Int sourcePos,
            Vector2Int direction,
            out ExpansionBoundaryKey key)
        {
            if (direction == Vector2Int.up || direction == Vector2Int.right)
            {
                key = new ExpansionBoundaryKey(sourcePos, direction);
                return true;
            }

            if (direction == Vector2Int.down)
            {
                key = new ExpansionBoundaryKey(sourcePos + direction, Vector2Int.up);
                return true;
            }

            if (direction == Vector2Int.left)
            {
                key = new ExpansionBoundaryKey(sourcePos + direction, Vector2Int.right);
                return true;
            }

            key = new ExpansionBoundaryKey(Vector2Int.zero, Vector2Int.zero);
            return false;
        }

        private static bool TryGetEightWayApproach(
            GridManager grid,
            Emitter emitter,
            Vector2Int targetPos,
            int nextDistance,
            out Vector2Int sourcePos,
            out Vector2Int approach)
        {
            Vector2Int[] approaches =
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

            foreach (Vector2Int candidate in approaches)
            {
                Vector2Int candidateSourcePos = targetPos - candidate;
                if (IsInnerEightWaySource(emitter, candidateSourcePos, nextDistance) &&
                    CanEmitterExpandFrom(emitter, grid.GetTile(candidateSourcePos)) &&
                    CanEmitterTargetNextTile(emitter, grid.GetTile(targetPos)))
                {
                    sourcePos = candidateSourcePos;
                    approach = candidate;
                    return true;
                }
            }

            Vector2Int delta = targetPos - emitter.Position;
            Vector2Int diagonalSource = targetPos - new Vector2Int(
                Mathf.Clamp(delta.x, -1, 1),
                Mathf.Clamp(delta.y, -1, 1));

            if (IsInnerEightWaySource(emitter, diagonalSource, nextDistance) &&
                CanEmitterExpandFrom(emitter, grid.GetTile(diagonalSource)) &&
                CanEmitterTargetNextTile(emitter, grid.GetTile(targetPos)))
            {
                sourcePos = diagonalSource;
                approach = GetDominantApproach(delta);
                return true;
            }

            sourcePos = Vector2Int.zero;
            approach = Vector2Int.zero;
            return false;
        }

        private static bool IsInnerEightWaySource(Emitter emitter, Vector2Int sourcePos, int nextDistance)
        {
            int distance = Mathf.Max(
                Mathf.Abs(sourcePos.x - emitter.Position.x),
                Mathf.Abs(sourcePos.y - emitter.Position.y));
            return distance < nextDistance;
        }

        private static Vector2Int GetDominantApproach(Vector2Int delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return new Vector2Int(Mathf.Clamp(delta.x, -1, 1), 0);
            }

            return new Vector2Int(0, Mathf.Clamp(delta.y, -1, 1));
        }

        private static bool IsAtMaxRange(Emitter emitter, int connectedDistance)
        {
            if (!GameManager.HasInstance || GameManager.Instance.Config == null)
            {
                return false;
            }

            var setting = GameManager.Instance.Config.GetEmitterSetting(emitter.Direction);
            int maxRange = setting != null ? setting.maxRange : 0;
            return maxRange > 0 && connectedDistance >= maxRange;
        }

        private static int GetConnectedDistance(GridManager grid, Emitter emitter, Vector2Int direction)
        {
            int connectedDistance = 0;
            while (true)
            {
                Vector2Int pos = emitter.Position + direction * (connectedDistance + 1);
                if (!grid.IsInBounds(pos))
                {
                    break;
                }

                TileData tile = grid.GetTile(pos);
                if (tile == null ||
                    tile.Owner != emitter.Owner ||
                    !tile.ParentEmitters.Contains(emitter))
                {
                    break;
                }

                connectedDistance++;
            }

            return connectedDistance;
        }

        private static bool CanEmitterExpandFrom(Emitter emitter, TileData sourceTile)
        {
            if (sourceTile == null || sourceTile.Owner != emitter.Owner)
            {
                return false;
            }

            if (sourceTile.Position == emitter.Position)
            {
                return sourceTile.Emitter == emitter || sourceTile.ParentEmitters.Contains(emitter);
            }

            return sourceTile.ParentEmitters.Contains(emitter);
        }

        private static bool CanEmitterTargetNextTile(Emitter emitter, TileData targetTile)
        {
            if (targetTile == null)
            {
                return false;
            }

            return targetTile.Owner != emitter.Owner ||
                   !targetTile.ParentEmitters.Contains(emitter);
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// 씬에 생성된 모든 하위 타일 오브젝트들을 일괄 파괴하고 뷰 할당 배열을 비웁니다.
        /// </summary>
        private void DestroyGrid()
        {
            if (_gridParent != null)
            {
                Destroy(_gridParent.gameObject);
                _gridParent = null;
            }

            _views = null;
            _width = 0;
            _height = 0;
        }

        /// <summary>
        /// 오브젝트가 파괴될 때 씬 잔여물이 남지 않도록 말끔히 정리합니다.
        /// </summary>
        private void OnDestroy()
        {
            DestroyGrid();
        }

        #endregion
    }
}
