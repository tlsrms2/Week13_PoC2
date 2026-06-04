// ============================================================================
// 파일:    Editor/LevelDesignerWindow.cs
// 프로젝트: Project SEVERANCE
// 용도: GameConfig 에셋을 직관적 탭 UI로 편집할 수 있는 커스텀 에디터 윈도우.
//       SerializedObject/SerializedProperty 패턴을 사용하여 Undo를 지원합니다.
// ============================================================================

using UnityEditor;
using UnityEngine;

namespace Severance.Editor
{
    /// <summary>
    /// Severance 레벨 디자이너 윈도우.
    /// 메뉴 Severance → 레벨 디자이너 에서 열 수 있습니다.
    /// </summary>
    public class LevelDesignerWindow : EditorWindow
    {
        #region Constants

        // 탭 정의
        private static readonly string[] TabLabels =
        {
            "그리드 설정",
            "자원 설정",
            "진원 비용 설정",
            "적 AI 설정",
            "전장의 안개",
            "스테이지 프리셋"
        };

        private const float TabWidth = 130f;
        private const float MinWindowWidth = 520f;
        private const float MinWindowHeight = 400f;

        #endregion

        #region State

        private GameConfig _selectedConfig;
        private SerializedObject _serializedConfig;
        private SerializedObject _serializedStage;

        private int _selectedTab;
        private Vector2 _tabScrollPos;
        private Vector2 _contentScrollPos;
        private Vector2 _stageResourceScrollPos;

        #endregion

        #region Menu Entry

        [MenuItem("Severance/레벨 디자이너")]
        public static void ShowWindow()
        {
            LevelDesignerWindow window = GetWindow<LevelDesignerWindow>("레벨 디자이너");
            window.minSize = new Vector2(MinWindowWidth, MinWindowHeight);
            window.Show();
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            // 이전에 선택된 에셋을 복원 시도
            if (_selectedConfig != null)
            {
                RebuildSerializedObject();
            }
        }

        private void OnGUI()
        {
            DrawConfigSelector();

            if (_selectedConfig == null)
            {
                EditorGUILayout.HelpBox("편집할 GameConfig 에셋을 선택하세요.", MessageType.Info);
                return;
            }

            // SerializedObject 유효성 체크
            if (_serializedConfig == null || _serializedConfig.targetObject == null)
            {
                RebuildSerializedObject();
            }

            _serializedConfig.Update();

            EditorGUILayout.Space(4);

            // 메인 레이아웃: 좌측 탭 바 + 우측 콘텐츠
            EditorGUILayout.BeginHorizontal();
            {
                DrawTabBar();
                DrawContent();
            }
            EditorGUILayout.EndHorizontal();

            // 변경 사항 적용
            if (_serializedConfig.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_selectedConfig);
            }
        }

        #endregion

        #region Config Selector (GameConfig 선택)

        private void DrawConfigSelector()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                EditorGUILayout.LabelField("GameConfig", GUILayout.Width(80));

                EditorGUI.BeginChangeCheck();
                GameConfig newConfig = (GameConfig)EditorGUILayout.ObjectField(
                    _selectedConfig, typeof(GameConfig), false);
                if (EditorGUI.EndChangeCheck() && newConfig != _selectedConfig)
                {
                    _selectedConfig = newConfig;
                    RebuildSerializedObject();
                }

                if (GUILayout.Button("새로 생성", GUILayout.Width(80)))
                {
                    CreateNewGameConfig();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void CreateNewGameConfig()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "GameConfig 에셋 생성",
                "NewGameConfig",
                "asset",
                "새 GameConfig 에셋을 저장할 위치를 선택하세요.");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            GameConfig instance = CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _selectedConfig = instance;
            RebuildSerializedObject();

