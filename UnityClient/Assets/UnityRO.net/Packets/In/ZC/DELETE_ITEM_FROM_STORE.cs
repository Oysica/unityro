using ROIO.Utils;

public partial class ZC {

    // An item taken out of storage (clif.cpp clif_storageitemremoved).
    // 00f6 <index>.W <amount>.L
    [PacketHandler(HEADER, "ZC_DELETE_ITEM_FROM_STORE", SIZE)]
    public class DELETE_ITEM_FROM_STORE : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_DELETE_ITEM_FROM_STORE;
        public const int SIZE = 8;
        public PacketHeader Header => HEADER;

        public short Index;
        public int Amount;

        public void Read(MemoryStreamReader br, int size) {
            Index = br.ReadShort();
            Amount = br.ReadInt();
        }
    }
}
