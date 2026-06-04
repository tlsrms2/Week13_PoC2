// ============================================================================
// 파일:    Grid/GridManager.cs
// 프로젝트: Project SEVERANCE
// 용도: 씬 상의 2D 타일 그리드 데이터를 총괄하는 싱글톤 매니저.
//          모든 타일 갱신 및 상태 읽기를 위한 공간 쿼리(인접, 범위 검색) API 제공.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Severance
{
    /// <summary>
    /// <see cref="TileData"/> 정보 배열을 소유하고 제공하는 논리 그리드 데이터 싱글톤 클래스입니다.
    /// 타일들의 상태 조회 및 직접적인 좌표 연산 헬퍼를 지원합니다.
    /// </summary>
    public class GridManager : Singleton<GridManager>
    {
        #region Fields

        /// <summary>가로/세로 [x, y] 인덱스로 구성된 타일 데이터 2D 배열.</summary>
        private TileData[,] _grid;

        /// <summary>가로 크기 캐시 필드.</summary>
        private int _width;

        /// <summary>세로 크기 캐시 필드.</summary>
        private int _height;

        /// <summary>
        /// 상, 하, 좌, 우 4방향 카드 방향 오프셋 데이터.
        /// </summary>
        private static readonly Vector2Int[] CardinalOffsets =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        /// <summary>
        /// 대각선 4방향 오프셋 데이터.
        /// </summary>
        private static readonly Vector2Int[] DiagonalOffsets =
        {
            new Vector2Int(-1,  1),  // 좌측 상단
            new Vector2Int( 1,  1),  // 우측 상단
            new Vector2Int(-1, -1),  // 좌측 하단
            new Vector2Int( 1, -1)   // 우측 하단
        };

        #endregion

        #region Properties

        /// <summary>그리드 가로 타일 개수.</summary>
        public int Width => _width;

        /// <summary>그리드 세로 타일 개수.</summary>
        public int Height => _height;

        /// <summary>그리드가 정상적으로 초기화 및 할당되었는지 여부.</summary>
        public bool IsInitialized => _grid != null;

        /// <summary>
        /// 그리드 상의 모든 타일을 행 단위(좌측 하단에서 우측 상단 순서)로 순회 가능한 열거자.
        /// </summary>
        public IEnumerable<TileData> AllTiles
        {
            get
            {
                if (_grid == null) yield break;

                for (int y = 0; y < _height; y++)
                {
                    for (int x = 0; x < _width; x++)
                    {
                        yield return _grid[x, y];
                    }
                }
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 지정된 크기 규격의 타일 그리드 공간을 생성하고 할당합니다. 중복 호출 시 이전 데이터는 삭제됩니다.
        /// </summary>
        /// <param name="width">그리드 가로 크기 (열).</param>
        /// <param name="height">그리드 세로 크기 (행).</param>
        public void Initialize(int width, int height)
        {
            Debug.Assert(width > 0 && height > 0,
                $"[GridManager] 잘못된 그리드 스펙: {width}x{height}");

            _width = width;
            _height = height;
            _grid = new TileData[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    _grid[x, y] = new TileData(new Vector2Int(x, y));
                }
            }

            Debug.Log($"[GridManager] 논리 그리드 초기화 완료: {width}x{height} (총 {width * height}개 타일).");
        }

        #endregion

        #region Tile Access

        /// <summary>
        /// 지정된 그리드 좌표 상의 타일 데이터를 반환하며, 그리드 범위 밖인 경우 null을 반환합니다.
        /// </summary>
        /// <param name="pos">그리드 2D 좌표.</param>
        /// <returns>타일 데이터 인스턴스 또는 null.</returns>
        public TileData GetTile(Vector2Int pos)
        {
            return GetTile(pos.x, pos.y);
        }

        /// <summary>
        /// x, y 값을 직접 입력하여 그리드 안의 타일 정보를 가져오며, 범위 밖이면 null을 반환합니다.
        /// </summary>
        /// <param name="x">열 인덱스.</param>
        /// <param name="y">행 인덱스.</param>
        /// <returns>타일 데이터 인스턴스 또는 null.</returns>
        public TileData GetTile(int x, int y)
        {
            if (!IsInBounds(x, y))
            {
                return null;
            }

            return _grid[x, y];
        }

        /// <summary>
        /// 지정된 좌표가 그리드 유효 한계 경계 내에 위치하는지 검사합니다.
        /// </summary>
        /// <param name="pos">그리드 2D 좌표.</param>
        /// <returns>경계 내부에 위치하면 true, 바깥이면 false.</returns>
        public bool IsInBounds(Vector2Int pos)
        {
            return IsInBounds(pos.x, pos.y);
        }

        /// <summary>
        /// 직접 x, y 인덱스 값을 입력하여 그리드 유효 한계 경계 내부인지 검사합니다.
        /// </summary>
        /// <param name="x">열 인덱스.</param>
        /// <param name="y">행 인덱스.</param>
        /// <returns>경계 내부에 위치하면 true, 바깥이면 false.</returns>
        public bool IsInBounds(int x, int y)
        {
            return x >= 0 && x < _width && y >= 0 && y < _height;
        }

        #endregion

        #region Spatial Queries

        /// <summary>
        /// 타일의 인접 타일 데이터를 리스트 형태로 수집합니다 (4방향 또는 대각선 포함 8방향 선택 가능).
        /// </summary>
        /// <param name="pos">중심 타일 좌표.</param>
        /// <param name="includeDiagonals">true 입력 시 대각선 이웃 타일까지 포함 (8방향).</param>
        /// <returns>그리드 유효 범위 내부의 인접 타일 리스트.</returns>
        public List<TileData> GetNeighbors(Vector2Int pos, bool includeDiagonals = false)
        {
            var result = new List<TileData>(includeDiagonals ? 8 : 4);

            foreach (var offset in CardinalOffsets)
            {
                var neighbor = pos + offset;
                if (IsInBounds(neighbor))
                {
                    result.Add(_grid[neighbor.x, neighbor.y]);
                }
            }

            if (includeDiagonals)
            {
                foreach (var offset in DiagonalOffsets)
                {
                    var neighbor = pos + offset;
                    if (IsInBounds(neighbor))
                    {
                        result.Add(_grid[neighbor.x, neighbor.y]);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 체비쇼프(Chebyshev, 사각 격자 반경) 거리를 기준으로 중심 타일 주위의 모든 타일을 리스트로 가져옵니다 (중심 타일 포함).
        /// </summary>
        /// <param name="center">반경 탐색 중심 좌표.</param>
        /// <param name="radius">탐색 반경 타일 수 (0인 경우 중심 타일만 포함).</param>
        /// <returns>지정 범위 내의 타일 리스트.</returns>
        public List<TileData> GetTilesInRadius(Vector2Int center, int radius)
        {
            int side = 2 * radius + 1;
            var result = new List<TileData>(side * side);

            int xMin = Mathf.Max(0, center.x - radius);
            int xMax = Mathf.Min(_width - 1, center.x + radius);
            int yMin = Mathf.Max(0, center.y - radius);
            int yMax = Mathf.Min(_height - 1, center.y + radius);

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    result.Add(_grid[x, y]);
                }
            }

            return result;
        }

        #endregion

        #region Tile Mutation Helpers

        /// <summary>
        /// 특정 타일 정보가 수정되었음을 이벤트 버스를 통해 전파합니다.
        /// 외부에서 타일 데이터만 수정하고 비주얼 리프레시가 필요할 때 호출합니다.
        /// </summary>
        /// <param name="position">상태가 변한 타일의 그리드 좌표.</param>
        public void NotifyTileChanged(Vector2Int position)
        {
            GameEvents.RaiseTileChanged(position);
        }

        /// <summary>
        /// 타일의 세력 소유권을 변경하고, 관련 변경 이벤트 통지를 동시에 호출하는 편의 메서드입니다.
        /// </summary>
        /// <param name="position">수정 타일의 그리드 좌표.</param>
        /// <param name="owner">신규 소유 세력.</param>
        /// <param name="level">신규 타일 레벨.</param>
        /// <param name="sourceEmitter">소유권 주장을 초래한 원천 이미터 레퍼런스 (선택 사항).</param>
        public void SetTileOwnership(Vector2Int position, Owner owner, int level, Emitter sourceEmitter)
        {
            TileData tile = GetTile(position);
            if (tile == null)
            {
                Debug.LogWarning($"[GridManager] SetTileOwnership 대상이 그리드 경계를 벗어남: {position}");
                return;
            }

            tile.SetOwnership(owner, level, sourceEmitter);
        }

        /// <summary>
        /// 타일의 소유 정보를 즉시 중립 상태로 바꾸며 변경 통지 이벤트를 함께 쏘는 편의 메서드입니다.
        /// </summary>
        /// <param name="position">대상 타일 좌표.</param>
        public void ClearTileOwnership(Vector2Int position)
        {
            TileData tile = GetTile(position);
            if (tile == null)
            {
                Debug.LogWarning($"[GridManager] ClearTileOwnership 대상이 그리드 경계를 벗어남: {position}");
                return;
            }

            tile.ClearOwnership();
        }

        #endregion
    }
}
