using ROIO.Utils;

public partial class ZC {

    // Ends a full item list started by ZC_INVENTORY_START (clif.cpp clif_inventoryEnd).
    // 0b0b <type>.B <flag>.B
    [PacketHandler(HEADER, "ZC_INVENTORY_END", SIZE)]
    public class INVENTORY_END : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_INVENTORY_END;
        public const int SIZE = 4;
        public PacketHeader Header => HEADER;

        public byte InvType;
        public byte Flag;

        public void Read(MemoryStreamReader br, int size) {
            InvType = (byte) br.ReadByte();
            Flag = (byte) br.ReadByte();
        }
    }
}
