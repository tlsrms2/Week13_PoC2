// ============================================================================
// 파일:    Input/CameraDragController.cs
// 프로젝트: Project SEVERANCE
// 용도: 2D 그리드 플레이 화면용 카메라 드래그 이동 및 줌 제어
// ============================================================================

using UnityEngine;

namespace Severance
{
    /// <summary>
    /// 씬 카메라를 2D Orthographic 기준으로 보정하고,
    /// 마우스 드래그 이동과 휠 줌을 제공하는 카메라 컨트롤러입니다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraDragController : MonoBehaviour
    {
        #region Inspector Fields

        [Header("카메라 기준")]
        [SerializeField] private bool configureAs2D = true;
        [SerializeField] private Vector3 defaultPosition = new Vector3(0f, 0f, -10f);
        [SerializeField] private float defaultOrthographicSize = 8f;

        [Header("드래그 이동")]
        [SerializeField] private int dragMouseButton = 1;
        [SerializeField] private float dragSensitivity = 1f;
        [SerializeField] private bool clampToGridBounds = true;
        [SerializeField] private float boundsPadding = 1.5f;

        [Header("줌")]
        [SerializeField] private float zoomSensitivity = 4f;
        [SerializeField] private float minOrthographicSize = 3f;
        [SerializeField] private float maxOrthographicSize = 18f;

        [Header("카메라 배경")]
        [SerializeField] private SpriteRenderer cameraBackground;
        [SerializeField] private bool autoFitBackgroundToView = true;
        [SerializeField] private float backgroundScalePadding = 1.05f;

        #endregion

        #region Fields

        private Camera _camera;
        private Vector3 _lastMouseWorldPosition;
        private bool _isDragging;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _camera = GetComponent<Camera>();

            if (configureAs2D)
            {
                ConfigureCameraFor2D();
            }

            CacheCameraBackground();
            FitBackgroundToCameraView();
        }

        private void Update()
        {
            HandleZoom();
            HandleDrag();
            ClampToGridBounds();
            FitBackgroundToCameraView();
        }

        #endregion

        #region Private Helpers

        private void ConfigureCameraFor2D()
        {
            _camera.orthographic = true;
            _camera.orthographicSize = Mathf.Clamp(
                defaultOrthographicSize,
                minOrthographicSize,
                maxOrthographicSize);

            transform.position = defaultPosition;
            transform.rotation = Quaternion.identity;
        }

        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scroll, 0f))
            {
                return;
            }

            _camera.orthographicSize = Mathf.Clamp(
                _camera.orthographicSize - scroll * zoomSensitivity,
                minOrthographicSize,
                maxOrthographicSize);
        }

        private void HandleDrag()
        {
            if (Input.GetMouseButtonDown(dragMouseButton))
            {
                _isDragging = true;
                // 스크린 마우스 좌표를 시작 기준으로 임시 보관
                _lastMouseWorldPosition = Input.mousePosition;
                return;
            }

            if (Input.GetMouseButtonUp(dragMouseButton))
            {
                _isDragging = false;
                return;
            }

            if (!_isDragging || !Input.GetMouseButton(dragMouseButton))
            {
                return;
            }

            Vector3 currentMousePos = Input.mousePosition;
            Vector3 difference = currentMousePos - _lastMouseWorldPosition;

            // 카메라 줌 크기(orthographicSize)에 비례하도록 드래그 속도 자동 보정
            float factor = (_camera.orthographicSize * 2f) / Screen.height;
            Vector3 delta = new Vector3(-difference.x * factor, -difference.y * factor, 0f);

            transform.position += delta * dragSensitivity;
            _lastMouseWorldPosition = currentMousePos;
        }

        private Vector3 GetMouseWorldPosition()
        {
            Vector3 mousePosition = Input.mousePosition;
            mousePosition.z = -transform.position.z;
            return _camera.ScreenToWorldPoint(mousePosition);
        }

        private void ClampToGridBounds()
        {
            if (!clampToGridBounds)
            {
                return;
            }

            if (!GridManager.HasInstance)
            {
                return;
            }

            GridManager grid = GridManager.Instance;
            if (grid == null || !grid.IsInitialized)
            {
                return;
            }

            // 설정 데이터로부터 타일 규격 및 간격 조회 (피치 값 산출)
            GameConfig config = GameManager.Instance.Config;
            float size = (config != null) ? config.TileSize : 1.0f;
            float spacing = (config != null) ? config.TileSpacing : 0.0f;
            float pitch = size + spacing;

            // 그리드가 중심 (0,0)에 오프셋 정렬되어 배치됨을 반영
            float offsetX = (grid.Width - 1) * pitch * 0.5f;
            float offsetY = (grid.Height - 1) * pitch * 0.5f;
 
            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;
 
            // 월드 기준 정중앙 정렬을 반영한 경계 계산
            float minX = -offsetX - boundsPadding + halfWidth;
            float maxX = offsetX + boundsPadding - halfWidth;
            float minY = -offsetY - boundsPadding + halfHeight;
            float maxY = offsetY + boundsPadding - halfHeight;
 
            Vector3 position = transform.position;
            position.x = ClampAxis(position.x, minX, maxX, 0f);
            position.y = ClampAxis(position.y, minY, maxY, 0f);
            transform.position = position;
        }

        private static float ClampAxis(float value, float min, float max, float fallback)
        {
            if (min > max)
            {
                return fallback;
            }

            return Mathf.Clamp(value, min, max);
        }

        private void CacheCameraBackground()
        {
            if (cameraBackground != null)
            {
                return;
            }

            SpriteRenderer[] children = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer renderer in children)
            {
                if (renderer == null || renderer.transform == transform)
                {
                    continue;
                }

                string lowerName = renderer.name.ToLowerInvariant();
                if (lowerName.Contains("background") || renderer.name.Contains("배경"))
                {
                    cameraBackground = renderer;
                    return;
                }
            }
        }

        private void FitBackgroundToCameraView()
        {
            if (!autoFitBackgroundToView ||
                cameraBackground == null ||
                cameraBackground.sprite == null ||
                _camera == null ||
                !_camera.orthographic)
            {
                return;
            }

            Bounds spriteBounds = cameraBackground.sprite.bounds;
            if (spriteBounds.size.x <= 0f || spriteBounds.size.y <= 0f)
            {
                return;
            }

            float viewHeight = _camera.orthographicSize * 2f;
            float viewWidth = viewHeight * _camera.aspect;
            float scale = Mathf.Max(
                viewWidth / spriteBounds.size.x,
                viewHeight / spriteBounds.size.y);

            scale *= Mathf.Max(1f, backgroundScalePadding);
            cameraBackground.transform.localScale = new Vector3(scale, scale, 1f);
        }

        #endregion
    }
}
