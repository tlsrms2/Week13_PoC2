// ============================================================================
// 파일:    Core/FogOfWarManager.cs
// 프로젝트: Project SEVERANCE
// 용도: 플레이어 영토 기준 전장의 안개 가시성 갱신.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Severance
{
    /// <summary>
    /// 플레이어가 소유한 타일 주변만 보이도록 타일 뷰의 안개 오버레이를 제어합니다.
    /// </summary>
    public class FogOfWarManager : Singleton<FogOfWarManager>
    {
        private GameConfig _config;

        public void Initialize(GameConfig config)
        {
            _config = config;
        }

        public void Refresh()
        {
            if (!GridManager.HasInstance || !GameManager.HasInstance)
            {
                return;
            }

            GridManager grid = GridManager.Instance;
            GridVisualizer visualizer = GameManager.Instance.Visualizer;
            if (grid == null || !grid.IsInitialized || visualizer == null)
            {
                return;
            }

            foreach (TileData tile in grid.AllTiles)
            {
                TileView view = visualizer.GetView(tile.Position);
                if (view != null)
                {
                    view.SetFogVisible(false);
                }
            }

            int radius = _config != null ? Mathf.Max(0, _config.FogOfWarRadius) : 2;
            HashSet<Vector2Int> visibleTiles = new HashSet<Vector2Int>();

            foreach (TileData tile in grid.AllTiles)
            {
                if (tile.Owner != Owner.Player || tile.Level <= 0)
                {
                    continue;
                }

                foreach (TileData visible in grid.GetTilesInRadius(tile.Position, radius))
                {
                    visibleTiles.Add(visible.Position);
                }
            }

            foreach (Vector2Int pos in visibleTiles)
            {
                TileView view = visualizer.GetView(pos);
                if (view != null)
                {
                    view.SetFogVisible(true);
                }
            }

            visualizer.RefreshExpansionIntentPreviews();
            GameEvents.RaiseFogUpdated();
        }

        /// <summary>
        /// 특정 좌표가 플레이어의 시야(안개가 걷힌 지역) 내에 속하는지 판별합니다.
        /// </summary>
        public bool IsTileVisible(Vector2Int pos)
        {
            if (!GridManager.HasInstance || _config == null)
            {
                return false;
            }

            GridManager grid = GridManager.Instance;
            int radius = Mathf.Max(0, _config.FogOfWarRadius);

            // 해당 타일 기준 시야 반경 내에 플레이어 영토가 존재한다면 밝혀진 구역으로 판정
            foreach (TileData tile in grid.GetTilesInRadius(pos, radius))
            {
                if (tile.Owner == Owner.Player && tile.Level > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
