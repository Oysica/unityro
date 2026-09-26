using ROIO.Utils;

public partial class ZC {

    // A skill's cell on the ground gone, or out of sight (clif.cpp clif_skill_delunit,
    // clif_clearchar_skillunit).
    // 0120 <id>.L
    [PacketHandler(HEADER, "ZC_SKILL_DISAPPEAR", SIZE)]
    public class SKILL_DISAPPEAR : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_SKILL_DISAPPEAR;
        public const int SIZE = 6;
        public PacketHeader Header => HEADER;

        public uint AID;

        public void Read(MemoryStreamReader br, int size) {
            AID = br.ReadUInt();
        }
    }
}
