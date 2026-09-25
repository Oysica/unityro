using ROIO.Utils;

public partial class ZC {

    // The party's sharing (clif.cpp clif_party_option)
    // 07d8 <exp: 0 each, 1 shared, 2 not allowed>.L <item pickup share>.B <item loot share>.B
    [PacketHandler(HEADER, "ZC_REQ_GROUPINFO_CHANGE_V2", SIZE)]
    public class REQ_GROUPINFO_CHANGE_V2 : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_REQ_GROUPINFO_CHANGE_V2;
        public const int SIZE = 8;
        public PacketHeader Header => HEADER;

        public int ExpOption;
        public bool SharePickup;
        public bool ShareLoot;

        public void Read(MemoryStreamReader br, int size) {
            ExpOption = br.ReadInt();
            SharePickup = br.ReadByte() != 0;
            ShareLoot = br.ReadByte() != 0;
        }
    }
}
