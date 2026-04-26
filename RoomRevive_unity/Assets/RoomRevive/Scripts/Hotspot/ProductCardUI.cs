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
        [SerializeField] private float autoHideDelay = 5f;

        private Transform _cam;
        private float _autoHideTimer;

        void Awake()
        {
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
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));

            if (exploreButton != null)
                exploreButton.onClick.AddListener(OnExplore);

            gameObject.SetActive(false);
        }

        void Update()
        {
            if (_autoHideTimer > 0f)
            {
                _autoHideTimer -= Time.deltaTime;
                if (_autoHideTimer <= 0f)
                    gameObject.SetActive(false);
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
            if (brandNameText != null)   brandNameText.text   = product.brandName;
            if (productNameText != null) productNameText.text = product.productName;
            if (emotionalLineText != null) emotionalLineText.text = product.emotionalLine;

            if (thumbnailImage != null)
            {
                thumbnailImage.sprite  = product.thumbnail;
                thumbnailImage.enabled = product.thumbnail != null;
            }

            _autoHideTimer = autoHideDelay;
            gameObject.SetActive(true);
        }

        void OnExplore()
        {
            Debug.Log("[ProductCard] Explore tapped — expanded view not yet implemented.");
        }
    }
}
