using ROIO.Utils;

public partial class ZC {

    // Someone's casting was interrupted (clif.cpp clif_skillcastcancel), sent to those around.
    // 01b9 <id>.L
    [PacketHandler(HEADER, "ZC_DISPEL", SIZE)]
    public class DISPEL : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_DISPEL;
        public const int SIZE = 6;
        public PacketHeader Header => HEADER;

        public uint AID;

        public void Read(MemoryStreamReader br, int size) {
            AID = br.ReadUInt();
        }
    }
}
