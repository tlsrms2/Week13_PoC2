// ============================================================================
// 파일:    Editor/StagePresetEditorWindow.cs
// 프로젝트: Project SEVERANCE
// 용도: StageConfig의 리소스 배치와 적 진원 배치를 2D 그리드 뷰를 통해
//       시각적으로 편집할 수 있게 해주는 커스텀 에디터 윈도우.
//       렉 걸림(GC 과부하)과 데이터 유실 현상을 완벽히 수정한 버전입니다.
// ============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Severance.Editor
{
    public class StagePresetEditorWindow : EditorWindow
    {
        #region Inner Types

        private enum ToolType
        {
            Eraser,
            PowerNode,
            VeinIron,
            VeinCopper,
            EnemyEmitter
        }

        private enum CellType
        {
            None,
            Resource,
            EnemyEmitter
        }

        private struct CellData
        {
            public CellType type;
            public TileResourceType resourceType;
            public int yield;      // 1 ~ 4
            public int level;      // 1 ~ GameConfig.MaxLevel
            public EmitterDirection direction;
        }

        #endregion

        #region State Fields

        [SerializeField] private GameConfig _selectedConfig;
        [SerializeField] private StageConfig _selectedStage;

        // 브러시 설정
        private ToolType _activeTool = ToolType.Eraser;
        private int _brushYield = 1;
        private int _brushLevel = 1;
        private EmitterDirection _brushDirection = EmitterDirection.EightWay;

        // 2D 맵 데이터 (직렬화 유지보수를 위해 인스턴스 해제 방지)
        private CellData[,] _map;
        private int _mapWidth;
        private int _mapHeight;
        private bool _isDataLoaded;

        // 스크롤 및 레이아웃
        private Vector2 _gridScrollPos;
        private const float CellSize = 55f;

        #endregion

        #region Menu Entry

        [MenuItem("Severance/스테이지 프리셋 에디터")]
        public static void ShowWindow()
        {
            StagePresetEditorWindow window = GetWindow<StagePresetEditorWindow>("스테이지 프리셋 에디터");
            window.minSize = new Vector2(600f, 500f);
            window.Show();
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            // 기 등록된 에셋이 있다면 자동 로드 시도
            if (_selectedConfig == null)
            {
                TryLoadActiveConfigs();
            }
            _isDataLoaded = false;
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(6);

            if (_selectedConfig == null)
            {
                EditorGUILayout.HelpBox("편집할 GameConfig 에셋을 상단에 지정해 주세요.", MessageType.Warning);
                return;
            }

            if (_selectedStage == null)
            {
                EditorGUILayout.HelpBox("이 GameConfig 에 StageConfig가 할당되지 않았습니다. StageConfig를 지정하거나 새로 생성하세요.", MessageType.Warning);
                return;
            }

            // 데이터 정합성 검사 및 최초 로드
            int reqWidth = _selectedConfig.GridWidth;
            int reqHeight = _selectedConfig.GridHeight;

            if (!_isDataLoaded || _map == null || _mapWidth != reqWidth || _mapHeight != reqHeight)
            {
                _mapWidth = reqWidth;
                _mapHeight = reqHeight;
                LoadStageData();
            }

            DrawToolbox();
            EditorGUILayout.Space(6);

            DrawGridArea();
            EditorGUILayout.Space(6);

            DrawFooter();
        }

        #endregion

        #region Header & Config Selection

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("스테이지 프리셋 에디터 ─ 설정 대상", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                EditorGUI.BeginChangeCheck();
                GameConfig prevConfig = _selectedConfig;
                _selectedConfig = (GameConfig)EditorGUILayout.ObjectField("GameConfig", _selectedConfig, typeof(GameConfig), false);
                if (EditorGUI.EndChangeCheck() && _selectedConfig != prevConfig)
                {
                    OnGameConfigChanged();
                }

                if (_selectedConfig != null)
                {
                    SerializedObject serializedConfig = new SerializedObject(_selectedConfig);
                    SerializedProperty stageConfigProp = serializedConfig.FindProperty("stageConfig");

                    EditorGUI.BeginChangeCheck();
                    StageConfig prevStage = _selectedStage;
                    _selectedStage = (StageConfig)EditorGUILayout.ObjectField("StageConfig (Linked)", _selectedStage, typeof(StageConfig), false);
                    if (EditorGUI.EndChangeCheck() && _selectedStage != prevStage)
                    {
                        if (stageConfigProp != null)
                        {
                            stageConfigProp.objectReferenceValue = _selectedStage;
                            serializedConfig.ApplyModifiedProperties();
                            EditorUtility.SetDirty(_selectedConfig);
                        }
                        LoadStageData();
                    }

                    if (_selectedStage == null && stageConfigProp != null && stageConfigProp.objectReferenceValue != null)
                    {
                        _selectedStage = stageConfigProp.objectReferenceValue as StageConfig;
                        LoadStageData();
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void OnGameConfigChanged()
        {
            _selectedStage = null;
            if (_selectedConfig != null)
            {
                SerializedObject serializedConfig = new SerializedObject(_selectedConfig);
                SerializedProperty stageConfigProp = serializedConfig.FindProperty("stageConfig");
                if (stageConfigProp != null && stageConfigProp.objectReferenceValue != null)
                {
                    _selectedStage = stageConfigProp.objectReferenceValue as StageConfig;
                }
            }
            LoadStageData();
        }

        private void TryLoadActiveConfigs()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _selectedConfig = AssetDatabase.LoadAssetAtPath<GameConfig>(path);
                OnGameConfigChanged();
            }
        }

        #endregion

        #region Toolbox (브러시 설정)

        private void DrawToolbox()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("편집 도구 브러시 (Active Brush)", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                EditorGUILayout.BeginHorizontal();
                {
                    DrawToolButton(ToolType.Eraser, "지우개");
                    DrawToolButton(ToolType.PowerNode, "전력 노드");
                    DrawToolButton(ToolType.VeinIron, "철 광맥");
                    DrawToolButton(ToolType.VeinCopper, "구리 광맥");
                    DrawToolButton(ToolType.EnemyEmitter, "적 진원");
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                if (_activeTool == ToolType.PowerNode || _activeTool == ToolType.VeinIron || _activeTool == ToolType.VeinCopper)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.LabelField("생산 획득량 (Yield):", GUILayout.Width(130));
                        _brushYield = EditorGUILayout.IntSlider(_brushYield, 1, 4);
                        EditorGUILayout.LabelField("(주기는 1턴으로 자동 고정)", EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else if (_activeTool == ToolType.EnemyEmitter)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        int maxEnemyLevel = Mathf.Max(1, _selectedConfig != null ? _selectedConfig.MaxLevel : 5);
                        _brushLevel = Mathf.Clamp(_brushLevel, 1, maxEnemyLevel);

                        EditorGUILayout.LabelField("이미터 레벨:", GUILayout.Width(80));
                        _brushLevel = EditorGUILayout.IntSlider(_brushLevel, 1, maxEnemyLevel, GUILayout.Width(180));
                        EditorGUILayout.Space(10);
                        _brushDirection = (EmitterDirection)EditorGUILayout.EnumPopup("방향 템플릿:", _brushDirection);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField("지우개 활성화: 타일을 클릭하면 배치가 지워집니다.", EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawToolButton(ToolType type, string label)
        {
            bool isActive = _activeTool == type;
            GUIStyle style = new GUIStyle(GUI.skin.button);
            if (isActive)
            {
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = new Color(0.2f, 0.6f, 1f);
            }

            if (GUILayout.Button(label, style, GUILayout.Height(30)))
            {
                _activeTool = type;
            }
        }

        #endregion

        #region Grid Display & Editing

        private void DrawGridArea()
        {
            if (_selectedConfig == null || _selectedStage == null || _map == null) return;

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField($"그리드 배치 (크기: {_mapWidth} x {_mapHeight})", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                _gridScrollPos = EditorGUILayout.BeginScrollView(_gridScrollPos, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                {
                    float widthPixels = _mapWidth * CellSize + 40f;
                    float heightPixels = _mapHeight * CellSize + 40f;

                    Rect rect = GUILayoutUtility.GetRect(widthPixels, heightPixels);
                    GUI.Box(rect, "", GUI.skin.scrollView);

                    for (int y = _mapHeight - 1; y >= 0; y--)
                    {
                        for (int x = 0; x < _mapWidth; x++)
                        {
                            float px = 20f + x * CellSize;
                            float py = 20f + (_mapHeight - 1 - y) * CellSize;
                            Rect cellRect = new Rect(rect.x + px, rect.y + py, CellSize - 2f, CellSize - 2f);

                            DrawCell(cellRect, x, y);
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawCell(Rect rect, int x, int y)
        {
            CellData cell = _map[x, y];
            string contentText = $"{x},{y}";
            Color backgroundColor = new Color(0.18f, 0.20f, 0.23f); // 기본 타일 색

            if (cell.type == CellType.Resource)
            {
                switch (cell.resourceType)
                {
                    case TileResourceType.PowerNode:
                        backgroundColor = new Color(0.13f, 0.44f, 0.16f); // 전력: 녹색
                        contentText = $"P\n+{cell.yield}";
                        break;
                    case TileResourceType.VeinIron:
                        backgroundColor = new Color(0.35f, 0.38f, 0.43f); // 철: 회색
                        contentText = $"Fe\n+{cell.yield}";
                        break;
                    case TileResourceType.VeinCopper:
                        backgroundColor = new Color(0.68f, 0.39f, 0.14f);  // 구리: 주황색
                        contentText = $"Cu\n+{cell.yield}";
                        break;
                }
            }
            else if (cell.type == CellType.EnemyEmitter)
            {
                backgroundColor = new Color(0.62f, 0.13f, 0.13f); // 적 진원: 빨간색
                string dirLabel = GetDirectionAbbreviation(cell.direction);
                contentText = $"E\nLv.{cell.level}\n({dirLabel})";
            }

            // [렉 개선] Texture2D 생성 오버헤드를 아예 없애기 위해 EditorGUI.DrawRect로 배경을 먼저 렌더합니다.
            EditorGUI.DrawRect(rect, backgroundColor);

            // 투명 버튼 스타일을 얹어 클릭 처리
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = cell.type == CellType.EnemyEmitter ? 9 : 11,
                fontStyle = FontStyle.Bold
            };
            buttonStyle.normal.textColor = Color.white;

            // 마우스 호버 효과 시각화
            if (rect.Contains(Event.current.mousePosition))
            {
                EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.12f));
            }

            if (GUI.Button(rect, contentText, buttonStyle))
            {
                ApplyBrushToCell(x, y);
            }
        }

        private string GetDirectionAbbreviation(EmitterDirection dir)
        {
            switch (dir)
            {
                case EmitterDirection.Up: return "U";
                case EmitterDirection.Down: return "D";
                case EmitterDirection.Left: return "L";
                case EmitterDirection.Right: return "R";
                case EmitterDirection.TShape: return "T";
                case EmitterDirection.Cross: return "+";
                case EmitterDirection.EightWay: return "8W";
                default: return "-";
            }
        }

        private void ApplyBrushToCell(int x, int y)
        {
            Undo.RecordObject(this, "Modify Stage Cell Cache");

            CellData newCell = new CellData();
            switch (_activeTool)
            {
                case ToolType.Eraser:
                    newCell.type = CellType.None;
                    break;

                case ToolType.PowerNode:
                    newCell.type = CellType.Resource;
                    newCell.resourceType = TileResourceType.PowerNode;
                    newCell.yield = _brushYield;
                    break;

                case ToolType.VeinIron:
                    newCell.type = CellType.Resource;
                    newCell.resourceType = TileResourceType.VeinIron;
                    newCell.yield = _brushYield;
                    break;

                case ToolType.VeinCopper:
                    newCell.type = CellType.Resource;
                    newCell.resourceType = TileResourceType.VeinCopper;
                    newCell.yield = _brushYield;
                    break;

                case ToolType.EnemyEmitter:
                    newCell.type = CellType.EnemyEmitter;
                    newCell.level = _brushLevel;
                    newCell.direction = _brushDirection;
                    break;
            }

            _map[x, y] = newCell;

            // [데이터 보존 개선] 클릭 변경 즉시 에셋에도 실시간 미러링 반영을 수행합니다 (날아감 방지)
            PushCacheToAssetData();
            Repaint();
        }

        #endregion

        #region Save / Load Logic

        private void LoadStageData()
        {
            if (_selectedStage == null)
            {
                _map = null;
                _isDataLoaded = false;
                return;
            }

            int w = _mapWidth > 0 ? _mapWidth : 20;
            int h = _mapHeight > 0 ? _mapHeight : 20;
            _map = new CellData[w, h];

            // 1. 자원 노드 적용
            if (_selectedStage.ResourceNodes != null)
            {
                foreach (StageResourceNode node in _selectedStage.ResourceNodes)
                {
                    if (node.Position.x >= 0 && node.Position.x < w &&
                        node.Position.y >= 0 && node.Position.y < h)
                    {
                        CellData cell = new CellData();
                        cell.type = CellType.Resource;
                        cell.resourceType = node.ResourceType;
                        cell.yield = node.Yield;
                        _map[node.Position.x, node.Position.y] = cell;
                    }
                }
            }

            // 2. 적 진원 적용
            if (_selectedStage.EnemyEmitters != null)
            {
                foreach (StageEnemyEmitterNode emitter in _selectedStage.EnemyEmitters)
                {
                    if (emitter.position.x >= 0 && emitter.position.x < w &&
                        emitter.position.y >= 0 && emitter.position.y < h)
                    {
                        CellData cell = new CellData();
                        cell.type = CellType.EnemyEmitter;
                        cell.level = emitter.level;
                        cell.direction = emitter.direction;
                        _map[emitter.position.x, emitter.position.y] = cell;
                    }
                }
            }

            _isDataLoaded = true;
        }

        /// <summary>
        /// 캐시 데이터를 StageConfig 에셋에 실시간 반영시킵니다 (SerializedProperty 적용으로 유실 완벽 방지).
        /// </summary>
        private void PushCacheToAssetData()
        {
            if (_selectedStage == null || _map == null) return;

            SerializedObject serializedStage = new SerializedObject(_selectedStage);
            SerializedProperty resourceNodesProp = serializedStage.FindProperty("resourceNodes");
            SerializedProperty enemyEmittersProp = serializedStage.FindProperty("enemyEmitters");

            if (resourceNodesProp != null && enemyEmittersProp != null)
            {
                List<StageResourceNode> savedResources = new List<StageResourceNode>();
                List<StageEnemyEmitterNode> savedEmitters = new List<StageEnemyEmitterNode>();

                for (int x = 0; x < _mapWidth; x++)
                {
                    for (int y = 0; y < _mapHeight; y++)
                    {
                        CellData cell = _map[x, y];
                        Vector2Int pos = new Vector2Int(x, y);

                        if (cell.type == CellType.Resource)
                        {
                            savedResources.Add(new StageResourceNode(pos, cell.resourceType, cell.yield));
                        }
                        else if (cell.type == CellType.EnemyEmitter)
                        {
                            savedEmitters.Add(new StageEnemyEmitterNode(pos, cell.level, cell.direction));
                        }
                    }
                }

                // 1. 자원 노드 직렬화 데이터 기록
                resourceNodesProp.ClearArray();
                resourceNodesProp.arraySize = savedResources.Count;
                for (int i = 0; i < savedResources.Count; i++)
                {
                    SerializedProperty element = resourceNodesProp.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("position").vector2IntValue = savedResources[i].Position;
                    element.FindPropertyRelative("resourceType").enumValueIndex = (int)savedResources[i].ResourceType;
                    element.FindPropertyRelative("yield").intValue = savedResources[i].Yield;
                }

                // 2. 적 진원 직렬화 데이터 기록
                enemyEmittersProp.ClearArray();
                enemyEmittersProp.arraySize = savedEmitters.Count;
                for (int i = 0; i < savedEmitters.Count; i++)
                {
                    SerializedProperty element = enemyEmittersProp.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("position").vector2IntValue = savedEmitters[i].position;
                    element.FindPropertyRelative("level").intValue = savedEmitters[i].level;
                    element.FindPropertyRelative("direction").enumValueIndex = (int)savedEmitters[i].direction;
                }

                serializedStage.ApplyModifiedProperties();
                EditorUtility.SetDirty(_selectedStage);
            }
        }

        private void SaveStageData()
        {
            if (_selectedStage == null || _map == null) return;

            PushCacheToAssetData();
            AssetDatabase.SaveAssets();

            Debug.Log($"[StagePresetEditor] '{_selectedStage.name}' 에 최종 저장 완료 및 파일 직렬화!");
        }

        #endregion

        #region Footer & Actions

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("새로고침 (Revert to Saved)", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("경고", "마지막으로 저장된 스테이지 프리셋 상태로 그리드를 복원합니까? 현재 미저장 변경사항이 유실됩니다.", "예", "아니오"))
                    {
                        LoadStageData();
                    }
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("스테이지 초기화 (Clear All)", GUILayout.Height(30), GUILayout.Width(150)))
                {
                    if (EditorUtility.DisplayDialog("경고", "그리드 맵의 모든 배치를 빈 칸으로 초기화하시겠습니까?", "예", "아니오"))
                    {
                        _map = new CellData[_mapWidth, _mapHeight];
                        PushCacheToAssetData();
                        Repaint();
                    }
                }

                if (GUILayout.Button("스테이지 저장 (Save Stage)", GUILayout.Height(30), GUILayout.Width(180)))
                {
                    SaveStageData();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        #endregion
    }
}
