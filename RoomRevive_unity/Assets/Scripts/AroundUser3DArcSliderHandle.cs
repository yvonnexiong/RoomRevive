using UnityEngine;
using UnityEngine.EventSystems;

namespace RoomRevive
{
    [DisallowMultipleComponent]
    public class AroundUser3DArcSliderHandle :
        MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler
    {
        public AroundUser3DArcSlider slider;

        [Header("Mouse / Editor Fallback")]
        public bool enableMouseFallback = true;

        private Camera cachedCamera;

        private void Reset()
        {
            if (slider == null)
            {
                slider = GetComponentInParent<AroundUser3DArcSlider>();
            }
        }

        private void Awake()
        {
            if (slider == null)
            {
                slider = GetComponentInParent<AroundUser3DArcSlider>();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (slider == null)
            {
                return;
            }

            if (TryGetWorldPointFromPointerEvent(eventData, out Vector3 worldPoint))
            {
                slider.BeginDragFromWorldPoint(worldPoint);
                return;
            }

            if (TryGetRayFromPointerEvent(eventData, out Ray ray))
            {
                slider.BeginDragFromRay(ray);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (slider == null)
            {
                return;
            }

            if (TryGetWorldPointFromPointerEvent(eventData, out Vector3 worldPoint))
            {
                slider.DragFromWorldPoint(worldPoint);
                return;
            }

            if (TryGetRayFromPointerEvent(eventData, out Ray ray))
            {
                slider.DragFromRay(ray);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (slider == null)
            {
                return;
            }

            slider.EndDrag();
        }

        private void OnMouseDown()
        {
            if (!enableMouseFallback || slider == null)
            {
                return;
            }

            if (TryGetMouseRay(out Ray ray))
            {
                slider.BeginDragFromRay(ray);
            }
        }

        private void OnMouseDrag()
        {
            if (!enableMouseFallback || slider == null)
            {
                return;
            }

            if (TryGetMouseRay(out Ray ray))
            {
                slider.DragFromRay(ray);
            }
        }

        private void OnMouseUp()
        {
            if (!enableMouseFallback || slider == null)
            {
                return;
            }

            slider.EndDrag();
        }

        private bool TryGetWorldPointFromPointerEvent(
            PointerEventData eventData,
            out Vector3 worldPoint
        )
        {
            worldPoint = Vector3.zero;

            if (eventData == null)
            {
                return false;
            }

            RaycastResult raycastResult = eventData.pointerCurrentRaycast;

            if (!raycastResult.isValid)
            {
                raycastResult = eventData.pointerPressRaycast;
            }

            if (!raycastResult.isValid)
            {
                return false;
            }

            worldPoint = raycastResult.worldPosition;

            if (worldPoint == Vector3.zero)
            {
                return false;
            }

            return true;
        }

        private bool TryGetRayFromPointerEvent(
            PointerEventData eventData,
            out Ray ray
        )
        {
            ray = default;

            if (eventData == null)
            {
                return false;
            }

            Camera eventCamera = eventData.pressEventCamera;

            if (eventCamera == null)
            {
                eventCamera = eventData.enterEventCamera;
            }

            if (eventCamera == null)
            {
                eventCamera = Camera.main;
            }

            if (eventCamera == null)
            {
                return false;
            }

            ray = eventCamera.ScreenPointToRay(eventData.position);
            return true;
        }

        private bool TryGetMouseRay(out Ray ray)
        {
            ray = default;

            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
            }

            if (cachedCamera == null)
            {
                return false;
            }

            ray = cachedCamera.ScreenPointToRay(Input.mousePosition);
            return true;
        }
    }
}