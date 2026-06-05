// ============================================================================
// 파일:    Emitter/EmitterManager.cs
// 프로젝트: Project SEVERANCE
// 용도: 씬 상의 이미터들의 생성, 제거, 활성 토글 및 턴별 영토 확장을 주도하는 매니저.
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Severance
{
    /// <summary>
    /// 이미터 배치 검증 규칙 수행, 토글 제어, 매 턴 확장 알고리즘 구동, 그리고 이미터 해제 소멸 처리를 주관하는
    /// 싱글톤 이미터 매니저 컴포넌트입니다.
    /// </summary>
    public class EmitterManager : Singleton<EmitterManager>
    {
        #region Private State

        /// <summary>영역 확장 요청을 담기 위한 내부 구조체.</summary>
        private struct ExpansionRequest
        {
            public Emitter Emitter;
            public Vector2Int Direction;
            public Vector2Int TargetPosition;
            public int Level;

            public ExpansionRequest(Emitter emitter, Vector2Int direction, Vector2Int targetPosition, int level)
            {
                Emitter = emitter;
                Direction = direction;
                TargetPosition = targetPosition;
                Level = level;
            }
        }

        /// <summary>플레이어 진영의 활성화된 이미터 목록.</summary>
        private readonly List<Emitter> _playerEmitters = new List<Emitter>();

        /// <summary>적 진영의 활성화된 이미터 목록.</summary>
        private readonly List<Emitter> _enemyEmitters = new List<Emitter>();

        /// <summary>그리드 좌표와 이미터 인스턴스 간의 매핑 색인 사전.</summary>
        private readonly Dictionary<Vector2Int, Emitter> _emittersByPosition =
            new Dictionary<Vector2Int, Emitter>();

        /// <summary>플레이어 진영의 턴별 확장 요청 목록.</summary>
        private readonly List<ExpansionRequest> _playerRequests = new List<ExpansionRequest>();

        /// <summary>적 진영의 턴별 확장 요청 목록.</summary>
        private readonly List<ExpansionRequest> _enemyRequests = new List<ExpansionRequest>();

        #endregion

        #region Public API — Queries

        /// <summary>
        /// 특정 그리드 좌표 상에 위치한 이미터가 존재하면 인스턴스를 반환하고, 없으면 null을 반환합니다.
        /// </summary>
        /// <param name="position">조회할 그리드 좌표.</param>
        /// <returns>이미터 인스턴스 또는 null.</returns>
        public Emitter GetEmitterAt(Vector2Int position)
        {
            _emittersByPosition.TryGetValue(position, out Emitter emitter);
            return emitter;
        }

        /// <summary>
        /// 특정 진영이 가동 중인 모든 이미터 목록을 복사하여 반환합니다.
        /// </summary>
        /// <param name="owner">조회할 진영.</param>
        /// <returns>이미터 복사 리스트.</returns>
        public List<Emitter> GetEmitters(Owner owner)
        {
            switch (owner)
            {
                case Owner.Player:
                    return new List<Emitter>(_playerEmitters);
                case Owner.Enemy:
                    return new List<Emitter>(_enemyEmitters);
                default:
                    return new List<Emitter>();
            }
        }

        public bool CanPlaceEmitter(Vector2Int position, Owner owner, int level,
                                    EmitterDirection direction, bool isCore = false,
                                    bool ignorePlacementRules = false)
        {
            if (owner == Owner.Neutral || level <= 0)
            {
                return false;
            }

            GridManager grid = GridManager.Instance;
            if (grid == null || !grid.IsInBounds(position))
            {
                return false;
            }

            TileData tile = grid.GetTile(position);
            if (tile == null || tile.IsOccupiedByEmitter)
            {
                return false;
            }

            if (!ignorePlacementRules && !isCore)
            {
                int minLevelToLimit = (owner == Owner.Player) ? 1 : 2;
                if (level >= minLevelToLimit)
                {
                    if (tile.Owner != owner || tile.Level < level)
                    {
                        return false;
                    }
                }
            }

            return !ResourceManager.HasInstance ||
                   ResourceManager.Instance.CanPlaceEmitter(owner, direction, level, isCore);
        }

        #endregion

        #region Public API — Placement

        /// <summary>
        /// 특정 좌표에 이미터를 배치하기 위한 유효 조건 점검 및 배치 프로세스를 시도합니다.
        /// <para><b>배치 조건 규칙 점검:</b></para>
        /// <list type="bullet">
        ///   <item>좌표가 그리드 공간 유효 경계 안쪽이어야 함.</item>
        ///   <item>목표 타일 상에 타 이미터가 없어야 함.</item>
        ///   <item>레벨 1 이미터: 기반 영역 없이 자유 배치 가능.</item>
        ///   <item>레벨 2 이상 이미터: 아군 소유 영토 위에만 배치 가능하며, 타일의 영토 레벨이 이미터 레벨 이상이어야 함.</item>
        /// </list>
        /// </summary>
        /// <param name="position">배치 지점 그리드 좌표.</param>
        /// <param name="owner">설치 진영 (중립 Faction은 불가).</param>
        /// <param name="level">설치할 이미터 등급 (1 이상).</param>
        /// <param name="direction">영토 확장 방향 템플릿.</param>
        /// <returns>
        /// 설치 성공 시 true, 규칙 미충족 실패 시 false를 반환합니다.
        /// </returns>
        public bool TryPlaceEmitter(Vector2Int position, Owner owner, int level,
                                     EmitterDirection direction, bool isCore = false,
                                     bool ignorePlacementRules = false,
                                     bool isInitialPreset = false,
                                     bool bypassesBaseRequirement = false)
        {
            // --- 기본 전제값 예외 체크 ---
            if (owner == Owner.Neutral)
            {
                Debug.LogWarning("[EmitterManager] 중립 상태로는 이미터를 설치할 수 없습니다.");
                return false;
            }

            GridManager grid = GridManager.Instance;
            if (grid == null)
            {
                Debug.LogError("[EmitterManager] GridManager 컴포넌트 인스턴스가 존재하지 않습니다.");
                return false;
            }

            if (!grid.IsInBounds(position))
            {
                Debug.LogWarning($"[EmitterManager] 설치 대상 {position} 좌표가 그리드 범위를 이탈했습니다.");
                return false;
            }

            TileData tile = grid.GetTile(position);
            if (tile == null)
            {
                Debug.LogError($"[EmitterManager] {position} 좌표 타일 데이터가 null입니다.");
                return false;
            }

            // --- 중복 배치 금지 검사 ---
            if (tile.IsOccupiedByEmitter)
            {
                Debug.LogWarning(
                    $"[EmitterManager] 타일 {position} 위치에 이미 이미터가 상주하고 있습니다.");
                return false;
            }

            // --- 소유권 및 등급 레벨 제한 규칙 검사 ---
            if (level <= 0)
            {
                Debug.LogWarning("[EmitterManager] 이미터 레벨은 최소 1 이상이어야 합니다.");
                return false;
            }

            bool checkRules = !ignorePlacementRules && !isCore;
            int limitLevel = (owner == Owner.Player) ? 1 : 2;

            if (checkRules && level >= limitLevel)
            {
                if (tile.Owner != owner)
                {
                    Debug.LogWarning(
                        $"[EmitterManager] 레벨 {level} 이미터 설치를 위해서는 아군 소유 영토가 필요합니다 (현 소유: {tile.Owner}).");
                    return false;
                }

                if (tile.Level < level)
                {
                    Debug.LogWarning(
                        $"[EmitterManager] 타일 레벨 {tile.Level}이 이미터 레벨 {level} 요구치보다 낮아 설치할 수 없습니다.");
                    return false;
                }
            }

            if (ResourceManager.HasInstance &&
                !ResourceManager.Instance.SpendEmitterPlacementCost(owner, direction, level, isCore))
            {
                return false;
            }

            // --- 이미터 객체 생성 및 시스템 등록 ---
            Emitter emitter = new Emitter(
                position,
                owner,
                level,
                direction,
                isCore,
                isInitialPreset,
                bypassesBaseRequirement);

            switch (owner)
            {
                case Owner.Player:
                    _playerEmitters.Add(emitter);
                    break;
                case Owner.Enemy:
                    _enemyEmitters.Add(emitter);
                    break;
            }

            _emittersByPosition[position] = emitter;

            // 설치 위치 타일도 종속성 기준점으로 등록한다.
            tile.Emitter = emitter;
            tile.SetOwnership(owner, emitter.Level, emitter);

            Debug.Log($"[EmitterManager] 이미터 생성 배치 완료: {emitter}");
            GameEvents.RaiseEmitterPlaced(emitter);

            grid.NotifyTileChanged(position);
            RefreshExpansionIntentPreviews();
            GameEvents.RaiseResourceChanged();

            return true;
        }

        #endregion

        #region Public API — Toggle

        /// <summary>
        /// 이미터 동작 전원을 켜고 끄며(ON/OFF 토글) 관련 변경 이벤트를 발생시킵니다.
        /// </summary>
        /// <param name="emitter">토글할 이미터 인스턴스.</param>
        public void ToggleEmitter(Emitter emitter)
        {
            if (emitter == null)
            {
                Debug.LogWarning("[EmitterManager] null인 이미터는 토글 조작을 처리할 수 없습니다.");
                return;
            }

            if (emitter.IsCore)
            {
                Debug.LogWarning("[EmitterManager] 플레이어 코어는 전원을 끌 수 없습니다.");
                return;
            }

            emitter.Toggle();
            Debug.Log($"[EmitterManager] 이미터 토글 가동: {emitter}");

            if (GridManager.HasInstance)
            {
                GridManager.Instance.NotifyTileChanged(emitter.Position);
            }

            RefreshExpansionIntentPreviews();
            GameEvents.RaiseResourceChanged();
            GameEvents.RaiseEmitterToggled(emitter);
        }

        #endregion

        #region Public API — Expansion

        /// <summary>
        /// 지정한 진영에 소유된 모든 가동 중인 이미터들의 다음 영토 확장 요청을 수집합니다.
        /// </summary>
        /// <param name="owner">확장 구동을 개시할 진영.</param>
        public void ExpandAll(Owner owner)
        {
            List<Emitter> emitters = (owner == Owner.Player) ? _playerEmitters : _enemyEmitters;
            GridManager grid = GridManager.Instance;

            if (grid == null)
            {
                Debug.LogError("[EmitterManager] 영토 확장을 구동할 GridManager를 찾을 수 없습니다.");
                return;
            }

            if (owner == Owner.Player)
                _playerRequests.Clear();
            else
                _enemyRequests.Clear();

            foreach (Emitter emitter in emitters)
            {
                if (!emitter.IsOn || emitter.IsCore)
                {
                    continue;
                }

                if (owner == Owner.Player &&
                    ResourceManager.HasInstance &&
                    !ResourceManager.Instance.CanExpand(owner))
                {
                    Debug.LogWarning("[EmitterManager] 전력 부족으로 플레이어 확장이 정지되었습니다.");
                    break;
                }

                CollectExpansionRequests(emitter, grid);
            }
        }

        /// <summary>
        /// 수집된 플레이어와 적의 확장 요청을 바탕으로 충돌(교착 및 Push)을 해결합니다.
        /// </summary>
        public void ResolveCollisions()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return;

            // 1. 모든 요청이 들어온 타일의 유니크 좌표 식별
            HashSet<Vector2Int> affectedTiles = new HashSet<Vector2Int>();
            foreach (var req in _playerRequests) affectedTiles.Add(req.TargetPosition);
            foreach (var req in _enemyRequests) affectedTiles.Add(req.TargetPosition);

            // 2. 타일별로 플레이어와 적의 확장 요청 그룹화
            Dictionary<Vector2Int, List<ExpansionRequest>> playerReqsByTile = new Dictionary<Vector2Int, List<ExpansionRequest>>();
            Dictionary<Vector2Int, List<ExpansionRequest>> enemyReqsByTile = new Dictionary<Vector2Int, List<ExpansionRequest>>();

            foreach (var req in _playerRequests)
            {
                if (!playerReqsByTile.ContainsKey(req.TargetPosition))
                    playerReqsByTile[req.TargetPosition] = new List<ExpansionRequest>();
                playerReqsByTile[req.TargetPosition].Add(req);
            }

            foreach (var req in _enemyRequests)
            {
                if (!enemyReqsByTile.ContainsKey(req.TargetPosition))
                    enemyReqsByTile[req.TargetPosition] = new List<ExpansionRequest>();
                enemyReqsByTile[req.TargetPosition].Add(req);
            }

            // 3. 타일별 충돌 및 영역 점유 규칙 적용
            foreach (Vector2Int pos in affectedTiles)
            {
                TileData tile = grid.GetTile(pos);
                if (tile == null) continue;

                bool hasPlayer = playerReqsByTile.TryGetValue(pos, out List<ExpansionRequest> pList);
                bool hasEnemy = enemyReqsByTile.TryGetValue(pos, out List<ExpansionRequest> eList);

                int totalPlayerLvl = CalculateInfluenceLevel(tile, Owner.Player, hasPlayer ? pList : null);
                int totalEnemyLvl = CalculateInfluenceLevel(tile, Owner.Enemy, hasEnemy ? eList : null);

                // --- 세력 관계에 따른 충돌 해결 판정 ---
                
                // 활성 영토 상태 판단 (해당 진영의 활성화된 영향력이 있는 상태)
                bool isPlayerActiveTerritory = (tile.Owner == Owner.Player && totalPlayerLvl > 0);
                bool isEnemyActiveTerritory = (tile.Owner == Owner.Enemy && totalEnemyLvl > 0);

                if (isPlayerActiveTerritory && isEnemyActiveTerritory)
                {
                    // 양쪽 활성 영토가 겹쳐서 교착되어 있던 특수 상황
                    if (totalPlayerLvl > totalEnemyLvl)
                    {
                        ApplyExpansion(Owner.Player, totalPlayerLvl, pList, tile);
                    }
                    else if (totalEnemyLvl > totalPlayerLvl)
                    {
                        ApplyExpansion(Owner.Enemy, totalEnemyLvl, eList, tile);
                    }
                    else
                    {
                        // 동급 충돌 시, 기존 소유주를 유지하여 교착 전선 유지
                        if (tile.Owner == Owner.Player)
                        {
                            ApplyExpansion(Owner.Player, totalPlayerLvl, pList, tile);
                        }
                        else
                        {
                            ApplyExpansion(Owner.Enemy, totalEnemyLvl, eList, tile);
                        }
                    }
                }
                else if (isPlayerActiveTerritory)
                {
                    // 플레이어 활성 영토에 적 침입 시도
                    if (totalEnemyLvl > 0)
                    {
                        if (totalPlayerLvl > totalEnemyLvl)
                        {
                            ApplyExpansion(Owner.Player, totalPlayerLvl, pList, tile);
                        }
                        else if (totalPlayerLvl < totalEnemyLvl)
                        {
                            // 적 레벨이 더 높아 플레이어 영토 점유 (Push)
                            ApplyExpansion(Owner.Enemy, totalEnemyLvl, eList, tile);
                        }
                        else
                        {
                            // 동급 충돌 -> 교착(Stalemate), 플레이어가 방어 성공하여 아군 영토 유지
                            ApplyExpansion(Owner.Player, totalPlayerLvl, pList, tile);
                            Debug.Log($"[EmitterManager] 플레이어 영토 교착 방어 성공: {pos} (플레이어 Lv {totalPlayerLvl} vs 적 Lv {totalEnemyLvl})");
                        }
                    }
                    else
                    {
                        ApplyExpansion(Owner.Player, totalPlayerLvl, pList, tile);
                    }
                }
                else if (isEnemyActiveTerritory)
                {
                    // 적군 활성 영토에 플레이어 침입 시도
                    if (totalPlayerLvl > 0)
                    {
                        if (totalEnemyLvl > totalPlayerLvl)
                        {
                            ApplyExpansion(Owner.Enemy, totalEnemyLvl, eList, tile);
                        }
                        else if (totalEnemyLvl < totalPlayerLvl)
                        {
                            // 플레이어 레벨이 더 높아 적군 영토 점유 (Push)
                            ApplyExpansion(Owner.Player, totalPlayerLvl, pList, tile);
                        }
                        else
                        {
                            // 동급 충돌 -> 교착(Stalemate), 적군이 방어 성공하여 적 영토 유지
                            ApplyExpansion(Owner.Enemy, totalEnemyLvl, eList, tile);
                            Debug.Log($"[EmitterManager] 적군 영토 교착 방어 성공: {pos} (적 Lv {totalEnemyLvl} vs 플레이어 Lv {totalPlayerLvl})");
                        }
                    }
                    else
                    {
                        ApplyExpansion(Owner.Enemy, totalEnemyLvl, eList, tile);
                    }
                }
                else
                {
                    // 중립 타일이거나 비활성 상태 타일인 경우
                    if (totalPlayerLvl > 0 && totalEnemyLvl > 0)
                    {
                        if (totalPlayerLvl > totalEnemyLvl)
                        {
                            ApplyExpansion(Owner.Player, totalPlayerLvl, pList, tile);
                        }
                        else if (totalEnemyLvl > totalPlayerLvl)
                        {
                            ApplyExpansion(Owner.Enemy, totalEnemyLvl, eList, tile);
                        }
                        else
                        {
                            // 중립지 동급 동시 진입 시 적군 우선 점령 적용
                            ApplyExpansion(Owner.Enemy, totalEnemyLvl, eList, tile);
                            Debug.Log($"[EmitterManager] 중립지 동급 충돌 적군 우선 점령 적용: {pos} (레벨 {totalEnemyLvl})");
                        }
                    }
                    else if (totalPlayerLvl > 0)
                    {
                        ApplyExpansion(Owner.Player, totalPlayerLvl, pList, tile);
                    }
                    else if (totalEnemyLvl > 0)
                    {
                        ApplyExpansion(Owner.Enemy, totalEnemyLvl, eList, tile);
                    }
                }
            }

            // 4. 모든 처리 완료 후 요청 리스트 비우기
            _playerRequests.Clear();
            _enemyRequests.Clear();
            RefreshExpansionIntentPreviews();
        }

        #endregion

        #region Public API — Removal

        /// <summary>
        /// 이미터를 맵에서 철거 소멸시키고, 본인이 가졌던 모든 종속 타일 연결 관계를 말소 청소합니다.
        /// (다른 살아있는 우군 이미터가 교차 지배하고 있지 않은 경우 해당 영토 타일들은 즉시 중립 무주지로 소거 환원됩니다)
        /// </summary>
        /// <param name="emitter">제거할 이미터.</param>
        public void RemoveEmitter(Emitter emitter)
        {
            if (emitter == null)
            {
                Debug.LogWarning("[EmitterManager] null인 이미터 소거 처리는 무시됩니다.");
                return;
            }

            if (emitter.IsCore)
            {
                Debug.LogWarning("[EmitterManager] 플레이어 메인 코어는 일반 제거 처리 대상이 아닙니다.");
                return;
            }

            GridManager grid = GridManager.Instance;

            // 이미터가 지배해왔던 영역 전수 해제
            foreach (Vector2Int tilePos in emitter.OwnedTiles)
            {
                TileData tile = grid?.GetTile(tilePos);
                if (tile != null)
                {
                    tile.ParentEmitters.Remove(emitter);

                    // 다른 교차 지배 우군 이미터가 더 이상 없는 영토이면서 소유권이 본 세력 소유였던 타일은 중립화
                    if (tile.ParentEmitters.Count == 0 && tile.Owner == emitter.Owner)
                    {
                        tile.ClearOwnership();
                    }
                    else if (tile.Owner == emitter.Owner)
                    {
                        // 남은 부모가 존재할 경우 레벨 재계산 (레벨 강등 여부 판단)
                        tile.RecalculateLevel();
                    }
                }
            }

            emitter.OwnedTiles.Clear();

            // 설치되어 있던 원점 타일의 이미터 정보 제거
            TileData emitterTile = grid?.GetTile(emitter.Position);
            if (emitterTile != null && emitterTile.Emitter == emitter)
            {
                emitterTile.Emitter = null;
                emitterTile.ParentEmitters.Remove(emitter);

                if (emitterTile.ParentEmitters.Count == 0 && emitterTile.Owner == emitter.Owner)
                {
                    emitterTile.ClearOwnership();
                }
                else if (emitterTile.Owner == emitter.Owner)
                {
                    emitterTile.RecalculateLevel();
                }
            }

            // 시스템 관리 등록 해제
            _emittersByPosition.Remove(emitter.Position);

            switch (emitter.Owner)
            {
                case Owner.Player:
                    _playerEmitters.Remove(emitter);
                    break;
                case Owner.Enemy:
                    _enemyEmitters.Remove(emitter);
                    break;
            }

            Debug.Log($"[EmitterManager] 이미터 제거 철거 완료: {emitter}");
            GameEvents.RaiseResourceChanged();
        }

        #endregion

        #region Private Helpers

        public void SeedStartingArea(Emitter emitter, int radius = 2)
        {
            if (emitter == null || !GridManager.HasInstance)
            {
                return;
            }

            GridManager grid = GridManager.Instance;
            foreach (TileData tile in grid.GetTilesInRadius(emitter.Position, Mathf.Max(0, radius)))
            {
                if (tile == null ||
                    (tile.IsOccupiedByEmitter && tile.Emitter != emitter) ||
                    (tile.Owner != Owner.Neutral && tile.Owner != emitter.Owner))
                {
                    continue;
                }

                tile.SetOwnership(emitter.Owner, 1, emitter);
                if (tile.Position != emitter.Position && !emitter.OwnedTiles.Contains(tile.Position))
                {
                    emitter.OwnedTiles.Add(tile.Position);
                }
            }
        }

        /// <summary>
        /// 단일 이미터를 분석하여 설정 템플릿 방향별 프론티어 영역으로의 다음 턴 확장 요청을 등록합니다.
        /// </summary>
        private void CollectExpansionRequests(Emitter emitter, GridManager grid)
        {
            List<Vector2Int> directions = emitter.GetExpansionDirections();

            foreach (Vector2Int dir in directions)
            {
                NormalizeFrontier(emitter, grid, dir);

                // 이미터의 최대 범위 검사
                int currentDistance = emitter.ExpansionFrontier.ContainsKey(dir) ? emitter.ExpansionFrontier[dir] : 0;

                int maxRange = 0;
                if (GameManager.HasInstance && GameManager.Instance.Config != null)
                {
                    var setting = GameManager.Instance.Config.GetEmitterSetting(emitter.Direction);
                    if (setting != null)
                    {
                        maxRange = setting.maxRange;
                    }
                }

                if (maxRange > 0 && currentDistance >= maxRange)
                {
                    continue;
                }

                Vector2Int nextPos = emitter.GetNextExpansionTile(dir);

                // 그리드 경계 검사
                if (!grid.IsInBounds(nextPos))
                {
                    continue;
                }

                TileData targetTile = grid.GetTile(nextPos);
                if (targetTile == null)
                {
                    continue;
                }

                // 출발지 타일의 영역 레벨을 계산하여 확장 세기 전파
                Vector2Int sourcePos = emitter.Position + dir * currentDistance;
                TileData sourceTile = grid.GetTile(sourcePos);

                if (!CanEmitterExpandFrom(emitter, sourceTile))
                {
                    continue;
                }

                int requestLevel = emitter.Level;
                ExpansionRequest req = new ExpansionRequest(emitter, dir, nextPos, requestLevel);
                if (emitter.Owner == Owner.Player)
                {
                    _playerRequests.Add(req);
                }
                else
                {
                    _enemyRequests.Add(req);
                }
            }
        }

        /// <summary>
        /// 충돌 해결 판정 완료 후 성공한 요청 이미터들에 대해 실질적으로 영역 점유 갱신 처리를 진행합니다.
        /// </summary>
        private void ApplyExpansion(Owner owner, int level, List<ExpansionRequest> successfulRequests, TileData tile)
        {
            if (successfulRequests != null &&
                successfulRequests.Count > 0 &&
                ResourceManager.HasInstance &&
                !ResourceManager.Instance.SpendExpansionPower(owner))
            {
                return;
            }

            // 타 세력 영토를 탈취 침식하는 경우, 해당 타일에 물려 있던 기존 진영 이미터 지배 관계를 모두 제거
            if (tile.Owner != Owner.Neutral && tile.Owner != owner)
            {
                // 타일에 상대 진영의 이미터가 배치되어 있다면 파괴 처리
                if (tile.IsOccupiedByEmitter && tile.Emitter.Owner != owner)
                {
                    if (tile.Emitter.IsCore)
                    {
                        if (GameManager.HasInstance)
                        {
                            GameManager.Instance.SetGameState(GameState.Defeat);
                        }
                        return;
                    }

                    RemoveEmitter(tile.Emitter);
                }

                List<Emitter> previousParents = new List<Emitter>(tile.ParentEmitters);
                foreach (Emitter parentEmitter in previousParents)
                {
                    parentEmitter.OwnedTiles.Remove(tile.Position);
                }
                tile.ParentEmitters.Clear();
            }

            if (successfulRequests == null || successfulRequests.Count == 0)
            {
                // 신규 확장 요청은 없으나 기존 활성 상태가 유지되는 경우 (예: 동일 레벨 침입 교착 방어 성공 시)
                tile.SetOwnership(owner, level, null);
            }
            else
            {
                bool isFirst = true;
                foreach (var req in successfulRequests)
                {
                    // 소유권 설정 및 변경 통지는 최초 1회만 수행
                    if (isFirst)
                    {
                        tile.SetOwnership(owner, level, req.Emitter);
                        isFirst = false;
                    }
                    else
                    {
                        if (req.Emitter != null)
                        {
                            tile.ParentEmitters.Add(req.Emitter);
                        }
                    }

                    if (req.Emitter != null)
                    {
                        if (!req.Emitter.OwnedTiles.Contains(tile.Position))
                        {
                            req.Emitter.OwnedTiles.Add(tile.Position);
                        }

                        req.Emitter.AdvanceFrontier(req.Direction);
                    }
                }
            }

            // 레벨 승격 여부 판단을 위해 영역 레벨 재계산 수행
            tile.RecalculateLevel();
        }

        /// <summary>
        /// 같은 편 기존 영향력과 이번 턴 확장 요청을 합쳐 실제 충돌 레벨을 계산합니다.
        /// </summary>
        private static int CalculateInfluenceLevel(
            TileData tile,
            Owner owner,
            List<ExpansionRequest> requests)
        {
            HashSet<Emitter> contributors = new HashSet<Emitter>();
            int levelSum = 0;

            if (tile.Owner == owner)
            {
                foreach (Emitter parent in tile.ParentEmitters)
                {
                    AddInfluence(parent, owner, parent != null ? parent.Level : 0, contributors, ref levelSum);
                }

                if (tile.Emitter != null)
                {
                    AddInfluence(tile.Emitter, owner, tile.Emitter.Level, contributors, ref levelSum);
                }
            }

            if (requests != null)
            {
                foreach (ExpansionRequest request in requests)
                {
                    AddInfluence(request.Emitter, owner, request.Level, contributors, ref levelSum);
                }
            }

            if (contributors.Count == 0)
            {
                return 0;
            }

            return Mathf.Clamp(levelSum, 1, 5);
        }

        private static void AddInfluence(
            Emitter emitter,
            Owner owner,
            int level,
            HashSet<Emitter> contributors,
            ref int levelSum)
        {
            if (emitter == null || emitter.Owner != owner)
            {
                return;
            }

            if (contributors.Add(emitter))
            {
                levelSum += level;
            }
        }

        /// <summary>
        /// 끊기거나 탈취된 프론티어를 현재 연결된 마지막 타일까지 되감습니다.
        /// </summary>
        private static void NormalizeFrontier(Emitter emitter, GridManager grid, Vector2Int direction)
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
                if (tile == null || tile.Owner != emitter.Owner || !tile.ParentEmitters.Contains(emitter))
                {
                    break;
                }

                connectedDistance++;
            }

            emitter.ExpansionFrontier[direction] = connectedDistance;
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

        /// <summary>
        /// BFS를 통해 영역 타일에서 부모 이미터까지 동일 세력 영토로 연결된 물리적 경로가 존재하는지 판단합니다.
        /// </summary>
        private bool HasPathToEmitter(Vector2Int startPos, Emitter targetEmitter, GridManager grid)
        {
            if (targetEmitter == null)
                return false;

            if (startPos == targetEmitter.Position)
                return true;

            Owner owner = targetEmitter.Owner;
            TileData startTile = grid.GetTile(startPos);
            if (startTile == null || startTile.Owner != owner)
                return false;

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

            queue.Enqueue(startPos);
            visited.Add(startPos);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                List<TileData> neighbors = grid.GetNeighbors(current, includeDiagonals: false);

                foreach (TileData neighbor in neighbors)
                {
                    if (visited.Contains(neighbor.Position))
                        continue;

                    // 해당 부모 진원이 실제 지배 중인 같은 세력 타일만 연결 통로로 인정
                    if (neighbor.Owner == owner &&
                        (neighbor.Position == targetEmitter.Position ||
                         neighbor.ParentEmitters.Contains(targetEmitter)))
                    {
                        if (neighbor.Position == targetEmitter.Position)
                        {
                            return true;
                        }

                        queue.Enqueue(neighbor.Position);
                        visited.Add(neighbor.Position);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 종속성 끊김에 의한 영토 소멸 및 기반 유실로 인한 이미터 파괴를 코루틴 기반으로 순차 연출하며 수행합니다.
        /// </summary>
        public IEnumerator PerformDependencyCheckCoroutine()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) yield break;

            bool changed = true;
            int safetyCounter = 0;
            const int maxIterations = 100;
            float delaySeconds = 0.08f; // 순차 소멸 연출 속도

            while (changed && safetyCounter++ < maxIterations)
            {
                changed = false;
                List<Vector2Int> tilesToClear = new List<Vector2Int>();
                List<TileData> tilesToRecalculate = new List<TileData>();

                // 1단계: 모든 영역 타일의 연결성 확인
                List<TileData> allTiles = new List<TileData>(grid.AllTiles);
                foreach (TileData tile in allTiles)
                {
                    if (tile.Owner == Owner.Neutral)
                        continue;

                    // 부모 이미터가 0개인 좀비 타일 감지 시 강제 소멸 수집
                    if (tile.ParentEmitters.Count == 0)
                    {
                        tilesToClear.Add(tile.Position);
                        changed = true;
                        continue;
                    }

                    List<Emitter> disconnected = new List<Emitter>();
                    foreach (Emitter parent in tile.ParentEmitters)
                    {
                        if (!HasPathToEmitter(tile.Position, parent, grid))
                        {
                            disconnected.Add(parent);
                        }
                    }

                    if (disconnected.Count > 0)
                    {
                        foreach (Emitter parent in disconnected)
                        {
                            tile.ParentEmitters.Remove(parent);
                            parent.OwnedTiles.Remove(tile.Position);
                        }

                        if (tile.ParentEmitters.Count == 0)
                        {
                            tilesToClear.Add(tile.Position);
                        }
                        else
                        {
                            tilesToRecalculate.Add(tile);
                        }
                        changed = true;
                    }
                }

                // 끊겨서 부모를 잃은 타일들을 애니메이션 연출 딜레이를 주어 순차 중립화
                if (tilesToClear.Count > 0)
                {
                    foreach (Vector2Int pos in tilesToClear)
                    {
                        grid.ClearTileOwnership(pos);
                        yield return new WaitForSeconds(delaySeconds);
                    }
                }

                // 연결이 유지된 타일들의 레벨 재계산 (영역 강등 대응)
                foreach (TileData tile in tilesToRecalculate)
                {
                    tile.RecalculateLevel();
                }

                // 2단계: 기반 타일의 유실/강등으로 인한 상위 이미터 파괴 판정
                List<Emitter> activeEmitters = new List<Emitter>();
                activeEmitters.AddRange(GetEmitters(Owner.Player));
                activeEmitters.AddRange(GetEmitters(Owner.Enemy));

                List<Emitter> emittersToRemove = new List<Emitter>();
                foreach (Emitter emitter in activeEmitters)
                {
                    TileData baseTile = grid.GetTile(emitter.Position);
                    if (baseTile == null) continue;

                    baseTile.RecalculateLevel();

                    int baseSupportLevel = CalculateEmitterBaseSupportLevel(baseTile, emitter);
                    if (baseTile.Owner != emitter.Owner || baseSupportLevel < emitter.Level)
                    {
                        emittersToRemove.Add(emitter);
                    }
                }

                if (emittersToRemove.Count > 0)
                {
                    foreach (Emitter emitter in emittersToRemove)
                    {
                        Debug.Log($"[EmitterManager] 기반 유실로 인한 이미터 셧다운 파괴: {emitter}");
                        yield return StartCoroutine(RemoveEmitterAnimated(emitter, delaySeconds));
                    }
                    changed = true;
                }
            }

            // 3단계: 최종적으로 맵 상의 모든 타일 레벨 재계산 동기화 (ON/OFF 상태 및 강등 최종 반영)
            foreach (TileData tile in grid.AllTiles)
            {
                tile.RecalculateLevel();
            }

            RefreshExpansionIntentPreviews();
        }

        /// <summary>
        /// 기반이 끊긴 진원과 그 진원이 만든 영역을 바깥쪽부터 순차적으로 소멸시킵니다.
        /// </summary>
        private IEnumerator RemoveEmitterAnimated(Emitter emitter, float delaySeconds)
        {
            if (emitter == null)
            {
                yield break;
            }

            GridManager grid = GridManager.Instance;
            if (grid == null)
            {
                RemoveEmitter(emitter);
                yield break;
            }

            // 먼저 시스템 등록을 해제해 이후 종속성 검사/확장 수집에서 제거 대상 진원이 다시 쓰이지 않게 한다.
            _emittersByPosition.Remove(emitter.Position);
            switch (emitter.Owner)
            {
                case Owner.Player:
                    _playerEmitters.Remove(emitter);
                    break;
                case Owner.Enemy:
                    _enemyEmitters.Remove(emitter);
                    break;
            }

            List<Vector2Int> ownedTiles = new List<Vector2Int>(emitter.OwnedTiles);
            ownedTiles.Sort((a, b) =>
            {
                int distA = Mathf.Abs(a.x - emitter.Position.x) + Mathf.Abs(a.y - emitter.Position.y);
                int distB = Mathf.Abs(b.x - emitter.Position.x) + Mathf.Abs(b.y - emitter.Position.y);
                return distB.CompareTo(distA);
            });

            foreach (Vector2Int tilePos in ownedTiles)
            {
                TileData tile = grid.GetTile(tilePos);
                if (tile == null)
                {
                    continue;
                }

                tile.ParentEmitters.Remove(emitter);

                if (tile.ParentEmitters.Count == 0 && tile.Owner == emitter.Owner)
                {
                    tile.ClearOwnership();
                }
                else if (tile.Owner == emitter.Owner)
                {
                    tile.RecalculateLevel();
                }

                yield return new WaitForSeconds(delaySeconds);
            }

            emitter.OwnedTiles.Clear();

            TileData emitterTile = grid.GetTile(emitter.Position);
            if (emitterTile != null && emitterTile.Emitter == emitter)
            {
                emitterTile.Emitter = null;
                emitterTile.ParentEmitters.Remove(emitter);

                if (emitterTile.ParentEmitters.Count == 0 && emitterTile.Owner == emitter.Owner)
                {
                    emitterTile.ClearOwnership();
                }
                else if (emitterTile.Owner == emitter.Owner)
                {
                    emitterTile.RecalculateLevel();
                }

                yield return new WaitForSeconds(delaySeconds * 1.5f);
            }

            Debug.Log($"[EmitterManager] 이미터 연출 제거 완료: {emitter}");
        }

        /// <summary>
        /// 진원 타일의 실제 레벨은 고정하되, 상위 진원 유지에 필요한 기반 지원 레벨만 별도로 계산합니다.
        /// </summary>
        private static int CalculateEmitterBaseSupportLevel(TileData baseTile, Emitter emitter)
        {
            if (!RequiresBaseSupport(emitter))
            {
                return emitter != null ? emitter.Level : 0;
            }

            int levelSum = CollectBaseSupporters(baseTile, emitter, null);
            return levelSum > 0 ? Mathf.Clamp(levelSum, 1, 5) : 0;
        }

        public static bool HasStableBaseSupport(TileData baseTile, Emitter emitter)
        {
            return RequiresBaseSupport(emitter) &&
                   CalculateEmitterBaseSupportLevel(baseTile, emitter) >= emitter.Level;
        }

        public static List<Emitter> GetCriticalBaseSupportEmitters(TileData baseTile, Emitter emitter)
        {
            List<Emitter> supporters = new List<Emitter>();
            if (!RequiresBaseSupport(emitter))
            {
                return supporters;
            }

            int levelSum = CollectBaseSupporters(baseTile, emitter, supporters);
            if (levelSum < emitter.Level)
            {
                supporters.Clear();
                return supporters;
            }

            for (int i = supporters.Count - 1; i >= 0; i--)
            {
                if (levelSum - supporters[i].Level >= emitter.Level)
                {
                    supporters.RemoveAt(i);
                }
            }

            supporters.Sort(CompareSupportEmitterPosition);
            return supporters;
        }

        private static bool RequiresBaseSupport(Emitter emitter)
        {
            return emitter != null &&
                   emitter.Level > 1 &&
                   !emitter.IsCore &&
                   !emitter.IsInitialPreset &&
                   !emitter.BypassesBaseRequirement;
        }

        private static int CollectBaseSupporters(TileData baseTile, Emitter emitter, List<Emitter> supporters)
        {
            if (baseTile == null || emitter == null)
            {
                return 0;
            }

            int levelSum = 0;

            foreach (Emitter parent in baseTile.ParentEmitters)
            {
                if (parent == null ||
                    parent == emitter ||
                    parent.Owner != emitter.Owner)
                {
                    continue;
                }

                levelSum += parent.Level;
                if (supporters != null)
                {
                    supporters.Add(parent);
                }
            }

            return levelSum;
        }

        private static int CompareSupportEmitterPosition(Emitter a, Emitter b)
        {
            if (a == null && b == null)
            {
                return 0;
            }

            if (a == null)
            {
                return 1;
            }

            if (b == null)
            {
                return -1;
            }

            int xCompare = a.Position.x.CompareTo(b.Position.x);
            return xCompare != 0 ? xCompare : a.Position.y.CompareTo(b.Position.y);
        }

        private static void RefreshExpansionIntentPreviews()
        {
            if (GameManager.HasInstance && GameManager.Instance.Visualizer != null)
            {
                GameManager.Instance.Visualizer.RefreshExpansionIntentPreviews();
            }
        }

        public int GetPlayerNextTurnPowerLoad()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || !grid.IsInitialized) return 0;

            int load = 0;
            foreach (Emitter emitter in _playerEmitters)
            {
                if (!emitter.IsOn || emitter.IsCore) continue;

                switch (emitter.Direction)
                {
                    case EmitterDirection.Cross:
                        load += 3;
                        break;
                    case EmitterDirection.TShape:
                        load += 2;
                        break;
                    default:
                        load += 1;
                        break;
                }
            }
            return load;
        }

        private int GetConnectedDistance(Emitter emitter, GridManager grid, Vector2Int direction)
        {
            int connectedDistance = 0;
            while (true)
            {
                Vector2Int pos = emitter.Position + direction * (connectedDistance + 1);
                if (!grid.IsInBounds(pos)) break;

                TileData tile = grid.GetTile(pos);
                if (tile == null || tile.Owner != emitter.Owner || !tile.ParentEmitters.Contains(emitter)) break;

                connectedDistance++;
            }
            return connectedDistance;
        }

        #endregion
    }
}
