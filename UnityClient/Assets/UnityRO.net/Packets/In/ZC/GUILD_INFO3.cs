using ROIO.Utils;

public partial class ZC {

    // Official ZC_GUILD_INFO for PACKETVER >= 20200902 (server struct
    // PACKET_ZC_GUILD_INFO, packets_struct.hpp:4995). This client has no
    // guild UI, so this is a pure parse-and-discard registration - without
    // it the packet stream desyncs (UnityRO's packet catalog predates this
    // PACKETVER range). Wire size: int16(2) + 11*int(44) + guildname[24] +
    // manageLand[16] + zeny(4) + masterGID(4) + masterName[24] = 118 bytes
    // total, computed field-by-field against the server struct (not
    // guessed) since NAME_LENGTH/MAP_NAME_LENGTH_EXT aren't visible here.
    [PacketHandler(HEADER, "ZC_GUILD_INFO3", SIZE)]
    public class GUILD_INFO3 : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_GUILD_INFO3;
        public const int SIZE = 118;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
            // 116-byte body; nothing to act on (no guild UI in this client).
        }
    }
}
