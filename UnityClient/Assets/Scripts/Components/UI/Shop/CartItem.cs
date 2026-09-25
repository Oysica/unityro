using ROIO;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

public class CartItem : MonoBehaviour {

    [SerializeField] 
    private RawImage ItemImage;

    [SerializeField] 
    private TextMeshProUGUI ItemName;

    [SerializeField] 
    private TextMeshProUGUI ItemPrice;

    [SerializeField]
    private TextMeshProUGUI ItemQuantity;

    public ItemNPCShopInfo ItemShopInfo { get; private set; }
    public int Quantity { get; private set; }

    private Item Item;

    public void SetItemShopInfo(ItemNPCShopInfo itemShopInfo, int quantity) {
        ItemShopInfo = itemShopInfo;
        Quantity = quantity;

        Item = DBManager.GetItem(itemShopInfo.itemID);
        ItemImage.texture = TextureAssetLoader.Load(DBManager.GetItemResPath(itemShopInfo.itemID, true));
        SetInfo();
    }

    private void SetInfo() {
        ItemName.text = Item.identifiedDisplayName;
        ItemPrice.text = $"{ItemShopInfo.specialPrice}Z";
        ItemQuantity.text = $"{Quantity}";
    }

    public void IncreaseQuantityBy(int quantity) {
        Quantity += quantity;
        SetInfo();
    }
}
