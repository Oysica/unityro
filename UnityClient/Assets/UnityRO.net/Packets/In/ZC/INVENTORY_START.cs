using ROIO.Utils;
using ROIO.Utils.Extensions;

public partial class ZC {

    // Starts a full item list of the inventory, cart or storage (clif.cpp clif_inventoryStart).
    // 0b08 <len>.W <type>.B <name>.?B
    [PacketHandler(HEADER, "ZC_INVENTORY_START")]
    public class INVENTORY_START : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_INVENTORY_START;
        public PacketHeader Header => HEADER;

        // 0 inventory, 1 cart, 2 storage, 3 guild storage (clif.cpp enum inventory_type)
        public byte InvType;
        public string Name;

        public void Read(MemoryStreamReader br, int size) {
            InvType = (byte) br.ReadByte();
            Name = br.ReadBinaryString(br.Length - br.Position).NetworkToText();
        }
    }
}