            Debug.Log($"[LevelDesigner] GameConfig 에셋 생성 완료: {path}");
        }

        private void RebuildSerializedObject()
        {
            _serializedConfig = _selectedConfig != null
                ? new SerializedObject(_selectedConfig)
                : null;
            _serializedStage = null;
        }

        #endregion

        #region Tab Bar (좌측 탭 바)

        private void DrawTabBar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(TabWidth));
            {
                _tabScrollPos = EditorGUILayout.BeginScrollView(_tabScrollPos, GUILayout.Width(TabWidth));
                {
                    for (int i = 0; i < TabLabels.Length; i++)
                    {
                        bool isSelected = _selectedTab == i;
                        GUIStyle style = isSelected ? GetSelectedTabStyle() : EditorStyles.toolbarButton;
                        if (GUILayout.Button(TabLabels[i], style, GUILayout.Height(28)))
                        {
                            _selectedTab = i;
                            _contentScrollPos = Vector2.zero;
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        private static GUIStyle GetSelectedTabStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.2f, 0.6f, 1f) }
            };
            return style;
        }

        #endregion

        #region Content Area (우측 콘텐츠 패널)

        private void DrawContent()
        {
            EditorGUILayout.BeginVertical("box");
            {
                _contentScrollPos = EditorGUILayout.BeginScrollView(_contentScrollPos);
                {
                    switch (_selectedTab)
                    {
                        case 0:
                            DrawGridSettings();
                            break;
                        case 1:
                            DrawResourceSettings();
                            break;
                        case 2:
                            DrawEmitterCostSettings();
                            break;
                        case 3:
                            DrawEnemySettings();
                            break;
                        case 4:
                            DrawFogSettings();
                            break;
                        case 5:
                            DrawStagePresetSettings();
                            break;
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Tab 0: 그리드 설정

        private void DrawGridSettings()
        {
            EditorGUILayout.LabelField("그리드 설정", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawPropertyField("gridWidth", "가로 칸 수");
            DrawPropertyField("gridHeight", "세로 칸 수");
            DrawPropertyField("tileSize", "타일 크기");
            DrawPropertyField("tileSpacing", "타일 간격");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("레벨 설정", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            SerializedProperty maxLevelProp = _serializedConfig.FindProperty("maxLevel");
            if (maxLevelProp != null)
            {
                EditorGUILayout.IntSlider(maxLevelProp, 1, 10, new GUIContent("최대 레벨"));
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("플레이어 코어", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawPropertyField("playerCorePosition", "코어 시작 좌표");
        }

        #endregion

        #region Tab 1: 자원 설정

        private void DrawResourceSettings()
        {
            EditorGUILayout.LabelField("자원 및 경제 ─ 전력", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawPropertyField("initialPower", "초기 전력");
            DrawPropertyField("powerPerExpansion", "확장당 전력 비용");
        }

        #endregion

        #region Tab 2: 진원 비용 설정

        private void DrawEmitterCostSettings()
        {
            EditorGUILayout.LabelField("플레이어 시작 방향", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            DrawPropertyField("playerStartDirection", "코어 초기 방향");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("진원 세부 설정 (방향별)", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawEmitterSettingFields("upSetting", "위 방향 (Up)");
            DrawEmitterSettingFields("downSetting", "아래 방향 (Down)");
            DrawEmitterSettingFields("leftSetting", "왼쪽 방향 (Left)");
            DrawEmitterSettingFields("rightSetting", "오른쪽 방향 (Right)");
            DrawEmitterSettingFields("tShapeSetting", "T자 모양 (TShape)");
            DrawEmitterSettingFields("crossSetting", "십자 모양 (Cross)");
            DrawEmitterSettingFields("eightWaySetting", "팔방향 (EightWay)");

            // 비용 미리보기
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("설정 요약 미리보기", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (_selectedConfig != null)
            {
                DrawEmitterSummaryRow("위 (Up)", EmitterDirection.Up);
                DrawEmitterSummaryRow("아래 (Down)", EmitterDirection.Down);
                DrawEmitterSummaryRow("왼쪽 (Left)", EmitterDirection.Left);
                DrawEmitterSummaryRow("오른쪽 (Right)", EmitterDirection.Right);
                DrawEmitterSummaryRow("T자 (TShape)", EmitterDirection.TShape);
                DrawEmitterSummaryRow("십자 (Cross)", EmitterDirection.Cross);
                DrawEmitterSummaryRow("팔방 (EightWay)", EmitterDirection.EightWay);
            }
        }

        private void DrawEmitterSettingFields(string propertyName, string title)
        {
            SerializedProperty prop = _serializedConfig.FindProperty(propertyName);
            if (prop != null)
            {
                EditorGUILayout.LabelField($"  {title}", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("requiredResource"), new GUIContent("필요 자원"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("cost"), new GUIContent("설치 비용"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("maxRange"), new GUIContent("최대 성장 범위 (0=무제한)"));
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }
            else
            {
                EditorGUILayout.HelpBox($"'{propertyName}' 필드를 찾을 수 없습니다.", MessageType.Warning);
            }
        }

        private void DrawEmitterSummaryRow(string label, EmitterDirection direction)
        {
            _serializedConfig.ApplyModifiedProperties();
            
            var setting = _selectedConfig.GetEmitterSetting(direction);
            if (setting == null)
            {
                EditorGUILayout.LabelField($"  {label}", "설정 없음");
                return;
            }

            string resourceLabel = GameConfig.GetResourceLabel(setting.requiredResource);
            string rangeLabel = setting.maxRange > 0 ? $"{setting.maxRange}칸" : "무제한";
            EditorGUILayout.LabelField($"  {label}", $"비용: {resourceLabel} x{setting.cost} | 범위: {rangeLabel}");
        }

        #endregion

        #region Tab 3: 적 AI 설정

        private void DrawEnemySettings()
        {
            EditorGUILayout.LabelField("적 AI 행동 확률", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "적은 매 턴 행동 여부를 확률로 판정한 뒤, 아래 전략 가중치에 따라 한 가지 행동 패턴을 선택합니다.",
                MessageType.Info);

            DrawPercentSlider("enemyActionChance", "턴당 행동 확률");
            DrawPercentSlider("enemyComboActionChance", "연속 행동 확률");
            DrawPercentSlider("enemyLevelGrowthChance", "레벨 성장 압력");
            DrawPercentSlider("enemyHighLevelSpikeChance", "고레벨 변칙 확률");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("공간 기준", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawPercentSlider("enemyCoreSafeZonePercent", "코어 안전권 비율");
            DrawPercentSlider("enemyEmitterSpacingPercent", "진원 간격 비율");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("전략 가중치", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawWeightSlider("enemyFrontlineBuildWeight", "전선 압박");
            DrawWeightSlider("enemySpearheadWeight", "직선 돌파");
            DrawWeightSlider("enemyFanoutWeight", "팔방 확산");
            DrawWeightSlider("enemyHiddenIncursionWeight", "은닉 침투");
            DrawWeightSlider("enemyFlankIncursionWeight", "측면 침투");
            DrawWeightSlider("enemyResourceRaidWeight", "자원 견제");
            DrawWeightSlider("enemyConsolidateWeight", "거점 강화");
        }

        #endregion

        #region Tab 4: 전장의 안개

        private void DrawFogSettings()
        {
            EditorGUILayout.LabelField("전장의 안개 설정", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawPropertyField("enableFogOfWar", "안개 기능 사용");
            DrawPropertyField("fogOfWarRadius", "가시 반경 (타일)");
        }

        #endregion

        #region Tab 5: 스테이지 프리셋

        private void DrawStagePresetSettings()
        {
            EditorGUILayout.LabelField("스테이지 프리셋", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // StageConfig 참조 필드
            SerializedProperty stageConfigProp = _serializedConfig.FindProperty("stageConfig");
            if (stageConfigProp != null)
            {
                EditorGUILayout.PropertyField(stageConfigProp, new GUIContent("스테이지 데이터"));
            }

            StageConfig stage = stageConfigProp != null
                ? stageConfigProp.objectReferenceValue as StageConfig
                : null;

            if (stage == null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("스테이지 에셋을 할당하면 자원 노드를 편집할 수 있습니다.", MessageType.Info);

                if (GUILayout.Button("새 StageConfig 생성"))
                {
                    CreateNewStageConfig(stageConfigProp);
                }

                return;
            }

            // StageConfig용 SerializedObject 구성
            if (_serializedStage == null || _serializedStage.targetObject != stage)
            {
                _serializedStage = new SerializedObject(stage);
            }

            _serializedStage.Update();

            EditorGUILayout.Space(8);
            DrawStageResourceNodes();

            if (_serializedStage.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(stage);
            }
        }

        private void DrawStageResourceNodes()
        {
            EditorGUILayout.LabelField("자원 노드 목록", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            SerializedProperty resourceNodesProp = _serializedStage.FindProperty("resourceNodes");
            if (resourceNodesProp == null || !resourceNodesProp.isArray)
            {
                EditorGUILayout.HelpBox("resourceNodes 필드를 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            // 추가/삭제 버튼
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("+ 노드 추가", GUILayout.Width(100)))
                {
                    int newIndex = resourceNodesProp.arraySize;
                    resourceNodesProp.InsertArrayElementAtIndex(newIndex);

                    // 새 요소 기본값 설정
                    SerializedProperty newElement = resourceNodesProp.GetArrayElementAtIndex(newIndex);
                    SerializedProperty posProp = newElement.FindPropertyRelative("position");
                    SerializedProperty typeProp = newElement.FindPropertyRelative("resourceType");
                    SerializedProperty yieldProp = newElement.FindPropertyRelative("yield");

                    if (posProp != null) posProp.vector2IntValue = Vector2Int.zero;
                    if (typeProp != null) typeProp.enumValueIndex = 1; // PowerNode
                    if (yieldProp != null) yieldProp.intValue = 1;
                }

                if (resourceNodesProp.arraySize > 0 &&
                    GUILayout.Button("- 마지막 삭제", GUILayout.Width(100)))
                {
                    resourceNodesProp.DeleteArrayElementAtIndex(resourceNodesProp.arraySize - 1);
                }

                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField($"총 {resourceNodesProp.arraySize}개", GUILayout.Width(60));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // 헤더
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                EditorGUILayout.LabelField("좌표", GUILayout.Width(120));
                EditorGUILayout.LabelField("자원 유형", GUILayout.Width(120));
                EditorGUILayout.LabelField("생산량", GUILayout.Width(60));
                EditorGUILayout.LabelField("삭제", GUILayout.Width(40));
            }
            EditorGUILayout.EndHorizontal();

            // 스크롤 영역
            _stageResourceScrollPos = EditorGUILayout.BeginScrollView(
                _stageResourceScrollPos, GUILayout.MaxHeight(300));
            {
                int removeIndex = -1;
                for (int i = 0; i < resourceNodesProp.arraySize; i++)
                {
                    SerializedProperty element = resourceNodesProp.GetArrayElementAtIndex(i);
                    SerializedProperty posProp = element.FindPropertyRelative("position");
                    SerializedProperty typeProp = element.FindPropertyRelative("resourceType");
                    SerializedProperty yieldProp = element.FindPropertyRelative("yield");

                    EditorGUILayout.BeginHorizontal();
                    {
                        if (posProp != null)
                        {
                            posProp.vector2IntValue = EditorGUILayout.Vector2IntField(
                                GUIContent.none, posProp.vector2IntValue, GUILayout.Width(120));
                        }

                        if (typeProp != null)
                        {
                            EditorGUILayout.PropertyField(typeProp, GUIContent.none, GUILayout.Width(120));
                        }

                        if (yieldProp != null)
                        {
                            yieldProp.intValue = EditorGUILayout.IntSlider(
                                yieldProp.intValue, 1, 4, GUILayout.Width(100));
                        }

                        if (GUILayout.Button("×", GUILayout.Width(24)))
                        {
                            removeIndex = i;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (removeIndex >= 0)
                {
                    resourceNodesProp.DeleteArrayElementAtIndex(removeIndex);
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("기본값으로 초기화"))
            {
                if (EditorUtility.DisplayDialog(
                    "초기화 확인",
                    "자원 노드를 기본 프리셋으로 초기화하시겠습니까?\n현재 데이터가 덮어씌워집니다.",
                    "초기화", "취소"))
                {
                    StageConfig stageTarget = _serializedStage.targetObject as StageConfig;
                    if (stageTarget != null)
                    {
                        Undo.RecordObject(stageTarget, "스테이지 자원 노드 초기화");
                        stageTarget.ResetToDefault();
                        EditorUtility.SetDirty(stageTarget);
                        _serializedStage = new SerializedObject(stageTarget);
                    }
                }
            }
        }

        private void CreateNewStageConfig(SerializedProperty stageConfigProp)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "StageConfig 에셋 생성",
                "NewStage",
                "asset",
                "새 StageConfig 에셋을 저장할 위치를 선택하세요.");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            StageConfig instance = CreateInstance<StageConfig>();
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (stageConfigProp != null)
            {
                stageConfigProp.objectReferenceValue = instance;
                _serializedConfig.ApplyModifiedProperties();
                EditorUtility.SetDirty(_selectedConfig);
            }

            Debug.Log($"[LevelDesigner] StageConfig 에셋 생성 완료: {path}");
        }

        #endregion

        #region Helpers

        /// <summary>
        /// SerializedProperty를 한국어 라벨로 표시하는 헬퍼.
        /// </summary>
        private void DrawPropertyField(string propertyName, string label)
        {
            SerializedProperty prop = _serializedConfig.FindProperty(propertyName);
            if (prop != null)
            {
                EditorGUILayout.PropertyField(prop, new GUIContent(label));
            }
            else
            {
                EditorGUILayout.HelpBox($"'{propertyName}' 필드를 찾을 수 없습니다.", MessageType.Warning);
            }
        }

        private void DrawPercentSlider(string propertyName, string label)
        {
            DrawIntSlider(propertyName, label, 0, 100, "%");
        }

        private void DrawWeightSlider(string propertyName, string label)
        {
            DrawIntSlider(propertyName, label, 0, 100, "가중치");
        }

        private void DrawIntSlider(string propertyName, string label, int min, int max, string suffix)
        {
            SerializedProperty prop = _serializedConfig.FindProperty(propertyName);
            if (prop == null)
            {
                EditorGUILayout.HelpBox($"'{propertyName}' 필드를 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            {
                prop.intValue = EditorGUILayout.IntSlider(label, Mathf.Clamp(prop.intValue, min, max), min, max);
                EditorGUILayout.LabelField(suffix, GUILayout.Width(44));
            }
            EditorGUILayout.EndHorizontal();
        }

        #endregion
    }
}
