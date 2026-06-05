// ============================================================================
// 파일:    Editor/TurnUISceneBuilder.cs
// 용도:    런타임 생성 TurnUI를 씬 배치 UI로 전환하기 위한 Editor 전용 생성기.
// ============================================================================

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Severance.Editor
{
    public static class TurnUISceneBuilder
    {
        private static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.07f, 0.82f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.22f, 0.30f, 0.96f);
        private static readonly Color TextColor = new Color(0.92f, 0.96f, 1f, 1f);
        private static readonly Color WarningColor = new Color(1f, 0.34f, 0.34f, 0f);

        [MenuItem("Severance/Setup Scene TurnUI")]
        public static void SetupCurrentScene()
        {
            Canvas canvas = EnsureCanvas();
            EnsureEventSystem();

            TurnUI turnUI = Object.FindFirstObjectByType<TurnUI>();
            if (turnUI == null)
            {
                GameObject turnUIObject = new GameObject("TurnUI");
                turnUI = turnUIObject.AddComponent<TurnUI>();
            }

            RectTransform root = EnsureChild(canvas.transform, "TurnUIRoot");
            Stretch(root, Vector2.zero, Vector2.zero);

            RectTransform statusPanel = CreatePanel(root, "StatusPanel",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -16f), new Vector2(820f, 78f));
            HorizontalLayoutGroup statusLayout = EnsureComponent<HorizontalLayoutGroup>(statusPanel.gameObject);
            statusLayout.padding = new RectOffset(10, 10, 8, 8);
            statusLayout.spacing = 6f;
            statusLayout.childAlignment = TextAnchor.MiddleLeft;
            statusLayout.childControlWidth = false;
            statusLayout.childControlHeight = true;

            TextMeshProUGUI turnText = CreateText(statusPanel, "TurnText", "턴: 0", 24f, TextAlignmentOptions.MidlineLeft, 120f);
            TextMeshProUGUI resourceText = CreateText(statusPanel, "ResourceText", "전력 -", 16f, TextAlignmentOptions.MidlineLeft, 660f);
            resourceText.overflowMode = TextOverflowModes.Overflow;

            RectTransform controlPanel = CreatePanel(root, "ControlPanel",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(16f, 16f), new Vector2(820f, 72f));
            HorizontalLayoutGroup controlLayout = EnsureComponent<HorizontalLayoutGroup>(controlPanel.gameObject);
            controlLayout.padding = new RectOffset(10, 10, 10, 10);
            controlLayout.spacing = 10f;
            controlLayout.childAlignment = TextAnchor.MiddleLeft;
            controlLayout.childControlWidth = false;
            controlLayout.childControlHeight = true;

            Button nextButton = CreateButton(controlPanel, "NextTurnButton", "다음", 86f);
            Button directionButton = CreateButton(controlPanel, "DirectionButton", "Cross", 118f);
            TextMeshProUGUI directionButtonText = directionButton.GetComponentInChildren<TextMeshProUGUI>();
            Button levelButton = CreateButton(controlPanel, "LevelButton", "Lv1", 86f);
            TextMeshProUGUI levelButtonText = levelButton.GetComponentInChildren<TextMeshProUGUI>();
            TextMeshProUGUI placementText = CreateText(controlPanel, "PlacementModeText", "배치: Cross Lv1 / 구리 x1", 15f, TextAlignmentOptions.MidlineLeft, 410f);

            RectTransform directionPanel = CreateOptionPanel(root, "DirectionOptions",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(96f, 94f), new Vector2(230f, 222f));
            string[] directionLabels = { "위", "아래", "왼쪽", "오른쪽", "T자", "십자" };
            foreach (string label in directionLabels)
            {
                CreateButton(directionPanel, $"Direction_{label}", label, 210f);
            }
            directionPanel.gameObject.SetActive(false);

            RectTransform levelPanel = CreateOptionPanel(root, "LevelOptions",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(220f, 94f), new Vector2(230f, 160f));
            Button level1Button = CreateButton(levelPanel, "Level_1", "Lv1", 210f);
            Button level2Button = CreateButton(levelPanel, "Level_2", "Lv2", 210f);
            Button level3Button = CreateButton(levelPanel, "Level_3", "Lv3", 210f);
            Button level4Button = CreateButton(levelPanel, "Level_4", "Lv4", 210f);
            Button level5Button = CreateButton(levelPanel, "Level_5", "Lv5", 210f);
            level4Button.gameObject.name = "Level_4";
            level5Button.gameObject.name = "Level_5";
            levelPanel.gameObject.SetActive(false);

            RectTransform tileInfoPanel = CreatePanel(root, "TileInfoPanel",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-12f, 12f), new Vector2(430f, 34f));
            TextMeshProUGUI tileInfoText = CreateText(tileInfoPanel, "TileInfoText", string.Empty, 14f, TextAlignmentOptions.MidlineLeft, 406f);
            Stretch(tileInfoText.rectTransform, new Vector2(10f, 5f), new Vector2(-10f, -5f));

            TextMeshProUGUI warningText = CreateText(root, "WarningText", string.Empty, 24f, TextAlignmentOptions.Center, 800f);
            RectTransform warningRect = warningText.rectTransform;
            warningRect.anchorMin = new Vector2(0.5f, 0.5f);
            warningRect.anchorMax = new Vector2(0.5f, 0.5f);
            warningRect.pivot = new Vector2(0.5f, 0.5f);
            warningRect.anchoredPosition = new Vector2(0f, 100f);
            warningRect.sizeDelta = new Vector2(800f, 100f);
            warningText.enableWordWrapping = true;
            warningText.color = WarningColor;

            RectTransform resultPanel = CreatePanel(root, "ResultPanel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(280f, 142f));
            VerticalLayoutGroup resultLayout = EnsureComponent<VerticalLayoutGroup>(resultPanel.gameObject);
            resultLayout.padding = new RectOffset(18, 18, 18, 18);
            resultLayout.spacing = 14f;
            resultLayout.childAlignment = TextAnchor.MiddleCenter;
            TextMeshProUGUI resultText = CreateText(resultPanel, "ResultText", string.Empty, 34f, TextAlignmentOptions.Center, 220f);
            Button restartButton = CreateButton(resultPanel, "RestartButton", "재시작", 150f);
            resultPanel.gameObject.SetActive(false);

            AssignTurnUI(
                turnUI,
                turnText,
                nextButton,
                resourceText,
                resultText,
                restartButton,
                directionButton,
                directionButtonText,
                levelButton,
                levelButtonText,
                level1Button,
                level2Button,
                level3Button,
                placementText,
                directionPanel,
                levelPanel,
                resultPanel,
                tileInfoText,
                warningText);

            AssignGameManager(turnUI);

            EditorUtility.SetDirty(turnUI);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[TurnUISceneBuilder] 씬 TurnUI 생성 및 참조 연결 완료.");
        }

        private static Canvas EnsureCanvas()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                return canvas;
            }

            GameObject canvasObject = new GameObject("HUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static RectTransform EnsureChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing as RectTransform;
            }

            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.transform as RectTransform;
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            RectTransform rect = EnsureChild(parent, name);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = EnsureComponent<Image>(rect.gameObject);
            image.color = PanelColor;
            image.raycastTarget = false;
            return rect;
        }

        private static RectTransform CreateOptionPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            RectTransform panel = CreatePanel(parent, name, anchorMin, anchorMax, pivot, position, size);
            VerticalLayoutGroup layout = EnsureComponent<VerticalLayoutGroup>(panel.gameObject);
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            return panel;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, TextAlignmentOptions alignment, float preferredWidth)
        {
            RectTransform rect = EnsureChild(parent, name);
            TextMeshProUGUI label = EnsureComponent<TextMeshProUGUI>(rect.gameObject);
            label.text = text;
            label.fontSize = fontSize;
            label.color = TextColor;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;

            LayoutElement layout = EnsureComponent<LayoutElement>(rect.gameObject);
            layout.minWidth = preferredWidth;
            layout.preferredWidth = preferredWidth;
            return label;
        }

        private static Button CreateButton(Transform parent, string name, string label, float preferredWidth)
        {
            RectTransform rect = EnsureChild(parent, name);
            Image image = EnsureComponent<Image>(rect.gameObject);
            image.color = ButtonColor;

            Button button = EnsureComponent<Button>(rect.gameObject);
            button.targetGraphic = image;

            LayoutElement layout = EnsureComponent<LayoutElement>(rect.gameObject);
            layout.minWidth = preferredWidth;
            layout.preferredWidth = preferredWidth;
            layout.minHeight = 26f;
            layout.preferredHeight = 28f;

            TextMeshProUGUI labelText = CreateText(rect, "Label", label, 15f, TextAlignmentOptions.Center, preferredWidth);
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = 10f;
            labelText.fontSizeMax = 15f;
            Stretch(labelText.rectTransform, Vector2.zero, Vector2.zero);
            return button;
        }

        private static void AssignTurnUI(
            TurnUI turnUI,
            TextMeshProUGUI turnText,
            Button nextButton,
            TextMeshProUGUI resourceText,
            TextMeshProUGUI resultText,
            Button restartButton,
            Button directionButton,
            TextMeshProUGUI directionButtonText,
            Button levelButton,
            TextMeshProUGUI levelButtonText,
            Button level1Button,
            Button level2Button,
            Button level3Button,
            TextMeshProUGUI placementText,
            RectTransform directionPanel,
            RectTransform levelPanel,
            RectTransform resultPanel,
            TextMeshProUGUI tileInfoText,
            TextMeshProUGUI warningText)
        {
            SerializedObject so = new SerializedObject(turnUI);
            SetObject(so, "turnCountText", turnText);
            SetObject(so, "nextTurnButton", nextButton);
            SetObject(so, "resourceText", resourceText);
            SetObject(so, "resultText", resultText);
            SetObject(so, "restartButton", restartButton);
            SetObject(so, "directionButton", directionButton);
            SetObject(so, "directionButtonText", directionButtonText);
            SetObject(so, "levelButton", levelButton);
            SetObject(so, "levelButtonText", levelButtonText);
            SetObject(so, "placeLevel1Button", level1Button);
            SetObject(so, "placeLevel2Button", level2Button);
            SetObject(so, "placeLevel3Button", level3Button);
            SetObject(so, "placementModeText", placementText);
            SetObject(so, "directionOptionsPanel", directionPanel);
            SetObject(so, "levelOptionsPanel", levelPanel);
            SetObject(so, "resultPanel", resultPanel);
            SetObject(so, "tileInfoText", tileInfoText);
            SetObject(so, "warningText", warningText);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignGameManager(TurnUI turnUI)
        {
            GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
            if (gameManager == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(gameManager);
            SetObject(so, "turnUI", turnUI);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
