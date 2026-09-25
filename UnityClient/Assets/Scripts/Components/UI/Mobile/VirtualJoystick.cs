using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// An on-screen stick: the knob follows the finger up to Radius from the middle. Direction is
/// where it points, its length how far it's pushed (0 to 1).
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {

    public RectTransform Knob;
    public float Radius = 70f;

    public Vector2 Direction { get; private set; }
    public bool IsHeld { get; private set; }

    private int PointerId;

    public void OnPointerDown(PointerEventData eventData) {
        IsHeld = true;
        PointerId = eventData.pointerId;
        Follow(eventData);
    }

    public void OnDrag(PointerEventData eventData) {
        if (IsHeld && eventData.pointerId == PointerId) {
            Follow(eventData);
        }
    }

    public void OnPointerUp(PointerEventData eventData) {
        if (eventData.pointerId == PointerId) {
            Release();
        }
    }

    private void OnDisable() {
        Release();
    }

    private void Follow(PointerEventData eventData) {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(transform as RectTransform, eventData.position, eventData.pressEventCamera, out var local)) {
            return;
        }

        var offset = Vector2.ClampMagnitude(local, Radius);
        Knob.anchoredPosition = offset;
        Direction = offset / Radius;
    }

    private void Release() {
        IsHeld = false;
        Direction = Vector2.zero;
        if (Knob != null) {
            Knob.anchoredPosition = Vector2.zero;
        }
    }
}
