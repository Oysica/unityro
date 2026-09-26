using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableUIWindow : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler, IInitializePotentialDragHandler {

    private CanvasGroup CanvasGroup;
    private Canvas MainCanvas;

    /// <summary>
    /// A press on the window (not on something in it that drags by itself) brings it over the
    /// others, as in the official client; they used to stay in the order they were made
    /// </summary>
    public void OnInitializePotentialDrag(PointerEventData eventData) {
        transform.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData eventData) {
        transform.SetAsLastSibling();
        if (CanvasGroup == null) {
            CanvasGroup = gameObject.GetOrAddComponent<CanvasGroup>();
        }

        if (MainCanvas == null) {
            MainCanvas = MainCanvas.FindMainCanvas();
        }

        CanvasGroup.alpha = 0.8f;
    }

    public void OnDrag(PointerEventData eventData) {
        (transform as RectTransform).anchoredPosition += eventData.delta / MainCanvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData) {
        CanvasGroup.alpha = 1f;
    }
}
