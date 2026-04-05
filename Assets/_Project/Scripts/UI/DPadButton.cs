using UnityEngine;
using UnityEngine.EventSystems;

namespace Shrink.UI
{
    /// <summary>
    /// Botón individual del D-pad. Gestiona su propio press/release y notifica a DPadController.
    /// Press = empieza a mover en esta dirección. Release = para.
    /// IBeginDragHandler + IDragHandler vacíos evitan que el drag burbujee a DPadController
    /// y dispare OnPointerUp prematuramente al mover el dedo sobre el botón.
    /// </summary>
    public class DPadButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler
    {
        [Tooltip("(0,1)=Up  (0,-1)=Down  (-1,0)=Left  (1,0)=Right")]
        [SerializeField] public Vector2Int direction;

        private DPadController _controller;

        private void Awake()
        {
            _controller = GetComponentInParent<DPadController>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_controller == null || _controller.IsEditMode) return;
            _controller.OnButtonDown(direction);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_controller == null || _controller.IsEditMode) return;
            _controller.OnButtonUp(direction);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_controller != null && _controller.IsEditMode)
                _controller.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_controller != null && _controller.IsEditMode)
                _controller.OnDrag(eventData);
        }
    }
}
