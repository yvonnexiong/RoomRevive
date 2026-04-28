using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RoomRevive
{
    public class ProductCardUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text brandNameText;
        [SerializeField] private TMP_Text productNameText;
        [SerializeField] private TMP_Text emotionalLineText;
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button exploreButton;

        [Header("Follow Settings")]
        [SerializeField] private float distance = 1.4f;
        [SerializeField] private float rightOffset = 0.45f;
        [SerializeField] private float verticalOffset = 0.05f;

        [Header("Auto Hide")]
        [SerializeField] private float autoHideDelay = 2f;

        private Transform _cam;
        private float _autoHideTimer;
        private CanvasGroup _canvasGroup;
        private Coroutine _fadeCoroutine;
        private ProductSO _currentProduct;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            HotspotInteractable.OnAnySelected += Show;
        }

        void OnDestroy()
        {
            HotspotInteractable.OnAnySelected -= Show;
        }

        void Start()
        {
            var centerEye = GameObject.Find("CenterEyeAnchor");
            _cam = centerEye != null ? centerEye.transform : Camera.main?.transform;

            if (closeButton != null)
                closeButton.onClick.AddListener(HideWithFade);

            if (exploreButton != null)
                exploreButton.onClick.AddListener(OnExplore);

            // Hide via CanvasGroup — keep GameObject always active so coroutines work
            SetVisible(false);
        }

        void Update()
        {
            if (_autoHideTimer > 0f)
            {
                _autoHideTimer -= Time.deltaTime;
                if (_autoHideTimer <= 0f)
                    HideWithFade();
            }
        }

        void LateUpdate()
        {
            if (_cam == null) return;
            transform.position = _cam.position
                + _cam.forward * distance
                + _cam.right   * rightOffset
                + _cam.up      * verticalOffset;
            transform.rotation = Quaternion.LookRotation(_cam.forward);
        }

        void Show(ProductSO product)
        {
            _currentProduct = product;
            if (brandNameText != null)   brandNameText.text   = product.brandName;
            if (productNameText != null) productNameText.text = product.productName;
            if (emotionalLineText != null) emotionalLineText.text = product.emotionalLine;

            if (thumbnailImage != null)
            {
                thumbnailImage.sprite  = product.thumbnail;
                thumbnailImage.enabled = product.thumbnail != null;
            }

            _autoHideTimer = autoHideDelay;

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeIn());
        }

        void HideWithFade()
        {
            _autoHideTimer = 0f;
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOut());
        }

        void SetVisible(bool visible)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        private IEnumerator FadeIn()
        {
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            float startAlpha = _canvasGroup.alpha;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.2f;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, Mathf.Clamp01(t));
                yield return null;
            }
            _canvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOut()
        {
            float startAlpha = _canvasGroup.alpha;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.2f;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(t));
                yield return null;
            }
            SetVisible(false);
        }

        void OnExplore()
        {
            HideWithFade();
            VariantCarouselUI.OnShowRequested?.Invoke(_currentProduct);
        }
    }
}
