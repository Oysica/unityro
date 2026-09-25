using ROIO.Utils;

public partial class ZC {

    // How full the storage is (clif.cpp clif_updatestorageamount).
    // 00f2 <amount>.W <max amount>.W
    [PacketHandler(HEADER, "ZC_NOTIFY_STOREITEM_COUNTINFO", SIZE)]
    public class NOTIFY_STOREITEM_COUNTINFO : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_NOTIFY_STOREITEM_COUNTINFO;
        public const int SIZE = 6;
        public PacketHeader Header => HEADER;

        public ushort Amount;
        public ushort MaxAmount;

        public void Read(MemoryStreamReader br, int size) {
            Amount = br.ReadUShort();
            MaxAmount = br.ReadUShort();
        }
    }
}
