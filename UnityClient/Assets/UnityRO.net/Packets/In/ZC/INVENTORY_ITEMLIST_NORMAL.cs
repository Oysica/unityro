
using ROIO.Utils;
using System.Collections.Generic;

public partial class ZC {

    /**
     * Non-stackable (Equippable) inventory list
     */
    [PacketHandler(HEADER, "ZC_INVENTORY_ITEMLIST_NORMAL")]
    public class INVENTORY_ITEMLIST_NORMAL : InPacket {

        private const int BLOCK_SIZE = 34;
        public const PacketHeader HEADER = PacketHeader.ZC_INVENTORY_ITEMLIST_NORMAL;
        public PacketHeader Header => HEADER;

        public List<ItemInfo> Inventory = new List<ItemInfo>();
        // 0 inventory, 1 cart, 2 storage, 3 guild storage (clif.cpp enum inventory_type)
        public byte InvType;

        public void Read(MemoryStreamReader br, int size) {
            InvType = (byte)br.ReadByte();

            var count = (br.Length - br.Position) / BLOCK_SIZE;

            for (int i = 0; i < count; i++) {
                var itemInfo = new ItemInfo();

                itemInfo.index = br.ReadShort(); // 2
                itemInfo.ItemID = (int)br.ReadUInt(); // 4
                itemInfo.itemType = br.ReadByte(); // 1
                itemInfo.amount = br.ReadShort(); // 2
                // Where the item can be worn (clif.cpp clif_item_normal: id->equip), not whether it
                // is: arrows always read as worn. Equipped ammo comes as ZC_EQUIP_ARROW
                itemInfo.location = (int)br.ReadUInt(); // 4
                itemInfo.slot = new ItemInfo.Slot() { // 8
                    card1 = (int)br.ReadUInt(),
                    card2 = (int)br.ReadUInt(),
                    card3 = (int)br.ReadUInt(),
                    card4 = (int)br.ReadUInt()
                };
                itemInfo.expireTime = br.ReadInt(); // 4
                itemInfo.flag = br.ReadByte(); // 4

                Inventory.Add(itemInfo);
            }
        }
    }
}
