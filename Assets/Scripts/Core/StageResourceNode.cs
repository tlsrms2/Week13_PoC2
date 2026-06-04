// ============================================================================
// 파일:    Core/StageResourceNode.cs
// 프로젝트: Project SEVERANCE
// 용도: 스테이지 자원 노드 배치 좌표 데이터.
// ============================================================================

using System;
using UnityEngine;

namespace Severance
{
    [Serializable]
    public struct StageResourceNode
    {
        [SerializeField] private Vector2Int position;
        [SerializeField] private TileResourceType resourceType;
        [SerializeField, Range(1, 4)] private int yield;

        public Vector2Int Position => position;
        public TileResourceType ResourceType => TileData.NormalizeResourceType(resourceType);
        public int Yield => Mathf.Clamp(yield <= 0 ? 1 : yield, 1, 4);

        public StageResourceNode(Vector2Int position, TileResourceType resourceType)
            : this(position, resourceType, 1)
        {
        }

        public StageResourceNode(Vector2Int position, TileResourceType resourceType, int yield)
        {
            this.position = position;
            this.resourceType = resourceType;
            this.yield = Mathf.Clamp(yield, 1, 4);
        }
    }
}
