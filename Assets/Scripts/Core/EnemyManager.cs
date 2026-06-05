// ============================================================================
// 파일:    Core/EnemyManager.cs
// 프로젝트: Project SEVERANCE
// 용도: 확률 기반 적 AI 전략 선택과 진원 배치.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Severance
{
    /// <summary>
    /// 매 턴 행동 여부를 확률로 결정한 뒤, 여러 전략 패턴 중 하나를 가중치 랜덤으로 실행합니다.
    /// 실제 영토 확장은 <see cref="EmitterManager"/>의 적 확장 단계가 담당합니다.
    /// </summary>
    public class EnemyManager : Singleton<EnemyManager>
    {
        private enum EnemyStrategy
        {
            FrontlineBuild,
            Spearhead,
            Fanout,
            HiddenIncursion,
            FlankIncursion,
            ResourceRaid,
            Consolidate
        }

        private struct BuildCandidate
        {
            public Vector2Int Position;
            public int Level;
            public EmitterDirection Direction;
            public int Score;
            public bool IgnorePlacementRules;
            public bool BypassesBaseRequirement;
            public string Label;

            public BuildCandidate(
                Vector2Int position,
                int level,
                EmitterDirection direction,
                int score,
                bool ignorePlacementRules,
                bool bypassesBaseRequirement,
                string label)
            {
                Position = position;
                Level = level;
                Direction = direction;
                Score = score;
                IgnorePlacementRules = ignorePlacementRules;
                BypassesBaseRequirement = bypassesBaseRequirement;
                Label = label;
            }
        }

        private GameConfig _config;

        public void Initialize(GameConfig config)
        {
            _config = config;

            int stageSpawned = SpawnStageEnemyEmitters();
            if (stageSpawned == 0)
            {
                int existingEnemyCount = EmitterManager.HasInstance
                    ? EmitterManager.Instance.GetEmitters(Owner.Enemy).Count
                    : 0;
                if (TryExecuteStrategy(EnemyStrategy.HiddenIncursion, 0, allowVisibleIncursion: true))
                {
                    SeedNewEnemyStartingAreas(existingEnemyCount);
                }
            }
        }

        public void TickEnemySpawner(int currentTurn)
        {
            if (_config == null || currentTurn <= 0 || !RollPercent(_config.EnemyActionChance))
            {
                return;
            }

            bool acted = TryExecuteRandomStrategy(currentTurn, allowVisibleIncursion: false);
            if (acted && RollPercent(_config.EnemyComboActionChance))
            {
                TryExecuteRandomStrategy(currentTurn, allowVisibleIncursion: false);
            }
        }

        private int SpawnStageEnemyEmitters()
        {
            if (_config == null ||
                _config.Stage == null ||
                !_config.Stage.HasEnemyEmitters ||
                !GridManager.HasInstance ||
                !EmitterManager.HasInstance)
            {
                return 0;
            }

            int spawned = 0;
            GridManager grid = GridManager.Instance;
            foreach (StageEnemyEmitterNode node in _config.Stage.EnemyEmitters)
            {
                if (grid == null || !grid.IsInitialized || !grid.IsInBounds(node.position))
                {
                    continue;
                }

                TileData tile = grid.GetTile(node.position);
                if (tile == null || tile.Owner == Owner.Player || tile.IsOccupiedByEmitter)
                {
                    continue;
                }

                if (EmitterManager.Instance.TryPlaceEmitter(
                    node.position,
                    Owner.Enemy,
                    ClampEnemyLevel(node.level),
                    node.direction,
                    ignorePlacementRules: true,
                    isInitialPreset: true,
                    bypassesBaseRequirement: true))
                {
                    EmitterManager.Instance.SeedStartingArea(EmitterManager.Instance.GetEmitterAt(node.position));
                    spawned++;
                }
            }

            return spawned;
        }

        private bool TryExecuteRandomStrategy(int currentTurn, bool allowVisibleIncursion)
        {
            List<EnemyStrategy> strategies = BuildEnabledStrategies();
            while (strategies.Count > 0)
            {
                EnemyStrategy strategy = PickWeightedStrategy(strategies);
                strategies.Remove(strategy);

                if (TryExecuteStrategy(strategy, currentTurn, allowVisibleIncursion))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryExecuteStrategy(EnemyStrategy strategy, int currentTurn, bool allowVisibleIncursion)
        {
            if (!GridManager.HasInstance || !EmitterManager.HasInstance)
            {
                return false;
            }

            GridManager grid = GridManager.Instance;
            if (grid == null || !grid.IsInitialized)
            {
                return false;
            }

            int dynamicLevel = GetDynamicEnemyLevel(currentTurn);
            List<BuildCandidate> candidates;
            switch (strategy)
            {
                case EnemyStrategy.FrontlineBuild:
                    candidates = BuildEnemyTerritoryCandidates(grid, dynamicLevel, strategy, "전선 압박");
                    break;
                case EnemyStrategy.Spearhead:
                    candidates = BuildEnemyTerritoryCandidates(grid, dynamicLevel, strategy, "직선 돌파");
                    break;
                case EnemyStrategy.Fanout:
                    candidates = BuildEnemyTerritoryCandidates(grid, dynamicLevel, strategy, "확산");
                    break;
                case EnemyStrategy.HiddenIncursion:
                    candidates = BuildNeutralIncursionCandidates(grid, dynamicLevel, strategy, "은닉 침투", allowVisibleIncursion);
                    break;
                case EnemyStrategy.FlankIncursion:
                    candidates = BuildNeutralIncursionCandidates(grid, dynamicLevel, strategy, "측면 침투", allowVisibleIncursion);
                    break;
                case EnemyStrategy.ResourceRaid:
                    candidates = BuildNeutralIncursionCandidates(grid, dynamicLevel, strategy, "자원 견제", allowVisibleIncursion);
                    break;
                case EnemyStrategy.Consolidate:
                    candidates = BuildEnemyTerritoryCandidates(grid, dynamicLevel, strategy, "거점 강화");
                    break;
                default:
                    return false;
            }

            return TryPlaceBestCandidate(candidates);
        }

        private List<BuildCandidate> BuildEnemyTerritoryCandidates(
            GridManager grid,
            int dynamicLevel,
            EnemyStrategy strategy,
            string label)
        {
            List<BuildCandidate> candidates = new List<BuildCandidate>();
            List<Emitter> enemyEmitters = EmitterManager.Instance.GetEmitters(Owner.Enemy);
            Vector2Int core = _config != null ? _config.PlayerCorePosition : Vector2Int.zero;
            int minSpacing = GetEmitterSpacing(grid);

            foreach (TileData tile in grid.AllTiles)
            {
                if (tile.Owner != Owner.Enemy ||
                    tile.IsOccupiedByEmitter ||
                    tile.Level <= 0)
                {
                    continue;
                }

                int nearestEmitterDistance = GetNearestEmitterDistance(tile.Position, enemyEmitters);
                if (nearestEmitterDistance <= minSpacing)
                {
                    continue;
                }

                bool isFrontier = HasNonEnemyNeighbor(grid, tile.Position);
                bool nearResource = HasNearbyResource(grid, tile.Position);
                int level = Mathf.Clamp(Mathf.Min(dynamicLevel, tile.Level), 1, GetEnemyMaxLevel());
                EmitterDirection direction = PickTerritoryDirection(tile.Position, level, strategy);

                int score = GetTerritoryScore(grid, tile, core, nearestEmitterDistance, isFrontier, nearResource, strategy);
                candidates.Add(new BuildCandidate(
                    tile.Position,
                    level,
                    direction,
                    score,
                    ignorePlacementRules: false,
                    bypassesBaseRequirement: false,
                    label: label));
            }

            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            return candidates;
        }

        private List<BuildCandidate> BuildNeutralIncursionCandidates(
            GridManager grid,
            int dynamicLevel,
            EnemyStrategy strategy,
            string label,
            bool allowVisible)
        {
            List<BuildCandidate> candidates = new List<BuildCandidate>();
            List<Emitter> enemyEmitters = EmitterManager.Instance.GetEmitters(Owner.Enemy);
            Vector2Int core = _config != null ? _config.PlayerCorePosition : Vector2Int.zero;
            int safeRadius = GetCoreSafeRadius(grid);
            int minSpacing = GetEmitterSpacing(grid);

            foreach (TileData tile in grid.AllTiles)
            {
                if (tile.Owner != Owner.Neutral || tile.IsOccupiedByEmitter)
                {
                    continue;
                }

                int coreDistance = ChebyshevDistance(tile.Position, core);
                if (coreDistance <= safeRadius ||
                    HasAdjacentOwner(grid, tile.Position, Owner.Player) ||
                    (!allowVisible && IsVisibleToPlayer(tile.Position)))
                {
                    continue;
                }

                int nearestEnemyDistance = GetNearestEmitterDistance(tile.Position, enemyEmitters);
                if (nearestEnemyDistance <= minSpacing)
                {
                    continue;
                }

                int nearestPlayerDistance = GetNearestOwnerDistance(grid, tile.Position, Owner.Player, 10);
                bool hasResource = tile.ResourceType != TileResourceType.None || HasNearbyResource(grid, tile.Position);
                if (strategy == EnemyStrategy.ResourceRaid && !hasResource)
                {
                    continue;
                }

                EmitterDirection direction = PickIncursionDirection(tile.Position, dynamicLevel, strategy);
                int score = GetIncursionScore(tile, coreDistance, nearestEnemyDistance, nearestPlayerDistance, hasResource, strategy);
                candidates.Add(new BuildCandidate(
                    tile.Position,
                    dynamicLevel,
                    direction,
                    score,
                    ignorePlacementRules: true,
                    bypassesBaseRequirement: true,
                    label: label));
            }

            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            return candidates;
        }

        private bool TryPlaceBestCandidate(List<BuildCandidate> candidates)
        {
            foreach (BuildCandidate candidate in candidates)
            {
                if (EmitterManager.Instance.TryPlaceEmitter(
                    candidate.Position,
                    Owner.Enemy,
                    candidate.Level,
                    candidate.Direction,
                    ignorePlacementRules: candidate.IgnorePlacementRules,
                    bypassesBaseRequirement: candidate.BypassesBaseRequirement))
                {
                    Debug.Log($"[EnemyManager] 전략 실행({candidate.Label}): Lv{candidate.Level} {candidate.Direction} @{candidate.Position}");
                    return true;
                }
            }

            return false;
        }

        private void SeedNewEnemyStartingAreas(int existingEnemyCount)
        {
            if (!EmitterManager.HasInstance)
            {
                return;
            }

            List<Emitter> enemyEmitters = EmitterManager.Instance.GetEmitters(Owner.Enemy);
            for (int i = Mathf.Max(0, existingEnemyCount); i < enemyEmitters.Count; i++)
            {
                EmitterManager.Instance.SeedStartingArea(enemyEmitters[i]);
            }
        }

        private List<EnemyStrategy> BuildEnabledStrategies()
        {
            List<EnemyStrategy> pool = new List<EnemyStrategy>();
            AddIfWeighted(pool, EnemyStrategy.FrontlineBuild);
            AddIfWeighted(pool, EnemyStrategy.Spearhead);
            AddIfWeighted(pool, EnemyStrategy.Fanout);
            AddIfWeighted(pool, EnemyStrategy.HiddenIncursion);
            AddIfWeighted(pool, EnemyStrategy.FlankIncursion);
            AddIfWeighted(pool, EnemyStrategy.ResourceRaid);
            AddIfWeighted(pool, EnemyStrategy.Consolidate);

            if (pool.Count == 0)
            {
                pool.Add(EnemyStrategy.FrontlineBuild);
                pool.Add(EnemyStrategy.HiddenIncursion);
            }

            return pool;
        }

        private void AddIfWeighted(List<EnemyStrategy> pool, EnemyStrategy strategy)
        {
            if (GetStrategyWeight(strategy) > 0)
            {
                pool.Add(strategy);
            }
        }

        private EnemyStrategy PickWeightedStrategy(List<EnemyStrategy> strategies)
        {
            int totalWeight = 0;
            foreach (EnemyStrategy strategy in strategies)
            {
                totalWeight += GetStrategyWeight(strategy);
            }

            if (totalWeight <= 0)
            {
                return strategies[Random.Range(0, strategies.Count)];
            }

            int roll = Random.Range(0, totalWeight);
            foreach (EnemyStrategy strategy in strategies)
            {
                roll -= GetStrategyWeight(strategy);
                if (roll < 0)
                {
                    return strategy;
                }
            }

            return strategies[strategies.Count - 1];
        }

        private int GetStrategyWeight(EnemyStrategy strategy)
        {
            switch (strategy)
            {
                case EnemyStrategy.FrontlineBuild:
                    return Mathf.Clamp(_config.EnemyFrontlineBuildWeight, 0, 100);
                case EnemyStrategy.Spearhead:
                    return Mathf.Clamp(_config.EnemySpearheadWeight, 0, 100);
                case EnemyStrategy.Fanout:
                    return Mathf.Clamp(_config.EnemyFanoutWeight, 0, 100);
                case EnemyStrategy.HiddenIncursion:
                    return Mathf.Clamp(_config.EnemyHiddenIncursionWeight, 0, 100);
                case EnemyStrategy.FlankIncursion:
                    return Mathf.Clamp(_config.EnemyFlankIncursionWeight, 0, 100);
                case EnemyStrategy.ResourceRaid:
                    return Mathf.Clamp(_config.EnemyResourceRaidWeight, 0, 100);
                case EnemyStrategy.Consolidate:
                    return Mathf.Clamp(_config.EnemyConsolidateWeight, 0, 100);
                default:
                    return 0;
            }
        }

        private int GetTerritoryScore(
            GridManager grid,
            TileData tile,
            Vector2Int core,
            int nearestEmitterDistance,
            bool isFrontier,
            bool nearResource,
            EnemyStrategy strategy)
        {
            int distanceToCore = ManhattanDistance(tile.Position, core);
            int pressureScore = Mathf.Max(0, grid.Width + grid.Height - distanceToCore) * 3;
            int spacingScore = Mathf.Min(nearestEmitterDistance, 12) * 6;
            int score = pressureScore + spacingScore + tile.Level * 20 + Random.Range(0, 5);

            switch (strategy)
            {
                case EnemyStrategy.FrontlineBuild:
                    return score + (isFrontier ? 70 : -40);
                case EnemyStrategy.Spearhead:
                    return score + GetAxisPressureScore(tile.Position, core) + (isFrontier ? 20 : 0);
                case EnemyStrategy.Fanout:
                    return score + spacingScore + (isFrontier ? 35 : 0);
                case EnemyStrategy.Consolidate:
                    return score + tile.Level * 35 + (isFrontier ? -10 : 15);
                case EnemyStrategy.ResourceRaid:
                    return score + (nearResource ? 80 : -30);
                default:
                    return score;
            }
        }

        private int GetIncursionScore(
            TileData tile,
            int coreDistance,
            int nearestEnemyDistance,
            int nearestPlayerDistance,
            bool hasResource,
            EnemyStrategy strategy)
        {
            int distanceScore = Mathf.Min(nearestEnemyDistance, 14) * 7 + Random.Range(0, 5);
            switch (strategy)
            {
                case EnemyStrategy.HiddenIncursion:
                    return distanceScore + coreDistance * 4;
                case EnemyStrategy.FlankIncursion:
                    return distanceScore + Mathf.Max(0, 10 - nearestPlayerDistance) * 18;
                case EnemyStrategy.ResourceRaid:
                    return distanceScore + (hasResource ? 120 : 0) + Mathf.Max(0, 8 - nearestPlayerDistance) * 10;
                default:
                    return distanceScore;
            }
        }

        private EmitterDirection PickTerritoryDirection(Vector2Int from, int level, EnemyStrategy strategy)
        {
            switch (strategy)
            {
                case EnemyStrategy.Spearhead:
                    return PickDirectionTowardCore(from);
                case EnemyStrategy.Fanout:
                    return EmitterDirection.Cross;
                case EnemyStrategy.Consolidate:
                    return EmitterDirection.Cross;
                case EnemyStrategy.ResourceRaid:
                    return level >= 2 ? EmitterDirection.Cross : PickDirectionTowardCore(from);
                default:
                    return level >= 2 ? EmitterDirection.Cross : PickDirectionTowardCore(from);
            }
        }

        private EmitterDirection PickIncursionDirection(Vector2Int from, int level, EnemyStrategy strategy)
        {
            if (strategy == EnemyStrategy.HiddenIncursion)
            {
                return level >= 3 ? EmitterDirection.Cross : PickDirectionTowardCore(from);
            }

            if (strategy == EnemyStrategy.ResourceRaid)
            {
                return level >= 2 ? EmitterDirection.Cross : PickDirectionTowardCore(from);
            }

            return level >= 2 ? EmitterDirection.Cross : PickDirectionTowardCore(from);
        }

        private EmitterDirection PickDirectionTowardCore(Vector2Int from)
        {
            Vector2Int core = _config != null ? _config.PlayerCorePosition : Vector2Int.zero;
            Vector2Int delta = core - from;

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                return delta.x >= 0 ? EmitterDirection.Right : EmitterDirection.Left;
            }

            return delta.y >= 0 ? EmitterDirection.Up : EmitterDirection.Down;
        }

        private int GetDynamicEnemyLevel(int currentTurn)
        {
            int growthChance = _config != null ? Mathf.Clamp(_config.EnemyLevelGrowthChance, 0, 100) : 8;
            int level = 1 + Mathf.FloorToInt(Mathf.Max(0, currentTurn) * growthChance / 100f);
            if (_config != null && RollPercent(_config.EnemyHighLevelSpikeChance))
            {
                level++;
            }

            return ClampEnemyLevel(level);
        }

        private int ClampEnemyLevel(int level)
        {
            return Mathf.Clamp(level, 1, GetEnemyMaxLevel());
        }

        private int GetEnemyMaxLevel()
        {
            return _config != null ? Mathf.Max(1, _config.MaxLevel) : 5;
        }

        private int GetCoreSafeRadius(GridManager grid)
        {
            int percent = _config != null ? Mathf.Clamp(_config.EnemyCoreSafeZonePercent, 0, 100) : 22;
            return Mathf.CeilToInt(Mathf.Min(grid.Width, grid.Height) * percent / 100f);
        }

        private int GetEmitterSpacing(GridManager grid)
        {
            int percent = _config != null ? Mathf.Clamp(_config.EnemyEmitterSpacingPercent, 0, 100) : 10;
            return Mathf.Max(1, Mathf.CeilToInt(Mathf.Min(grid.Width, grid.Height) * percent / 100f));
        }

        private static bool RollPercent(int percent)
        {
            return Random.Range(0, 100) < Mathf.Clamp(percent, 0, 100);
        }

        private bool HasNonEnemyNeighbor(GridManager grid, Vector2Int pos)
        {
            foreach (TileData neighbor in grid.GetNeighbors(pos, includeDiagonals: false))
            {
                if (neighbor.Owner != Owner.Enemy)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasAdjacentOwner(GridManager grid, Vector2Int pos, Owner owner)
        {
            foreach (TileData neighbor in grid.GetNeighbors(pos, includeDiagonals: true))
            {
                if (neighbor.Owner == owner)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasNearbyResource(GridManager grid, Vector2Int pos)
        {
            foreach (TileData tile in grid.GetTilesInRadius(pos, 2))
            {
                if (tile.ResourceType != TileResourceType.None)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsVisibleToPlayer(Vector2Int pos)
        {
            return _config != null &&
                   _config.EnableFogOfWar &&
                   FogOfWarManager.HasInstance &&
                   FogOfWarManager.Instance.IsTileVisible(pos);
        }

        private static int GetNearestEmitterDistance(Vector2Int pos, List<Emitter> emitters)
        {
            if (emitters == null || emitters.Count == 0)
            {
                return 9999;
            }

            int nearest = 9999;
            foreach (Emitter emitter in emitters)
            {
                if (emitter == null)
                {
                    continue;
                }

                nearest = Mathf.Min(nearest, ChebyshevDistance(pos, emitter.Position));
            }

            return nearest;
        }

        private static int GetNearestOwnerDistance(GridManager grid, Vector2Int pos, Owner owner, int searchRadius)
        {
            int nearest = 9999;
            foreach (TileData tile in grid.GetTilesInRadius(pos, searchRadius))
            {
                if (tile.Owner == owner)
                {
                    nearest = Mathf.Min(nearest, ChebyshevDistance(pos, tile.Position));
                }
            }

            return nearest;
        }

        private static int GetAxisPressureScore(Vector2Int pos, Vector2Int target)
        {
            Vector2Int delta = target - pos;
            return Mathf.Abs(Mathf.Abs(delta.x) - Mathf.Abs(delta.y)) * 4;
        }

        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static int ChebyshevDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }
    }
}
