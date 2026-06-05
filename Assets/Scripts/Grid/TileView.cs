using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Severance
{
    /// <summary>
    /// 媛쒕퀎 洹몃━????쇱쓽 ?????쒓컖???곗텧 諛?而댄룷?뚰듃 ?뚮뜑?щ? 愿由ы빀?덈떎.
    /// ????꾨━?뱀뿉 <see cref="SpriteRenderer"/> 諛?<see cref="BoxCollider2D"/> 而댄룷?뚰듃? ?④퍡 遺李⑺빐 ?ъ슜?⑸땲??
    /// <see cref="GameEvents.OnTileChanged"/> 諛?<see cref="GameEvents.OnFogUpdated"/> ?대깽?몃? 援щ룆?섏뿬
    /// ?쇰━ ????곗씠??<see cref="TileData"/>??理쒖떊 ?곹깭? 鍮꾩＜?쇱쓣 ?ㅼ떆媛??숆린?뷀빀?덈떎.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class TileView : MonoBehaviour
    {
        #region Constants ??Colour Palette

        /// <summary>以묐┰ / ?뚯쑀二??녿뒗 ??쇱쓽 湲곕낯 ?됱긽.</summary>
        private static readonly Color NeutralColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color[] PlayerLevelTints =
        {
            new Color(0.30f, 0.48f, 0.82f, 1f),
            new Color(0.20f, 0.40f, 0.78f, 1f),
            new Color(0.12f, 0.30f, 0.68f, 1f),
            new Color(0.07f, 0.21f, 0.56f, 1f),
            new Color(0.03f, 0.13f, 0.42f, 1f)
        };

        private static readonly Color[] EnemyLevelTints =
        {
            new Color(0.78f, 0.24f, 0.24f, 1f),
            new Color(0.72f, 0.15f, 0.15f, 1f),
            new Color(0.62f, 0.08f, 0.08f, 1f),
            new Color(0.50f, 0.04f, 0.04f, 1f),
            new Color(0.36f, 0.01f, 0.01f, 1f)
        };

        /// <summary>?꾩옣???덇컻 留덉뒪???ㅻ쾭?덉씠 ?됱긽 (?꾩쟾 李⑦룓 寃??.</summary>
        private static readonly Color FogColor = new Color(0f, 0f, 0f, 1f);

        /// <summary>吏꾩썝???ㅻⅨ 吏꾩썝???곹뼢沅??덉뿉 ?덉쓣 ???쒖떆???섏씠?쇱씠???됱긽.</summary>
        private static readonly Color SupportedEmitterColor = new Color(0.25f, 1f, 0.35f, 0.42f);

        private static readonly Color PlacementValidColor = new Color(0.25f, 1f, 0.35f, 0.35f);
        private static readonly Color PlacementInvalidColor = new Color(1f, 0.2f, 0.2f, 0.35f);
        private static readonly Color EmitterAreaOutlineColor = new Color(0.2f, 1f, 0.32f, 1f);
        private static readonly Color CoreBorderColor = new Color(0.00f, 0.10f, 1.00f, 1f);
        private static readonly Color PlayerExpansionIntentColor = new Color(0.00f, 0.00f, 1.00f, 1f); // #0000FF
        private static readonly Color EnemyExpansionIntentColor = new Color(1.00f, 0.00f, 0.00f, 1f);  // #FF0000
        private static readonly Color OffEmitterColor = new Color(0.42f, 0.42f, 0.42f, 1f);

        /// <summary>?먯썝 ?몃뱶 醫낅쪟蹂??ㅽ봽?쇱씠???ㅻ쾭?덉씠 ?됱긽 留ㅽ븨.</summary>
        private static readonly Color ResourcePowerNode   = new Color(1.0f, 0.9f, 0.2f, 1f);
        private static readonly Color ResourceVeinIron     = new Color(0.6f, 0.6f, 0.6f, 1f);
        private static readonly Color ResourceVeinCopper   = new Color(0.8f, 0.5f, 0.2f, 1f);
        private static readonly Color ResourceIronBorder = new Color(0.72f, 0.76f, 0.82f, 1f);
        private static readonly Color ResourceCopperBorder = new Color(1.0f, 0.58f, 0.22f, 1f);
        private static readonly Color ResourcePowerBorder  = new Color(1.0f, 0.9f, 0.2f, 1f);

        #endregion

        #region Serialized Fields

        [Header("?쒓컖???먯뀑 李몄“")]
        [Tooltip("??쇱쓽 湲곕낯 諛곌꼍???뚮뜑留곹븯???듭떖 ?ㅽ봽?쇱씠???뚮뜑??")]
        [SerializeField] private SpriteRenderer tileRenderer;




        [Tooltip("?붾쾭洹몄슜 ????덈꺼 ?レ옄瑜??쒖떆???띿뒪??而댄룷?뚰듃 (?좏깮 ?ы빆).")]
        [SerializeField] private TextMeshProUGUI levelText;

        [Tooltip("?꾩옣???덇컻 ?④낵瑜???뼱?뚯슱 ?먯떇 ?ㅻ툕?앺듃???ㅽ봽?쇱씠???뚮뜑??")]
        [SerializeField] private SpriteRenderer fogOverlay;

        [Tooltip("????댁쓽 ?먯썝 ?꾩씠肄섏쓣 ?꾩슱 ?ㅽ봽?쇱씠???뚮뜑??(?좏깮 ?ы빆).")]
        [SerializeField] private SpriteRenderer resourceIndicator;

        [Tooltip("??Up) 諛⑺뼢 ?대????곸뿭 ?쒖떆瑜??뚮뜑留곹븷 ?ㅽ봽?쇱씠???뚮뜑??")]
        [SerializeField] private SpriteRenderer emitterIndicatorUp;

        [Tooltip("?꾨옒(Down) 諛⑺뼢 ?대????곸뿭 ?쒖떆瑜??뚮뜑留곹븷 ?ㅽ봽?쇱씠???뚮뜑??")]
        [SerializeField] private SpriteRenderer emitterIndicatorDown;

        [Tooltip("?쇱そ(Left) 諛⑺뼢 ?대????곸뿭 ?쒖떆瑜??뚮뜑留곹븷 ?ㅽ봽?쇱씠???뚮뜑??")]
        [SerializeField] private SpriteRenderer emitterIndicatorLeft;

        [Tooltip("?ㅻⅨ履?Right) 諛⑺뼢 ?대????곸뿭 ?쒖떆瑜??뚮뜑留곹븷 ?ㅽ봽?쇱씠???뚮뜑??")]
        [SerializeField] private SpriteRenderer emitterIndicatorRight;

        [Tooltip("위(Up) 방향 이미터 영역 레벨 텍스트.")]
        [SerializeField] private TextMeshProUGUI emitterLevelTextUp;

        [Tooltip("아래(Down) 방향 이미터 영역 레벨 텍스트.")]
        [SerializeField] private TextMeshProUGUI emitterLevelTextDown;

        [Tooltip("왼쪽(Left) 방향 이미터 영역 레벨 텍스트.")]
        [SerializeField] private TextMeshProUGUI emitterLevelTextLeft;

        [Tooltip("오른쪽(Right) 방향 이미터 영역 레벨 텍스트.")]
        [SerializeField] private TextMeshProUGUI emitterLevelTextRight;

        [Tooltip("醫뚯륫 ?곷떒(UpLeft) ?媛곸꽑 諛⑺뼢 ?대????곸뿭 ?쒖떆 ?ㅽ봽?쇱씠???뚮뜑??")]
        [SerializeField] private SpriteRenderer emitterIndicatorUpLeft;

        [Tooltip("?곗륫 ?곷떒(UpRight) ?媛곸꽑 諛⑺뼢 ?대????곸뿭 ?쒖떆 ?ㅽ봽?쇱씠???뚮뜑??")]
        [SerializeField] private SpriteRenderer emitterIndicatorUpRight;

        [Tooltip("醫뚯륫 ?섎떒(DownLeft) ?媛곸꽑 諛⑺뼢 ?대????곸뿭 ?쒖떆 ?ㅽ봽?쇱씠???뚮뜑??")]
        [SerializeField] private SpriteRenderer emitterIndicatorDownLeft;

        [Tooltip("?곗륫 ?섎떒(DownRight) ?媛곸꽑 諛⑺뼢 ?대????곸뿭 ?쒖떆 ?ㅽ봽?쇱씠???뚮뜑??")]
        [SerializeField] private SpriteRenderer emitterIndicatorDownRight;

        [Tooltip("吏꾩썝???ㅻⅨ 吏꾩썝???곸뿭 ?꾩뿉 ?덉쓣 ???쒖떆???숈쟻 ?섏씠?쇱씠?? 誘명븷?????고??꾩뿉 ?먮룞 ?앹꽦?⑸땲??")]
        [SerializeField] private SpriteRenderer supportedEmitterOverlay;

        [Tooltip("諛곗튂 紐⑤뱶?먯꽌 ?ㅼ튂 媛???щ?瑜??쒖떆???숈쟻 ?섏씠?쇱씠?? 誘명븷?????고??꾩뿉 ?먮룞 ?앹꽦?⑸땲??")]
        [SerializeField] private SpriteRenderer placementPreviewOverlay;

        [Tooltip("?먯썝/?꾨젰 ????앹궛???レ옄 ?쒖떆. 誘명븷?????고??꾩뿉 ?먮룞 ?앹꽦?⑸땲??")]
        [SerializeField] private TextMeshPro resourceYieldText;

        #endregion

        #region Runtime State

        /// <summary>??酉?而댄룷?뚰듃媛 ?蹂?섎뒗 2D 洹몃━??醫뚰몴.</summary>
        private Vector2Int _position;

        /// <summary>?꾩옱 ??쇱씠 ?덇컻??媛?ㅼ?吏 ?딄퀬 諛앺?吏?媛???곹깭?몄? ?щ?.</summary>
        private bool _isVisible = true;

        private readonly List<LineRenderer> _resourceBorderLines = new List<LineRenderer>();
        private readonly List<LineRenderer> _coreBorderLines = new List<LineRenderer>();
        private readonly List<LineRenderer> _emitterAreaOutlineLines = new List<LineRenderer>();
        private readonly List<LineRenderer> _expansionIntentLines = new List<LineRenderer>();
        private readonly List<TextMeshPro> _expansionIntentTexts = new List<TextMeshPro>();
        private readonly List<LineRenderer> _enemyExpansionIntentLines = new List<LineRenderer>();
        private readonly List<TextMeshPro> _enemyExpansionIntentTexts = new List<TextMeshPro>();

        private bool _placementPreviewActive;
        private EmitterDirection _placementPreviewDirection;
        #endregion

        #region Static Events

        /// <summary>
        /// ?뚮젅?댁뼱媛 ?멸쾶???곸뿉????쇱쓣 ?대┃?덉쓣 ??諛쒖깮?⑸땲??
        /// ?낅젰 泥섎━ ?몃뱾??諛?諛곗튂 ?쒖뒪???깆뿉?????대깽?몃? 泥?랬?⑸땲??
        /// </summary>
        public static event Action<Vector2Int> OnTileClicked;

        #endregion

        #region Initialization

        /// <summary>
        /// 吏??醫뚰몴瑜?湲곗??쇰줈 ???鍮꾩＜??酉곕? 珥덇린?뷀빀?덈떎.
        /// <see cref="GridVisualizer"/>媛 洹몃━?쒕? 鍮뚮뵫????吏곸젒 ?몄텧?⑸땲??
        /// </summary>
        /// <param name="position">?????酉곌? ?섑???2D 洹몃━???꾩튂 醫뚰몴.</param>
        public void Initialize(Vector2Int position)
        {
            _position = position;

            // ?꾨━???몄뒪?숉꽣???ㅽ봽?쇱씠???뚮뜑?ш? 誘명븷?밸맂 寃쎌슦 ?먯껜 寃??            if (tileRenderer == null)
            {
                tileRenderer = GetComponent<SpriteRenderer>();
            }

            // 湲곕낯 以묐┰ 移쇰씪 ?곸슜
            tileRenderer.color = NeutralColor;

            if (levelText != null)
            {
                levelText.text = string.Empty;
            }

            ClearEmitterDirectionLevelTexts();
            EnsureSupportedEmitterOverlay();
            EnsurePlacementPreviewOverlay();
            EnsureResourceBorderLines();
            EnsureEmitterAreaOutlineLines();
            SetFogVisible(false);
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            GameEvents.OnTileChanged += HandleTileChanged;
            GameEvents.OnFogUpdated += HandleFogUpdated;
            GameEvents.OnTurnEnded += HandleTurnEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnTileChanged -= HandleTileChanged;
            GameEvents.OnFogUpdated -= HandleFogUpdated;
            GameEvents.OnTurnEnded -= HandleTurnEnded;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// ?뱀젙 ????곗씠??蹂???듭?瑜??섏떊?섏뿬 ?먯떊?먭쾶 ?대떦????鍮꾩＜?쇱쓣 ?ㅼ떆 媛깆떊?⑸땲??
        /// </summary>
        private void HandleTileChanged(Vector2Int changedPos)
        {
            if (changedPos != _position) return;

            RefreshVisuals();
        }

        /// <summary>
        /// ?덇컻 媛깆떊 ?대깽??????낆엯?덈떎.
        /// </summary>
        private void HandleFogUpdated()
        {
            UpdateFogOverlay();
        }

        /// <summary>
        /// 醫낆냽??泥댄겕濡?遺紐?吏꾩썝 ?곌껐??諛붾뚯뼱??吏꾩썝 ??쇱쓽 ?レ옄??洹몃?濡쒖씪 ???덉쑝誘濡???醫낅즺留덈떎 蹂댁“?좎쓣 媛깆떊?⑸땲??
        /// </summary>
        private void HandleTurnEnded(int turnNumber)
        {
            RefreshVisuals();
        }

        #endregion

        #region Visual Refresh

        /// <summary>
        /// <see cref="GridManager"/>濡쒕???理쒖떊 <see cref="TileData"/> ?뺣낫瑜?議고쉶?섏뿬 ???而щ윭, ?띿뒪?? ?먯썝 ?몃뵒耳?댄꽣瑜??숆린?뷀빀?덈떎.
        /// </summary>
        public void RefreshVisuals()
        {
            if (!GridManager.HasInstance) return;

            TileData data = GridManager.Instance.GetTile(_position);
            if (data == null) return;

            if (!_isVisible)
            {
                HideFoggedDetails();
                return;
            }

            // --- ?몃젰 ?곹깭 諛??덈꺼 洹몃씪?곗씠???곗텧 ---
            if (data.IsOccupiedByEmitter && data.Emitter != null && !data.Emitter.IsOn)
            {
                tileRenderer.color = OffEmitterColor;
            }
            else
            {
                tileRenderer.color = GetOwnerColor(data.Owner, data.VisualLevel);
            }

            // --- ?덈꺼 ?띿뒪???곸슜 ---
            if (levelText != null)
            {
                if (data.Owner != Owner.None && data.Level > 0)
                {
                    levelText.text = data.Level.ToString();
                }
                else
                {
                    levelText.text = string.Empty;
                }
            }

            // --- ?먯썝 ????쒖떆 ---
            UpdateResourceIndicator(data);
            UpdateResourceBorder(data);

            // --- ?대????꾩씠肄??쒖떆 ---
            UpdateEmitterIndicator(data.Emitter);
            ClearSupportPreview();
        }

        /// <summary>
        /// ?몃젰 ?뚯쑀二??뺣낫 諛??곹넗 ?덈꺼 ?ㅼ??쇱뿉 留욌뒗 洹몃씪?곗씠???됱긽??異붿텧?⑸땲??
        /// </summary>
        private Color GetOwnerColor(Owner owner, int level)
        {
            if (level <= 0)
            {
                return NeutralColor;
            }

            switch (owner)
            {
                case Owner.Player:
                    return GetLevelTint(PlayerLevelTints, level);
                case Owner.Enemy:
                    return GetLevelTint(EnemyLevelTints, level);
                default:
                    return NeutralColor;
            }
        }

        private static Color GetLevelTint(Color[] tints, int level)
        {
            int index = Mathf.Clamp(level - 1, 0, tints.Length - 1);
            return tints[index];
        }

        /// <summary>
        /// ?먯썝 留ㅽ븨 媛믪뿉 留욊쾶 ?꾩슜 ?ㅽ봽?쇱씠???몃뵒耳?댄꽣 ?됱긽/鍮꾩＜???곹깭瑜??낅뜲?댄듃?⑸땲??
        /// </summary>
        private void UpdateResourceIndicator(TileData data)
        {
            if (resourceIndicator == null) return;

            if (data.ResourceType == TileResourceType.None ||
                (data.ResourceType != TileResourceType.PowerNode && data.IsResourceCollected))
            {
                resourceIndicator.enabled = false;
                return;
            }

            resourceIndicator.enabled = true;

            switch (data.ResourceType)
            {
                case TileResourceType.PowerNode:
                    resourceIndicator.color = ResourcePowerNode;
                    break;
                case TileResourceType.VeinIron:
                    resourceIndicator.color = ResourceVeinIron;
                    break;
                case TileResourceType.VeinCopper:
                    resourceIndicator.color = ResourceVeinCopper;
                    break;
                case TileResourceType.VeinSilicon:
                    resourceIndicator.color = ResourceVeinCopper;
                    break;
            }
        }

        private void UpdateResourceBorder(TileData data)
        {
            EnsureResourceBorderLines();

            TileResourceType resource = TileData.NormalizeResourceType(data.ResourceType);
            bool showBorder = resource == TileResourceType.VeinIron || 
                              resource == TileResourceType.VeinCopper ||
                              resource == TileResourceType.PowerNode;
            if (resource != TileResourceType.PowerNode && data.IsResourceCollected)
            {
                showBorder = false;
            }

            int yield = showBorder ? Mathf.Clamp(data.ResourceYield, 1, 4) : 0;
            if (showBorder)
            {
                EnsureResourceYieldText();
            }
            
            Color color = Color.white;
            if (resource == TileResourceType.VeinIron)
            {
                color = ResourceIronBorder;
            }
            else if (resource == TileResourceType.VeinCopper)
            {
                color = ResourceCopperBorder;
            }
            else if (resource == TileResourceType.PowerNode)
            {
                color = ResourcePowerBorder;
            }

            for (int i = 0; i < _resourceBorderLines.Count; i++)
            {
                LineRenderer line = _resourceBorderLines[i];
                if (line == null)
                {
                    continue;
                }

                line.enabled = showBorder;
                if (showBorder)
                {
                    line.startColor = color;
                    line.endColor = color;
                }
            }

            if (resourceYieldText != null)
            {
                resourceYieldText.enabled = showBorder;
                resourceYieldText.text = showBorder ? yield.ToString() : string.Empty;
                resourceYieldText.color = color;
            }
        }

        /// <summary>
        /// ????곸쓽 ?대???諛곗튂 ?щ? 諛??뺤옣 諛⑺뼢???곕씪 ?곹븯醫뚯슦 ?꾩씠肄??ㅽ봽?쇱씠?몄? ?쒖꽦???곹깭瑜??낅뜲?댄듃?⑸땲??
        /// </summary>
        private void UpdateEmitterIndicator(Emitter emitter)
        {
            if (emitter == null)
            {
                SetCoreBorderVisible(false);
                if (_placementPreviewActive)
                {
                    ApplyDirectionIndicators(_placementPreviewDirection, 0);
                    return;
                }

                if (emitterIndicatorUp != null) emitterIndicatorUp.enabled = false;
                if (emitterIndicatorDown != null) emitterIndicatorDown.enabled = false;
                if (emitterIndicatorLeft != null) emitterIndicatorLeft.enabled = false;
                if (emitterIndicatorRight != null) emitterIndicatorRight.enabled = false;
                if (emitterIndicatorUpLeft != null) emitterIndicatorUpLeft.enabled = false;
                if (emitterIndicatorUpRight != null) emitterIndicatorUpRight.enabled = false;
                if (emitterIndicatorDownLeft != null) emitterIndicatorDownLeft.enabled = false;
                if (emitterIndicatorDownRight != null) emitterIndicatorDownRight.enabled = false;
                ClearEmitterDirectionLevelTexts();
                return;
            }

            if (emitter.IsCore)
            {
                HideDirectionIndicators();
                ClearEmitterDirectionLevelTexts();
                SetCoreBorderVisible(true);
                return;
            }

            SetCoreBorderVisible(false);
            ApplyDirectionIndicators(emitter.Direction, emitter.Level);
        }

        private void HideDirectionIndicators()
        {
            if (emitterIndicatorUp != null) emitterIndicatorUp.enabled = false;
            if (emitterIndicatorDown != null) emitterIndicatorDown.enabled = false;
            if (emitterIndicatorLeft != null) emitterIndicatorLeft.enabled = false;
            if (emitterIndicatorRight != null) emitterIndicatorRight.enabled = false;
            if (emitterIndicatorUpLeft != null) emitterIndicatorUpLeft.enabled = false;
            if (emitterIndicatorUpRight != null) emitterIndicatorUpRight.enabled = false;
            if (emitterIndicatorDownLeft != null) emitterIndicatorDownLeft.enabled = false;
            if (emitterIndicatorDownRight != null) emitterIndicatorDownRight.enabled = false;
        }

        private void ApplyDirectionIndicators(EmitterDirection direction, int level)
        {
            bool hasUp = direction == EmitterDirection.Up ||
                         direction == EmitterDirection.TShape ||
                         direction == EmitterDirection.Cross;
            bool hasDown = direction == EmitterDirection.Down ||
                           direction == EmitterDirection.Cross;
            bool hasLeft = direction == EmitterDirection.Left ||
                           direction == EmitterDirection.TShape ||
                           direction == EmitterDirection.Cross;
            bool hasRight = direction == EmitterDirection.Right ||
                            direction == EmitterDirection.TShape ||
                            direction == EmitterDirection.Cross;

            bool hasUpLeft = false;
            bool hasUpRight = false;
            bool hasDownLeft = false;
            bool hasDownRight = false;

            if (emitterIndicatorUp != null)
            {
                emitterIndicatorUp.enabled = hasUp;
            }

            if (emitterIndicatorDown != null)
            {
                emitterIndicatorDown.enabled = hasDown;
            }

            if (emitterIndicatorLeft != null)
            {
                emitterIndicatorLeft.enabled = hasLeft;
            }

            if (emitterIndicatorRight != null)
            {
                emitterIndicatorRight.enabled = hasRight;
            }

            if (emitterIndicatorUpLeft != null)
            {
                emitterIndicatorUpLeft.enabled = hasUpLeft;
            }

            if (emitterIndicatorUpRight != null)
            {
                emitterIndicatorUpRight.enabled = hasUpRight;
            }

            if (emitterIndicatorDownLeft != null)
            {
                emitterIndicatorDownLeft.enabled = hasDownLeft;
            }

            if (emitterIndicatorDownRight != null)
            {
                emitterIndicatorDownRight.enabled = hasDownRight;
            }

            bool showLevelText = level > 0;
            SetEmitterDirectionLevelText(emitterLevelTextUp, hasUp && showLevelText, level);
            SetEmitterDirectionLevelText(emitterLevelTextDown, hasDown && showLevelText, level);
            SetEmitterDirectionLevelText(emitterLevelTextLeft, hasLeft && showLevelText, level);
            SetEmitterDirectionLevelText(emitterLevelTextRight, hasRight && showLevelText, level);
        }

        private static void SetEmitterDirectionLevelText(TextMeshProUGUI text, bool visible, int level)
        {
            if (text == null)
            {
                return;
            }

            text.enabled = visible;
            text.text = visible ? level.ToString() : string.Empty;
        }

        private void ClearEmitterDirectionLevelTexts()
        {
            SetEmitterDirectionLevelText(emitterLevelTextUp, false, 0);
            SetEmitterDirectionLevelText(emitterLevelTextDown, false, 0);
            SetEmitterDirectionLevelText(emitterLevelTextLeft, false, 0);
            SetEmitterDirectionLevelText(emitterLevelTextRight, false, 0);
        }

        public void SetPlacementPreview(bool visible, bool canPlace, EmitterDirection direction)
        {
            EnsurePlacementPreviewOverlay();

            _placementPreviewActive = visible;
            _placementPreviewDirection = direction;

            if (placementPreviewOverlay != null)
            {
                placementPreviewOverlay.enabled = visible;
                placementPreviewOverlay.color = canPlace ? PlacementValidColor : PlacementInvalidColor;
            }

            if (GridManager.HasInstance)
            {
                TileData data = GridManager.Instance.GetTile(_position);
                UpdateEmitterIndicator(data?.Emitter);
            }
        }

        public void SetEmitterAreaOutline(bool up, bool right, bool down, bool left)
        {
            EnsureEmitterAreaOutlineLines();

            bool[] edgeVisible = { up, right, down, left };
            for (int i = 0; i < _emitterAreaOutlineLines.Count; i++)
            {
                LineRenderer line = _emitterAreaOutlineLines[i];
                if (line == null)
                {
                    continue;
                }

                line.enabled = _isVisible && i < edgeVisible.Length && edgeVisible[i];
            }
        }

        public void ClearEmitterAreaOutline()
        {
            for (int i = 0; i < _emitterAreaOutlineLines.Count; i++)
            {
                if (_emitterAreaOutlineLines[i] != null)
                {
                    _emitterAreaOutlineLines[i].enabled = false;
                }
            }
        }

        public void ShowSupportPreview(TileData data)
        {
            ClearSupportPreview();

            if (!_isVisible ||
                data == null ||
                !data.IsOccupiedByEmitter ||
                data.Emitter == null ||
                !EmitterManager.HasStableBaseSupport(data, data.Emitter))
            {
                return;
            }

            UpdateSupportedEmitterOverlay(true);
        }

        public void ClearSupportPreview()
        {
            if (supportedEmitterOverlay != null)
            {
                supportedEmitterOverlay.enabled = false;
            }
        }

        public void SetExpansionIntent(Vector2Int approachDirection, int playerLevel, int enemyLevel)
        {
            if (!_isVisible)
            {
                return;
            }

            EnsureExpansionIntentMarkers();

            int edgeIndex = GetExpansionIntentEdgeIndex(approachDirection);
            if (edgeIndex < 0 ||
                edgeIndex >= _expansionIntentLines.Count ||
                edgeIndex >= _expansionIntentTexts.Count ||
                edgeIndex >= _enemyExpansionIntentLines.Count ||
                edgeIndex >= _enemyExpansionIntentTexts.Count)
            {
                return;
            }

            int playerPower = Mathf.Clamp(playerLevel, 0, 5);
            int enemyPower = Mathf.Clamp(enemyLevel, 0, 5);
            if (playerPower <= 0 && enemyPower <= 0)
            {
                return;
            }

            GetExpansionIntentEdge(edgeIndex, out Vector3 start, out Vector3 end);

            if (playerPower > 0 && enemyPower > 0)
            {
                float playerShare = playerPower == enemyPower
                    ? 0.5f
                    : (playerPower > enemyPower ? 2f / 3f : 1f / 3f);
                Vector3 split = Vector3.Lerp(start, end, playerShare);

                ShowExpansionIntentSegment(edgeIndex, true, start, split, PlayerExpansionIntentColor, playerPower);
                ShowExpansionIntentSegment(edgeIndex, false, split, end, EnemyExpansionIntentColor, enemyPower);
                return;
            }

            if (playerPower > 0)
            {
                ShowExpansionIntentSegment(edgeIndex, true, start, end, PlayerExpansionIntentColor, playerPower);
                HideExpansionIntentSegment(edgeIndex, false);
                return;
            }

            HideExpansionIntentSegment(edgeIndex, true);
            ShowExpansionIntentSegment(edgeIndex, false, start, end, EnemyExpansionIntentColor, enemyPower);
        }

        public void ClearExpansionIntentPreview()
        {
            for (int i = 0; i < _expansionIntentLines.Count; i++)
            {
                if (_expansionIntentLines[i] != null)
                {
                    _expansionIntentLines[i].enabled = false;
                }
            }

            for (int i = 0; i < _expansionIntentTexts.Count; i++)
            {
                if (_expansionIntentTexts[i] != null)
                {
                    _expansionIntentTexts[i].enabled = false;
                    _expansionIntentTexts[i].text = string.Empty;
                }
            }

            for (int i = 0; i < _enemyExpansionIntentLines.Count; i++)
            {
                if (_enemyExpansionIntentLines[i] != null)
                {
                    _enemyExpansionIntentLines[i].enabled = false;
                }
            }

            for (int i = 0; i < _enemyExpansionIntentTexts.Count; i++)
            {
                if (_enemyExpansionIntentTexts[i] != null)
                {
                    _enemyExpansionIntentTexts[i].enabled = false;
                    _enemyExpansionIntentTexts[i].text = string.Empty;
                }
            }
        }

        private void ShowExpansionIntentSegment(
            int edgeIndex,
            bool isPlayer,
            Vector3 start,
            Vector3 end,
            Color color,
            int level)
        {
            LineRenderer line = isPlayer ? _expansionIntentLines[edgeIndex] : _enemyExpansionIntentLines[edgeIndex];
            TextMeshPro text = isPlayer ? _expansionIntentTexts[edgeIndex] : _enemyExpansionIntentTexts[edgeIndex];

            if (line != null)
            {
                line.SetPosition(0, start);
                line.SetPosition(1, end);
                line.startColor = color;
                line.endColor = color;
                line.enabled = true;
            }

            if (text != null)
            {
                Vector3 labelPosition = (start + end) * 0.5f;
                labelPosition.z = -0.16f;
                text.transform.localPosition = labelPosition;
                text.enabled = true;
                text.text = level.ToString();
            }
        }

        private void HideExpansionIntentSegment(int edgeIndex, bool isPlayer)
        {
            LineRenderer line = isPlayer ? _expansionIntentLines[edgeIndex] : _enemyExpansionIntentLines[edgeIndex];
            TextMeshPro text = isPlayer ? _expansionIntentTexts[edgeIndex] : _enemyExpansionIntentTexts[edgeIndex];

            if (line != null)
            {
                line.enabled = false;
            }

            if (text != null)
            {
                text.enabled = false;
                text.text = string.Empty;
            }
        }

        /// <summary>
        /// ?덈꺼 2 ?댁긽 吏꾩썝???ㅻⅨ 媛숈? ??吏꾩썝???곸뿭 ?덉뿉 ?ㅼ뼱? ?덈뒗吏 ?쒖떆?⑸땲??
        /// </summary>
        private void UpdateSupportedEmitterOverlay(bool visible)
        {
            EnsureSupportedEmitterOverlay();
            if (supportedEmitterOverlay == null)
            {
                return;
            }

            supportedEmitterOverlay.enabled = visible;
        }

        /// <summary>
        /// ?꾨━???섏젙 ?놁씠???곹뼢沅??쒖떆媛 媛?ν븯?꾨줉 ?고????ㅻ쾭?덉씠瑜?以鍮꾪빀?덈떎.
        /// </summary>
        private void EnsureSupportedEmitterOverlay()
        {
            if (supportedEmitterOverlay != null || tileRenderer == null)
            {
                return;
            }

            GameObject overlayObj = new GameObject("SupportedEmitterOverlay");
            overlayObj.transform.SetParent(transform, false);
            overlayObj.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            overlayObj.transform.localScale = new Vector3(0.72f, 0.72f, 1f);

            supportedEmitterOverlay = overlayObj.AddComponent<SpriteRenderer>();
            supportedEmitterOverlay.sprite = tileRenderer.sprite;
            supportedEmitterOverlay.color = SupportedEmitterColor;
            supportedEmitterOverlay.sortingLayerID = tileRenderer.sortingLayerID;
            supportedEmitterOverlay.sortingOrder = tileRenderer.sortingOrder + 1;
            supportedEmitterOverlay.enabled = false;
        }

        private void EnsurePlacementPreviewOverlay()
        {
            if (placementPreviewOverlay != null || tileRenderer == null)
            {
                return;
            }

            GameObject overlayObj = new GameObject("PlacementPreviewOverlay");
            overlayObj.transform.SetParent(transform, false);
            overlayObj.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            overlayObj.transform.localScale = Vector3.one;

            placementPreviewOverlay = overlayObj.AddComponent<SpriteRenderer>();
            placementPreviewOverlay.sprite = tileRenderer.sprite;
            placementPreviewOverlay.sortingLayerID = tileRenderer.sortingLayerID;
            placementPreviewOverlay.sortingOrder = tileRenderer.sortingOrder + 2;
            placementPreviewOverlay.enabled = false;
        }

        private void EnsureResourceBorderLines()
        {
            if (_resourceBorderLines.Count > 0 || tileRenderer == null)
            {
                return;
            }

            Vector3[] starts =
            {
                new Vector3(-0.47f, 0.47f, -0.09f),
                new Vector3(0.47f, 0.47f, -0.09f),
                new Vector3(0.47f, -0.47f, -0.09f),
                new Vector3(-0.47f, -0.47f, -0.09f)
            };

            Vector3[] ends =
            {
                new Vector3(0.47f, 0.47f, -0.09f),
                new Vector3(0.47f, -0.47f, -0.09f),
                new Vector3(-0.47f, -0.47f, -0.09f),
                new Vector3(-0.47f, 0.47f, -0.09f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject lineObj = new GameObject($"ResourceBorder_{i}");
                lineObj.transform.SetParent(transform, false);

                LineRenderer line = lineObj.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.startWidth = 0.055f;
                line.endWidth = 0.055f;
                line.numCapVertices = 2;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.sortingLayerID = tileRenderer.sortingLayerID;
                line.sortingOrder = tileRenderer.sortingOrder + 6;
                line.SetPosition(0, starts[i]);
                line.SetPosition(1, ends[i]);
                line.enabled = false;
                _resourceBorderLines.Add(line);
            }
        }

        private void EnsureResourceYieldText()
        {
            if (resourceYieldText != null || tileRenderer == null)
            {
                return;
            }

            GameObject textObj = new GameObject("ResourceYieldText");
            textObj.transform.SetParent(transform, false);
            textObj.transform.localPosition = new Vector3(0.30f, -0.30f, -0.13f);
            textObj.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

            resourceYieldText = textObj.AddComponent<TextMeshPro>();
            resourceYieldText.alignment = TextAlignmentOptions.Center;
            resourceYieldText.enableWordWrapping = false;
            resourceYieldText.fontSize = 5.4f;
            resourceYieldText.fontStyle = FontStyles.Bold;
            resourceYieldText.rectTransform.sizeDelta = new Vector2(1.2f, 1.2f);
            resourceYieldText.sortingLayerID = tileRenderer.sortingLayerID;
            resourceYieldText.sortingOrder = tileRenderer.sortingOrder + 9;
            resourceYieldText.enabled = false;
        }

        private void SetCoreBorderVisible(bool visible)
        {
            if (visible)
            {
                EnsureCoreBorderLines();
            }

            for (int i = 0; i < _coreBorderLines.Count; i++)
            {
                if (_coreBorderLines[i] != null)
                {
                    _coreBorderLines[i].enabled = visible;
                }
            }
        }

        private void EnsureCoreBorderLines()
        {
            if (_coreBorderLines.Count > 0 || tileRenderer == null)
            {
                return;
            }

            Vector3[] starts =
            {
                new Vector3(-0.49f, 0.49f, -0.11f),
                new Vector3(0.49f, 0.49f, -0.11f),
                new Vector3(0.49f, -0.49f, -0.11f),
                new Vector3(-0.49f, -0.49f, -0.11f)
            };

            Vector3[] ends =
            {
                new Vector3(0.49f, 0.49f, -0.11f),
                new Vector3(0.49f, -0.49f, -0.11f),
                new Vector3(-0.49f, -0.49f, -0.11f),
                new Vector3(-0.49f, 0.49f, -0.11f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject lineObj = new GameObject($"CoreBorder_{i}");
                lineObj.transform.SetParent(transform, false);

                LineRenderer line = lineObj.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.startWidth = 0.08f;
                line.endWidth = 0.08f;
                line.numCapVertices = 2;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startColor = CoreBorderColor;
                line.endColor = CoreBorderColor;
                line.sortingLayerID = tileRenderer.sortingLayerID;
                line.sortingOrder = tileRenderer.sortingOrder + 8;
                line.SetPosition(0, starts[i]);
                line.SetPosition(1, ends[i]);
                line.enabled = false;
                _coreBorderLines.Add(line);
            }
        }

        private void EnsureExpansionIntentMarkers()
        {
            if (_expansionIntentLines.Count > 0 || tileRenderer == null)
            {
                return;
            }

            Vector3[] starts =
            {
                new Vector3(-0.43f, 0.54f, -0.14f),
                new Vector3(0.54f, 0.43f, -0.14f),
                new Vector3(0.43f, -0.54f, -0.14f),
                new Vector3(-0.54f, -0.43f, -0.14f)
            };

            Vector3[] ends =
            {
                new Vector3(0.43f, 0.54f, -0.14f),
                new Vector3(0.54f, -0.43f, -0.14f),
                new Vector3(-0.43f, -0.54f, -0.14f),
                new Vector3(-0.54f, 0.43f, -0.14f)
            };

            Vector3[] labelPositions =
            {
                new Vector3(0f, 0.54f, -0.15f),
                new Vector3(0.54f, 0f, -0.15f),
                new Vector3(0f, -0.54f, -0.15f),
                new Vector3(-0.54f, 0f, -0.15f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject lineObj = new GameObject($"ExpansionIntent_{i}");
                lineObj.transform.SetParent(transform, false);

                LineRenderer line = lineObj.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.startWidth = 0.08f;
                line.endWidth = 0.08f;
                line.numCapVertices = 4;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startColor = PlayerExpansionIntentColor;
                line.endColor = PlayerExpansionIntentColor;
                line.sortingLayerID = tileRenderer.sortingLayerID;
                line.sortingOrder = tileRenderer.sortingOrder + 10;
                line.SetPosition(0, starts[i]);
                line.SetPosition(1, ends[i]);
                line.enabled = false;
                _expansionIntentLines.Add(line);

                GameObject enemyLineObj = new GameObject($"EnemyExpansionIntent_{i}");
                enemyLineObj.transform.SetParent(transform, false);

                LineRenderer enemyLine = enemyLineObj.AddComponent<LineRenderer>();
                enemyLine.useWorldSpace = false;
                enemyLine.positionCount = 2;
                enemyLine.startWidth = 0.08f;
                enemyLine.endWidth = 0.08f;
                enemyLine.numCapVertices = 4;
                enemyLine.material = new Material(Shader.Find("Sprites/Default"));
                enemyLine.startColor = EnemyExpansionIntentColor;
                enemyLine.endColor = EnemyExpansionIntentColor;
                enemyLine.sortingLayerID = tileRenderer.sortingLayerID;
                enemyLine.sortingOrder = tileRenderer.sortingOrder + 10;
                enemyLine.SetPosition(0, starts[i]);
                enemyLine.SetPosition(1, ends[i]);
                enemyLine.enabled = false;
                _enemyExpansionIntentLines.Add(enemyLine);

                GameObject textObj = new GameObject($"ExpansionIntentLevel_{i}");
                textObj.transform.SetParent(transform, false);
                textObj.transform.localPosition = labelPositions[i];
                textObj.transform.localScale = new Vector3(0.22f, 0.22f, 1f);

                TextMeshPro text = textObj.AddComponent<TextMeshPro>();
                text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = false;
                text.fontSize = 6.5f;
                text.fontStyle = FontStyles.Bold;
                text.color = Color.white;
                text.rectTransform.sizeDelta = new Vector2(1.4f, 1.4f);
                text.sortingLayerID = tileRenderer.sortingLayerID;
                text.sortingOrder = tileRenderer.sortingOrder + 11;
                text.enabled = false;
                _expansionIntentTexts.Add(text);

                GameObject enemyTextObj = new GameObject($"EnemyExpansionIntentLevel_{i}");
                enemyTextObj.transform.SetParent(transform, false);
                enemyTextObj.transform.localPosition = labelPositions[i];
                enemyTextObj.transform.localScale = new Vector3(0.22f, 0.22f, 1f);

                TextMeshPro enemyText = enemyTextObj.AddComponent<TextMeshPro>();
                enemyText.alignment = TextAlignmentOptions.Center;
                enemyText.enableWordWrapping = false;
                enemyText.fontSize = 6.5f;
                enemyText.fontStyle = FontStyles.Bold;
                enemyText.color = Color.white;
                enemyText.rectTransform.sizeDelta = new Vector2(1.4f, 1.4f);
                enemyText.sortingLayerID = tileRenderer.sortingLayerID;
                enemyText.sortingOrder = tileRenderer.sortingOrder + 11;
                enemyText.enabled = false;
                _enemyExpansionIntentTexts.Add(enemyText);
            }
        }

        private static int GetExpansionIntentEdgeIndex(Vector2Int approachDirection)
        {
            if (approachDirection == Vector2Int.up) return 0;
            if (approachDirection == Vector2Int.right) return 1;
            if (approachDirection == Vector2Int.down) return 2;
            if (approachDirection == Vector2Int.left) return 3;
            return -1;
        }

        private static void GetExpansionIntentEdge(int edgeIndex, out Vector3 start, out Vector3 end)
        {
            switch (edgeIndex)
            {
                case 0:
                    start = new Vector3(-0.43f, 0.54f, -0.14f);
                    end = new Vector3(0.43f, 0.54f, -0.14f);
                    return;
                case 1:
                    start = new Vector3(0.54f, 0.43f, -0.14f);
                    end = new Vector3(0.54f, -0.43f, -0.14f);
                    return;
                case 2:
                    start = new Vector3(0.43f, -0.54f, -0.14f);
                    end = new Vector3(-0.43f, -0.54f, -0.14f);
                    return;
                case 3:
                    start = new Vector3(-0.54f, -0.43f, -0.14f);
                    end = new Vector3(-0.54f, 0.43f, -0.14f);
                    return;
                default:
                    start = Vector3.zero;
                    end = Vector3.zero;
                    return;
            }
        }

        private void EnsureEmitterAreaOutlineLines()
        {
            if (_emitterAreaOutlineLines.Count > 0 || tileRenderer == null)
            {
                return;
            }

            Vector3[] starts =
            {
                new Vector3(-0.5f, 0.5f, -0.12f),
                new Vector3(0.5f, 0.5f, -0.12f),
                new Vector3(0.5f, -0.5f, -0.12f),
                new Vector3(-0.5f, -0.5f, -0.12f)
            };

            Vector3[] ends =
            {
                new Vector3(0.5f, 0.5f, -0.12f),
                new Vector3(0.5f, -0.5f, -0.12f),
                new Vector3(-0.5f, -0.5f, -0.12f),
                new Vector3(-0.5f, 0.5f, -0.12f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject lineObj = new GameObject($"EmitterAreaOutline_{i}");
                lineObj.transform.SetParent(transform, false);

                LineRenderer line = lineObj.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.startWidth = 0.07f;
                line.endWidth = 0.07f;
                line.numCapVertices = 2;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startColor = EmitterAreaOutlineColor;
                line.endColor = EmitterAreaOutlineColor;
                line.sortingLayerID = tileRenderer.sortingLayerID;
                line.sortingOrder = tileRenderer.sortingOrder + 8;
                line.SetPosition(0, starts[i]);
                line.SetPosition(1, ends[i]);
                line.enabled = false;
                _emitterAreaOutlineLines.Add(line);
            }
        }

        #endregion

        #region Fog of War

        /// <summary>
        /// ??쇱쓽 ?꾩옣???덇컻 李⑦룓 媛???щ?瑜?媛깆떊?⑸땲??
        /// </summary>
        /// <param name="visible">true = 諛앺?吏??덇컻 ?놁쓬), false = ?덇컻??媛?ㅼ쭚.</param>
        public void SetFogVisible(bool visible)
        {
            _isVisible = visible;
            UpdateFogOverlay();
        }

        /// <summary>
        /// 媛???곹깭???곕씪 ?ㅽ봽?쇱씠???ㅻ쾭?덉씠 留덉뒪???쒖꽦 ?곹깭瑜??숆린?뷀빀?덈떎.
        /// </summary>
        private void UpdateFogOverlay()
        {
            if (fogOverlay == null) return;

            fogOverlay.enabled = !_isVisible;

            if (!_isVisible)
            {
                fogOverlay.color = FogColor;
                if (tileRenderer != null)
                {
                    fogOverlay.sortingLayerID = tileRenderer.sortingLayerID;
                    fogOverlay.sortingOrder = tileRenderer.sortingOrder + 20;
                }

                HideFoggedDetails();
            }
            else
            {
                RefreshVisuals();
            }
        }

        private void HideFoggedDetails()
        {
            if (levelText != null) levelText.text = string.Empty;
            if (resourceIndicator != null) resourceIndicator.enabled = false;
            if (emitterIndicatorUp != null) emitterIndicatorUp.enabled = false;
            if (emitterIndicatorDown != null) emitterIndicatorDown.enabled = false;
            if (emitterIndicatorLeft != null) emitterIndicatorLeft.enabled = false;
            if (emitterIndicatorRight != null) emitterIndicatorRight.enabled = false;
            if (emitterIndicatorUpLeft != null) emitterIndicatorUpLeft.enabled = false;
            if (emitterIndicatorUpRight != null) emitterIndicatorUpRight.enabled = false;
            if (emitterIndicatorDownLeft != null) emitterIndicatorDownLeft.enabled = false;
            if (emitterIndicatorDownRight != null) emitterIndicatorDownRight.enabled = false;
            ClearEmitterDirectionLevelTexts();
            if (supportedEmitterOverlay != null) supportedEmitterOverlay.enabled = false;
            if (resourceYieldText != null)
            {
                resourceYieldText.enabled = false;
                resourceYieldText.text = string.Empty;
            }
            for (int i = 0; i < _resourceBorderLines.Count; i++)
            {
                if (_resourceBorderLines[i] != null)
                {
                    _resourceBorderLines[i].enabled = false;
                }
            }
            SetCoreBorderVisible(false);
            ClearEmitterAreaOutline();
            ClearExpansionIntentPreview();
        }

        /// <summary>?꾩옱 ??쇱쓽 媛?쒖꽦 ?곹깭瑜??섑??낅땲??</summary>
        public bool IsVisible => _isVisible;

        #endregion

        #region Input

        /// <summary>
        /// ?좊땲??留덉슦???대┃ 肄쒕갚 ?⑥닔?낅땲?? 肄쒕씪?대뜑 ?대┃ ?대깽???섏떊 ???뺤쟻 ?대┃ ?대깽?몃? ?⑸땲??
        /// </summary>
        private void OnMouseDown()
        {
            OnTileClicked?.Invoke(_position);
        }

        #endregion

        /// <summary>??酉?而댄룷?뚰듃媛 李몄“?섎뒗 怨좎쑀 2D 洹몃━???꾩튂.</summary>
        public Vector2Int Position => _position;
    }
}
