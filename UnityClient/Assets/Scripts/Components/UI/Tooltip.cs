using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Tooltip : MonoBehaviour {

    [SerializeField]
    private TextMeshProUGUI tooltipText;

    private void Start() {
        tooltipText.autoSizeTextContainer = true;
    }
    /// <param name="pivot">The corner of the tooltip placed at <paramref name="position"/>, bottom left by default</param>
    public void SetText(string text, Vector3 position, Vector2? pivot = null) {
        // Shown, or its text changed while shown (e.g. a status' time left counting down)
        if (text != null && (!gameObject.activeInHierarchy || text != tooltipText.text)) {
            gameObject.SetActive(true);
            (gameObject.transform as RectTransform).pivot = pivot ?? Vector2.zero;
            gameObject.transform.position = position;
            Vector2 textSize = tooltipText.GetPreferredValues(text);
            tooltipText.text = text;
            (gameObject.transform as RectTransform).sizeDelta = textSize;
        } else if (text == null) {
            gameObject.SetActive(false);
            tooltipText.text = text;
        }
    }
}
