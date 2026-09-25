using ROIO.Utils;
using System.Collections.Generic;

public partial class ZC {

    // An item put in storage (clif.cpp clif_storageitemadded).
    // 0b44 <index>.W <amount>.L <item id>.L <type>.B <identified>.B <damaged>.B
    //      <cards>.4L { <option index>.W <value>.W <param>.B }*5 <refine>.B <grade>.B
    [PacketHandler(HEADER, "ZC_ADD_ITEM_TO_STORE", SIZE)]
    public class ADD_ITEM_TO_STORE : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_ADD_ITEM_TO_STORE;
        public const int SIZE = 58;
        private const int OPTION_COUNT = 5;
        public PacketHeader Header => HEADER;

        public ItemInfo ItemInfo;

        public void Read(MemoryStreamReader br, int size) {
            ItemInfo = new ItemInfo {
                index = br.ReadShort(),
                amount = br.ReadInt(),
                ItemID = (int) br.ReadUInt(),
                itemType = br.ReadByte()
            };
            ItemInfo.flag = br.ReadByte() == 1 ? 1 : 0; // identified
            ItemInfo.IsDamaged = br.ReadByte() == 1;
            ItemInfo.slot = new ItemInfo.Slot {
                card1 = (int) br.ReadUInt(),
                card2 = (int) br.ReadUInt(),
                card3 = (int) br.ReadUInt(),
                card4 = (int) br.ReadUInt()
            };
            ItemInfo.options = new List<ItemInfo.Option>();
            for (var i = 0; i < OPTION_COUNT; i++) {
                var option = new ItemInfo.Option {
                    optIndex = br.ReadShort(),
                    value = br.ReadShort(),
                    param1 = (byte) br.ReadByte()
                };
                if (option.optIndex != 0) {
                    ItemInfo.options.Add(option);
                }
            }
            ItemInfo.refine = (byte) br.ReadByte();
            ItemInfo.enchantgrade = (byte) br.ReadByte();
        }
    }
}
