using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Touches on the game world. A touch that starts outside the UI and is let go quickly where it
/// started is a tap, the touch screen's click; two fingers on the world steer the camera and are
/// no taps.
/// </summary>
public static class ScreenInput {

    private const float TAP_MAX_SECONDS = 0.35f;
    private const float TAP_MAX_MOVE_INCHES = 0.12f;
    private const float DEFAULT_DPI = 160f;

    private struct TouchStart {
        public float Time;
        public Vector2 Position;
        public bool OverUI;
        public bool Gesture;
    }

    private static readonly Dictionary<int, TouchStart> Starts = new Dictionary<int, TouchStart>();
    private static readonly List<Vector2> Taps = new List<Vector2>();
    private static readonly List<Touch> Touches = new List<Touch>();
    private static readonly List<RaycastResult> RaycastResults = new List<RaycastResult>();
    private static int ProcessedFrame = -1;

    /// <summary>
    /// A touch screen is being used: there's no cursor, the world is tapped
    /// </summary>
    public static bool UsesTouch => Application.isMobilePlatform || Input.touchCount > 0;

    /// <summary>
    /// Where the world was tapped this frame
    /// </summary>
    public static List<Vector2> WorldTaps {
        get {
            Refresh();
            return Taps;
        }
    }

    /// <summary>
    /// The fingers down on the world (not on the UI)
    /// </summary>
    public static List<Touch> WorldTouches {
        get {
            Refresh();
            return Touches;
        }
    }

    /// <summary>
    /// Asks the UI itself: the event system only knows a finger after it has handled it, which
    /// can be later in the frame it touches down
    /// </summary>
    public static bool IsOverUI(Vector2 screenPosition) {
        var eventSystem = EventSystem.current;
        if (eventSystem == null) {
            return false;
        }

        RaycastResults.Clear();
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, RaycastResults);
        foreach (var result in RaycastResults) {
            if (result.module is GraphicRaycaster) {
                return true;
            }
        }
        return false;
    }

    private static void Refresh() {
        if (ProcessedFrame == Time.frameCount) {
            return;
        }
        ProcessedFrame = Time.frameCount;
        Taps.Clear();
        Touches.Clear();

        var maxMove = TAP_MAX_MOVE_INCHES * (Screen.dpi > 0 ? Screen.dpi : DEFAULT_DPI);
        for (var i = 0; i < Input.touchCount; i++) {
            var touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began || !Starts.ContainsKey(touch.fingerId)) {
                Starts[touch.fingerId] = new TouchStart {
                    Time = Time.unscaledTime,
                    Position = touch.position,
                    OverUI = IsOverUI(touch.position)
                };
            }

            var start = Starts[touch.fingerId];
            var isUp = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            if (isUp) {
                Starts.Remove(touch.fingerId);
            }
            if (start.OverUI) {
                continue;
            }

            if (!isUp) {
                Touches.Add(touch);
            } else if (touch.phase == TouchPhase.Ended
                && !start.Gesture
                && Time.unscaledTime - start.Time <= TAP_MAX_SECONDS
                && (touch.position - start.Position).magnitude <= maxMove) {
                Taps.Add(touch.position);
            }
        }

        if (Touches.Count > 1) {
            foreach (var touch in Touches) {
                var start = Starts[touch.fingerId];
                start.Gesture = true;
                Starts[touch.fingerId] = start;
            }
        }
    }
}
