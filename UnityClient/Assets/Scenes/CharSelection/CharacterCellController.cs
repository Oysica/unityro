using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterCellController : MonoBehaviour, IPointerClickHandler {

    private CharacterData data;

    public Text characterName;

    public bool IsEmpty => data == null;

    public Action<CharacterData> OnCharacterSelected;
    // TEMPORARY BRING-UP STUB: classic-RO double-click-to-enter. This client
    // never wired that up (single click only selects; entering requires a
    // separate "Start Game" button whose Addressable-driven visuals are
    // broken/collapsed in this bring-up data set). Wired independently of
    // any texture/layout state so it works regardless of that button's UI
    // issues.
    public Action OnEnterGameRequested;

    public void BindData(CharacterData data) {
        this.data = data;

        characterName.text = data.Name;

        GameObject player = new GameObject(data.Name);
        player.layer = LayerMask.NameToLayer("Characters");
        player.transform.SetParent(this.transform);
        player.transform.localScale = new Vector3(30f, 30f, 1f);
        player.transform.localPosition = new Vector3(0, -40f, 0f);

        Entity entity = player.AddComponent<Entity>();
        entity.Init(data, LayerMask.NameToLayer("Characters"), null, true);
        entity.SetReady(true, true);
    }
    public void OnPointerClick(PointerEventData eventData) {
        if (eventData.button == PointerEventData.InputButton.Left) {
            // Double-click check runs first and unconditionally: the known
            // cosmetic KeyNotFoundException inside OnCharacterSelected (empty
            // job-name table, see Job.cs) is unhandled and would otherwise
            // abort this whole method before reaching the check below.
            if (eventData.clickCount >= 2) {
                OnEnterGameRequested?.Invoke();
            }
            OnCharacterSelected?.Invoke(data);
        }
    }
}
