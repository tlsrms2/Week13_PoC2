// ============================================================================
// 파일:    UI/DebugPanelUI.cs
// 프로젝트: Project SEVERANCE
// 용도: F1 토글 가능한 임시 디버그 패널 컴포넌트 (IMGUI 기반).
//          게임 테스팅을 위한 이미터 배치 설정 제어 제공.
// ============================================================================

using UnityEngine;

namespace Severance
{
    /// <summary>
    /// F1 단축키를 감지하여 런타임에 디버그 기능을 제어하는 IMGUI 기반 UI 패널입니다.
    /// 씬 편집 없이 게임 시작 시 자동으로 초기화 및 부착되어 작동합니다.
    /// </summary>
    public class DebugPanelUI : MonoBehaviour
    {
        #region Private Fields

        private bool _showPanel = false;
        private Owner _selectedOwner = Owner.Player;
        private EmitterDirection _selectedDirection = EmitterDirection.Cross;
        private int _selectedLevel = 1;

        // UI 윈도우 좌표 및 크기 설정
        private Rect _windowRect = new Rect(20, 20, 300, 480);

        #endregion

        #region Initialization

        /// <summary>
        /// 게임이 로드될 때 씬 상에 디버그 패널용 임시 오브젝트를 동적 생성 및 등록합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            GameObject debugGo = new GameObject("DebugPanelUI");
            debugGo.AddComponent<DebugPanelUI>();
            DontDestroyOnLoad(debugGo);
            Debug.Log("[DebugPanelUI] 임시 디버그 패널이 씬 상에 동적 등록되었습니다. (F1 키로 토글)");
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            // F1 키를 눌렀을 때 패널 표시 여부를 토글합니다.
            if (Input.GetKeyDown(KeyCode.F1))
            {
                _showPanel = !_showPanel;
            }
        }

        private void OnGUI()
        {
            if (!_showPanel) return;

            // GUI 스킨 스타일 설정
            GUI.skin.window.fontSize = 13;
            _windowRect = GUI.Window(999, _windowRect, DrawDebugWindow, "SEVERANCE 디버그 패널 (F1 토글)");
        }

        #endregion

        #region IMGUI Rendering Helpers

        private void DrawDebugWindow(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.Space(10);

            // --- 1. 소유 진영 설정 ---
            GUILayout.Label("<b>1. 소유자 (Owner) 선택:</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_selectedOwner == Owner.Player, "아군 (Player)", "Button"))
            {
                _selectedOwner = Owner.Player;
            }
            if (GUILayout.Toggle(_selectedOwner == Owner.Enemy, "적군 (Enemy)", "Button"))
            {
                _selectedOwner = Owner.Enemy;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // --- 2. 이미터 레벨 설정 ---
            GUILayout.Label($"<b>2. 이미터 레벨: Lv{_selectedLevel}</b>");
            _selectedLevel = Mathf.RoundToInt(GUILayout.HorizontalSlider(_selectedLevel, 1f, 5f));
            
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= 5; i++)
            {
                if (GUILayout.Button(i.ToString()))
                {
                    _selectedLevel = i;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // --- 3. 확장 방향 템플릿 설정 ---
            GUILayout.Label("<b>3. 확장 방향 템플릿:</b>");
            
            System.Array directions = System.Enum.GetValues(typeof(EmitterDirection));
            GUILayout.BeginVertical("box");
            for (int i = 0; i < directions.Length; i += 2)
            {
                GUILayout.BeginHorizontal();
                
                EmitterDirection dir1 = (EmitterDirection)directions.GetValue(i);
                bool isSelected1 = (_selectedDirection == dir1);
                if (GUILayout.Toggle(isSelected1, dir1.ToString(), "Button"))
                {
                    _selectedDirection = dir1;
                }

                if (i + 1 < directions.Length)
                {
                    EmitterDirection dir2 = (EmitterDirection)directions.GetValue(i + 1);
                    bool isSelected2 = (_selectedDirection == dir2);
                    if (GUILayout.Toggle(isSelected2, dir2.ToString(), "Button"))
                    {
                        _selectedDirection = dir2;
                    }
                }
                else
                {
                    // 홀수 개수인 경우 비율 유지를 위해 빈 공간 추가
                    GUILayout.Label("", GUILayout.ExpandWidth(true));
                }

                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.Space(15);

            // --- 4. 모드 실행 및 동작 트리거 ---
            GUILayout.Label("<b>4. 입력 모드 제어:</b>");
            
            InputHandler input = InputHandler.Instance;
            if (input != null)
            {
                string statusText = $"현재 모드: {input.CurrentMode}";
                if (input.CurrentMode == InputMode.PlaceEmitter)
                {
                    statusText += $"\n({input.SelectedOwner} {input.SelectedDirection} Lv{input.SelectedLevel})";
                }
                GUILayout.Label($"<color=yellow>{statusText}</color>");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("설치 모드 켜기", GUILayout.Height(30)))
                {
                    input.SetPlacementParameters(_selectedOwner, _selectedDirection, _selectedLevel);
                }
                if (GUILayout.Button("선택 모드로", GUILayout.Height(30)))
                {
                    input.SetMode(InputMode.Select);
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("<color=red>InputHandler가 씬에 없습니다.</color>");
            }

            GUILayout.Space(10);
            GUILayout.EndVertical();

            // 윈도우 드래그 가능 범위 설정
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        #endregion
    }
}
