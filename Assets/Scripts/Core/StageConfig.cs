// ============================================================================
// 파일:    Core/StageConfig.cs
// 프로젝트: Project SEVERANCE
// 용도: 스테이지별 자원 노드 배치 데이터.
// ============================================================================

using UnityEngine;

namespace Severance
{
    [CreateAssetMenu(fileName = "Stage01", menuName = "Severance/StageConfig", order = 2)]
    public class StageConfig : ScriptableObject
    {
        [Header("자원 배치")]
        [SerializeField]
        private StageResourceNode[] resourceNodes = CreateDefaultResourceNodes();

        public StageResourceNode[] ResourceNodes => resourceNodes;
        public bool HasResourceNodes => resourceNodes != null && resourceNodes.Length > 0;

        [Header("적 진원 배치")]
        [SerializeField]
        private StageEnemyEmitterNode[] enemyEmitters = CreateDefaultEnemyEmitters();

        public StageEnemyEmitterNode[] EnemyEmitters => enemyEmitters;
        public bool HasEnemyEmitters => enemyEmitters != null && enemyEmitters.Length > 0;

        public static StageResourceNode[] CreateDefaultResourceNodes()
        {
            return new[]
            {
                // 초반 안전권: 코어 주변에서 기본 확장과 단방향 진원 비용을 회수한다.
                new StageResourceNode(new Vector2Int(2, 5), TileResourceType.PowerNode, 2),
                new StageResourceNode(new Vector2Int(5, 2), TileResourceType.PowerNode, 2),
                new StageResourceNode(new Vector2Int(3, 4), TileResourceType.VeinIron, 1),
                new StageResourceNode(new Vector2Int(4, 3), TileResourceType.VeinIron, 1),
                new StageResourceNode(new Vector2Int(6, 4), TileResourceType.VeinIron, 2),

                // 중반 전선: Cross/TShape 진원을 위한 구리와 유지 전력을 놓는다.
                new StageResourceNode(new Vector2Int(6, 6), TileResourceType.PowerNode, 3),
                new StageResourceNode(new Vector2Int(9, 6), TileResourceType.PowerNode, 3),
                new StageResourceNode(new Vector2Int(5, 8), TileResourceType.VeinCopper, 1),
                new StageResourceNode(new Vector2Int(8, 10), TileResourceType.VeinCopper, 2),
                new StageResourceNode(new Vector2Int(10, 8), TileResourceType.VeinCopper, 2),

                // 중앙 경쟁권: 고효율 구리와 전력 보급을 두고 적과 충돌하게 만든다.
                new StageResourceNode(new Vector2Int(7, 10), TileResourceType.PowerNode, 4),
                new StageResourceNode(new Vector2Int(11, 11), TileResourceType.PowerNode, 4),
                new StageResourceNode(new Vector2Int(12, 12), TileResourceType.VeinCopper, 3),
                new StageResourceNode(new Vector2Int(12, 13), TileResourceType.VeinCopper, 3),
                new StageResourceNode(new Vector2Int(13, 12), TileResourceType.VeinCopper, 3),

                // 적 전초권: 고효율 철/구리를 원하면 위험 지역을 밀어야 한다.
                new StageResourceNode(new Vector2Int(15, 8), TileResourceType.PowerNode, 4),
                new StageResourceNode(new Vector2Int(13, 15), TileResourceType.PowerNode, 4),
                new StageResourceNode(new Vector2Int(15, 12), TileResourceType.VeinIron, 4),
                new StageResourceNode(new Vector2Int(12, 15), TileResourceType.VeinCopper, 4),
                new StageResourceNode(new Vector2Int(16, 16), TileResourceType.VeinCopper, 4)
            };
        }

        public static StageEnemyEmitterNode[] CreateDefaultEnemyEmitters()
        {
            return new[]
            {
                // 플레이어 코어 반대편의 약한 개막 거점. 이후 적 AI가 길을 뻗고 전진 기지를 세운다.
                new StageEnemyEmitterNode(new Vector2Int(17, 17), 1, EmitterDirection.Down)
            };
        }

        public void SaveData(StageResourceNode[] newResources, StageEnemyEmitterNode[] newEmitters)
        {
            resourceNodes = newResources;
            enemyEmitters = newEmitters;
        }

        public void ResetToDefault()
        {
            resourceNodes = CreateDefaultResourceNodes();
            enemyEmitters = CreateDefaultEnemyEmitters();
        }
    }

    [System.Serializable]
    public struct StageEnemyEmitterNode
    {
        public Vector2Int position;
        public int level;
        public EmitterDirection direction;

        public StageEnemyEmitterNode(Vector2Int position, int level, EmitterDirection direction)
        {
            this.position = position;
            this.level = level;
            this.direction = direction;
        }
    }
}
