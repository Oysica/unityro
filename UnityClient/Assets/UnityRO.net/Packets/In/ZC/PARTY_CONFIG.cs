using ROIO.Utils;

public partial class ZC {

    // Whether party invites are refused, sent at login (clif.cpp clif_partyinvitationstate)
    // 02c9 <refuse>.B
    [PacketHandler(HEADER, "ZC_PARTY_CONFIG", SIZE)]
    public class PARTY_CONFIG : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_PARTY_CONFIG;
        public const int SIZE = 3;
        public PacketHeader Header => HEADER;

        public bool RefuseInvites;

        public void Read(MemoryStreamReader br, int size) {
            RefuseInvites = br.ReadByte() != 0;
        }
    }
}
