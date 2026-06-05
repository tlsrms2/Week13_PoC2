// ============================================================================
// 파일:    Core/ResourceManager.cs
// 프로젝트: Project SEVERANCE
// 용도: 플레이어 전력 및 광맥 자원 보유량, 생산/수거/확장 비용 처리.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Severance
{
    /// <summary>
    /// 플레이어 자원 상태를 관리하고, 점령한 자원 타일을 턴마다 정산합니다.
    /// </summary>
    public class ResourceManager : Singleton<ResourceManager>
    {
        #region Fields

        private GameConfig _config;
        private int _resourceTurnCounter;

        #endregion

        #region Properties

        public int Power => GetTotalPowerCapacity();
        public int Iron { get; private set; }
        public int Copper { get; private set; }
        public int PowerProductionPerTurn { get; private set; }
        public int IronProductionPerTurn { get; private set; }
        public int CopperProductionPerTurn { get; private set; }

        #endregion

        #region Public API

        public void Initialize(GameConfig config)
        {
            _config = config;
            Iron = 0;
            Copper = 0;
            PowerProductionPerTurn = 0;
            IronProductionPerTurn = 0;
            CopperProductionPerTurn = 0;
            _resourceTurnCounter = 0;

            GameEvents.RaiseResourceChanged();
        }

        /// <summary>
        /// 맵에 기본 자원 노드를 무작위 배치합니다.
        /// </summary>
        public void SeedResources()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || !grid.IsInitialized || _config == null)
            {
                return;
            }

            if (_config.Stage != null && _config.Stage.HasResourceNodes)
            {
                PlaceStageResources(_config.Stage);
                return;
            }

            PlaceRandomResources(TileResourceType.PowerNode, Mathf.Max(0, _config.PowerSetting.count), _config.PowerSetting.gainAmount);
            PlaceRandomResources(TileResourceType.VeinIron, Mathf.Max(0, _config.IronSetting.count), _config.IronSetting.gainAmount);
            PlaceRandomResources(TileResourceType.VeinCopper, Mathf.Max(0, _config.CopperSetting.count), _config.CopperSetting.gainAmount);
        }

        public bool CanExpand(Owner owner)
        {
            return true;
        }

        public bool SpendExpansionPower(Owner owner)
        {
            return true;
        }

        public bool CanPlaceEmitter(Owner owner, EmitterDirection direction, int level, bool isCore)
        {
            if (owner != Owner.Player || isCore)
            {
                return true;
            }

            TileResourceType required = GetRequiredResource(direction);
            int cost = GetEmitterPlacementCost(direction, level);
            return required == TileResourceType.None || cost <= 0 || GetVeinAmount(required) >= cost;
        }

        public bool SpendEmitterPlacementCost(Owner owner, EmitterDirection direction, int level, bool isCore)
        {
            if (owner != Owner.Player || isCore)
            {
                return true;
            }

            TileResourceType required = GetRequiredResource(direction);
            int cost = GetEmitterPlacementCost(direction, level);
            if (required == TileResourceType.None)
            {
                return true;
            }

            if (cost <= 0)
            {
                return true;
            }

            if (GetVeinAmount(required) < cost)
            {
                Debug.LogWarning($"[ResourceManager] {direction} (레벨 {level}) 진원 설치 비용({GetEmitterPlacementCostText(direction, level)})이 부족합니다.");
                return false;
            }

            AddVein(required, -cost);
            GameEvents.RaiseResourceChanged();
            return true;
        }

        public TileResourceType GetRequiredResource(EmitterDirection direction)
        {
            return _config != null ? _config.GetRequiredResource(direction) : TileResourceType.None;
        }

        public int GetEmitterPlacementCost(EmitterDirection direction, int level = 1)
        {
            int baseCost = _config != null ? Mathf.Max(0, _config.GetEmitterPlacementCost(direction)) : 0;
            return baseCost > 0 ? baseCost + (level - 1) : 0;
        }

        public string GetEmitterPlacementCostText(EmitterDirection direction, int level = 1)
        {
            TileResourceType resource = GetRequiredResource(direction);
            int cost = GetEmitterPlacementCost(direction, level);
            if (resource == TileResourceType.None || cost <= 0)
            {
                return "비용 없음";
            }

            return $"{GameConfig.GetResourceLabel(resource)} x{cost}";
        }

        public bool CanAffordEmitterPlacement(EmitterDirection direction, int level = 1)
        {
            TileResourceType required = GetRequiredResource(direction);
            int cost = GetEmitterPlacementCost(direction, level);
            return required == TileResourceType.None || cost <= 0 || GetVeinAmount(required) >= cost;
        }

        /// <summary>
        /// 점령한 전력 노드 생산과 광맥 주기 생산을 정산합니다.
        /// </summary>
        public void UpdateResources()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || !grid.IsInitialized)
            {
                return;
            }

            _resourceTurnCounter++;

            int powerInterval = _config != null ? Mathf.Max(1, _config.PowerSetting.productionInterval) : 1;
            int ironInterval = _config != null ? Mathf.Max(1, _config.IronSetting.productionInterval) : 1;
            int copperInterval = _config != null ? Mathf.Max(1, _config.CopperSetting.productionInterval) : 1;

            bool isPowerTurn = (_resourceTurnCounter % powerInterval == 0);
            bool isIronTurn = (_resourceTurnCounter % ironInterval == 0);
            bool isCopperTurn = (_resourceTurnCounter % copperInterval == 0);

            int totalPowerYield = 0;
            int totalIronYield = 0;
            int totalCopperYield = 0;

            int gainedPower = 0;
            int gainedIron = 0;
            int gainedCopper = 0;

            foreach (TileData tile in grid.AllTiles)
            {
                if (tile.Owner != Owner.Player || tile.Level <= 0)
                {
                    continue;
                }

                switch (TileData.NormalizeResourceType(tile.ResourceType))
                {
                    case TileResourceType.PowerNode:
                        int pYield = Mathf.Max(0, GetTileYield(tile));
                        totalPowerYield += pYield;
                        if (isPowerTurn) gainedPower += pYield;
                        break;

                    case TileResourceType.VeinIron:
                        int iYield = GetTileYield(tile);
                        totalIronYield += iYield;
                        if (isIronTurn) gainedIron += iYield;
                        break;

                    case TileResourceType.VeinCopper:
                        int cYield = GetTileYield(tile);
                        totalCopperYield += cYield;
                        if (isCopperTurn) gainedCopper += cYield;
                        break;
                }
            }

            if (gainedIron > 0) Iron += gainedIron;
            if (gainedCopper > 0) Copper += gainedCopper;

            // UI 표시용 턴당 생산량 (평균 효율 표기)
            PowerProductionPerTurn = powerInterval > 0 ? (totalPowerYield / powerInterval) : 0;
            IronProductionPerTurn = ironInterval > 0 ? (totalIronYield / ironInterval) : 0;
            CopperProductionPerTurn = copperInterval > 0 ? (totalCopperYield / copperInterval) : 0;

            GameEvents.RaiseResourceChanged();
        }

        #endregion

        #region Helpers

        private void PlaceRandomResources(TileResourceType type, int count, int yield = 1)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || !grid.IsInitialized)
            {
                return;
            }

            List<TileData> candidates = new List<TileData>();
            foreach (TileData tile in grid.AllTiles)
            {
                if (tile.ResourceType == TileResourceType.None)
                {
                    candidates.Add(tile);
                }
            }

            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int index = Random.Range(0, candidates.Count);
                candidates[index].SetResource(type, yield);
                candidates.RemoveAt(index);
            }
        }

        private void PlaceStageResources(StageConfig stage)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || !grid.IsInitialized || stage == null)
            {
                return;
            }

            HashSet<Vector2Int> used = new HashSet<Vector2Int>();
            foreach (StageResourceNode node in stage.ResourceNodes)
            {
                if (node.ResourceType == TileResourceType.None ||
                    !grid.IsInBounds(node.Position) ||
                    !used.Add(node.Position))
                {
                    continue;
                }

                TileData tile = grid.GetTile(node.Position);
                if (tile == null)
                {
                    continue;
                }

                tile.SetResource(node.ResourceType, node.Yield);
            }
        }

        private static int GetTileYield(TileData tile)
        {
            return tile != null ? Mathf.Clamp(tile.ResourceYield, 1, 4) : 0;
        }

        private int GetVeinAmount(TileResourceType type)
        {
            switch (type)
            {
                case TileResourceType.VeinIron:
                    return Iron;
                case TileResourceType.VeinCopper:
                    return Copper;
                case TileResourceType.VeinSilicon:
                    return Copper;
                default:
                    return 0;
            }
        }

        private void AddVein(TileResourceType type, int amount)
        {
            switch (type)
            {
                case TileResourceType.VeinIron:
                    Iron = Mathf.Max(0, Iron + amount);
                    break;
                case TileResourceType.VeinCopper:
                    Copper = Mathf.Max(0, Copper + amount);
                    break;
                case TileResourceType.VeinSilicon:
                    Copper = Mathf.Max(0, Copper + amount);
                    break;
            }
        }

        public int GetTotalPowerCapacity()
        {
            if (_config == null) return 0;
            int baseCapacity = Mathf.Max(0, _config.InitialPower);
            int nodeCapacity = 0;

            GridManager grid = GridManager.Instance;
            if (grid != null && grid.IsInitialized)
            {
                foreach (TileData tile in grid.AllTiles)
                {
                    if (tile.Owner == Owner.Player &&
                        tile.Level > 0 &&
                        TileData.NormalizeResourceType(tile.ResourceType) == TileResourceType.PowerNode)
                    {
                        nodeCapacity += Mathf.Max(0, tile.ResourceYield);
                    }
                }
            }
            return baseCapacity + nodeCapacity;
        }

        public int GetPlayerNextTurnPowerLoad()
        {
            return EmitterManager.HasInstance ? EmitterManager.Instance.GetPlayerNextTurnPowerLoad() : 0;
        }

        #endregion
    }
}
