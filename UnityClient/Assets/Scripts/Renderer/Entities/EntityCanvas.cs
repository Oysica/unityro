using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EntityCanvas : MonoBehaviour {

    [SerializeField] private TextMeshProUGUI EntityName;
    [SerializeField] private TextMeshProUGUI StoreName;
    [SerializeField] private TextMeshProUGUI ChatRoomName;
    [SerializeField] private TextMeshProUGUI EntityMessage;
    [SerializeField] private Slider HPBar;
    [SerializeField] private Slider SPBar;

    private Entity Entity;

    // Casting: a bar over the head, filling up until the skill goes off, as in the official
    // client. In the canvas' units, where the HP bar is 15 by 2 at the feet
    private const float CAST_BAR_WIDTH = 15f;
    private const float CAST_BAR_HEIGHT = 2f;
    private const float CAST_BAR_BORDER = 0.3f;
    private const float CAST_BAR_ABOVE_HEAD = 3f;
    private const float CAST_BAR_DEFAULT_TOP = 24f; // a player's height, when the picture isn't known
    // The sprites' frames leave room over what's drawn: a player's hair ends about 24 up a frame
    // reaching 31
    private const float PICTURE_DRAWN = 0.78f;

    private RectTransform CastBar;
    private RectTransform CastFill;
    private Coroutine Casting;

    public void Init(Entity entity) {
        Entity = entity;
        Entity.OnParameterUpdated += OnEntityParameterUpdated;
    }

    private void OnDestroy() {
        if (Entity != null) {
            Entity.OnParameterUpdated -= OnEntityParameterUpdated;
        }
    }

    private void OnEntityParameterUpdated() {
        SetEntityName(Entity.Status.name);
        SetEntityHP(Entity.Status.hp, Entity.Status.max_hp);
        SetEntitySP(Entity.Status.sp, Entity.Status.max_sp);
    }

    public void SetEntityName(string name) {
        EntityName.text = name;

        Vector2 textSize = EntityName.GetPreferredValues(name);
        (EntityName.gameObject.transform as RectTransform).sizeDelta = textSize;
    }

    public void SetEntityHP(long value, long maxValue) {
        HPBar.minValue = 0f;
        HPBar.maxValue = maxValue;
        HPBar.value = value;
    }

    public void SetEntitySP(long value, long maxValue) {
        SPBar.minValue = 0f;
        SPBar.maxValue = maxValue;
        SPBar.value = value;
    }

    public void SetEntityMessage(string message) {
        EntityMessage.text = message;
        EntityMessage.transform.parent.gameObject.SetActive(true);

        Vector2 textSize = EntityMessage.GetPreferredValues(message);
        (EntityMessage.gameObject.transform as RectTransform).sizeDelta = textSize;

        StartCoroutine(HideAfterSeconds(6f, delegate {
            EntityMessage.transform.parent.gameObject.SetActive(false);
        }));
    }

    internal void ShowEntityName() {
        EntityName.gameObject.SetActive(true);
    }

    internal void HideEntityName() {
        EntityName.gameObject.SetActive(false);
    }

    internal void ShowEntityHP() {
        HPBar.gameObject.SetActive(true);
    }

    internal void HideEntityHP() {
        HPBar.gameObject.SetActive(false);
    }

    internal void ShowEntitySP() {
        SPBar.gameObject.SetActive(true);
    }

    internal void HideEntitySP() {
        SPBar.gameObject.SetActive(false);
    }

    /// <param name="picture">Where the one casting's picture is in the world: the bar goes over it</param>
    public void StartCasting(float seconds, Bounds? picture) {
        if (seconds <= 0f) {
            return;
        }
        if (CastBar == null) {
            BuildCastBar();
        }
        if (Casting != null) {
            StopCoroutine(Casting);
        }

        var top = picture.HasValue ? TopOf(picture.Value) * PICTURE_DRAWN : CAST_BAR_DEFAULT_TOP;
        CastBar.anchoredPosition = new Vector2(0f, top + CAST_BAR_ABOVE_HEAD);
        CastFill.anchorMax = new Vector2(0f, 1f);
        CastBar.gameObject.SetActive(true);
        Casting = StartCoroutine(FillCastBar(seconds));
    }

    public void StopCasting() {
        if (Casting != null) {
            StopCoroutine(Casting);
            Casting = null;
        }
        if (CastBar != null) {
            CastBar.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// How high the picture reaches up the canvas: the sprites lean back towards the camera as
    /// the canvas does, so the highest of the box' corners, not its top in the world
    /// </summary>
    private float TopOf(Bounds bounds) {
        var top = float.MinValue;
        for (var i = 0; i < 8; i++) {
            var corner = new Vector3(
                (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                (i & 4) == 0 ? bounds.min.z : bounds.max.z
            );
            top = Mathf.Max(top, transform.InverseTransformPoint(corner).y);
        }
        return top;
    }

    private IEnumerator FillCastBar(float seconds) {
        var start = Time.time;
        while (Time.time - start < seconds) {
            CastFill.anchorMax = new Vector2((Time.time - start) / seconds, 1f);
            yield return null;
        }
        Casting = null;
        CastBar.gameObject.SetActive(false);
    }

    private void BuildCastBar() {
        var border = new GameObject("Cast Bar", typeof(RectTransform)).AddComponent<Image>();
        border.transform.SetParent(transform, false);
        border.color = new Color(0.05f, 0.05f, 0.05f, 0.9f);
        border.raycastTarget = false;
        CastBar = border.rectTransform;
        CastBar.anchorMin = CastBar.anchorMax = new Vector2(0.5f, 0.5f);
        CastBar.pivot = new Vector2(0.5f, 0f);
        CastBar.sizeDelta = new Vector2(CAST_BAR_WIDTH, CAST_BAR_HEIGHT);

        var back = new GameObject("Background", typeof(RectTransform)).AddComponent<Image>();
        back.transform.SetParent(CastBar, false);
        back.color = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        back.raycastTarget = false;
        back.rectTransform.anchorMin = Vector2.zero;
        back.rectTransform.anchorMax = Vector2.one;
        back.rectTransform.offsetMin = new Vector2(CAST_BAR_BORDER, CAST_BAR_BORDER);
        back.rectTransform.offsetMax = new Vector2(-CAST_BAR_BORDER, -CAST_BAR_BORDER);

        var fill = new GameObject("Fill", typeof(RectTransform)).AddComponent<Image>();
        fill.transform.SetParent(back.transform, false);
        fill.color = new Color(0.3f, 0.95f, 0.35f);
        fill.raycastTarget = false;
        CastFill = fill.rectTransform;
        CastFill.anchorMin = Vector2.zero;
        CastFill.anchorMax = new Vector2(0f, 1f);
        CastFill.offsetMin = CastFill.offsetMax = Vector2.zero;

        border.material = back.material = fill.material = OnTopMaterial();
        CastBar.gameObject.SetActive(false);
    }

    private static Material CastBarMaterial;

    /// <summary>
    /// Drawn over the map: the bar is up by the head, where a tree or a wall behind the one
    /// casting hid it
    /// </summary>
    private static Material OnTopMaterial() {
        if (CastBarMaterial == null) {
            CastBarMaterial = new Material(Canvas.GetDefaultCanvasMaterial());
            CastBarMaterial.SetInt("unity_GUIZTestMode", (int) UnityEngine.Rendering.CompareFunction.Always);
        }
        return CastBarMaterial;
    }

    private IEnumerator HideAfterSeconds(float seconds, Action callback) {
        yield return new WaitForSeconds(seconds);
        callback.Invoke();
    }
}
