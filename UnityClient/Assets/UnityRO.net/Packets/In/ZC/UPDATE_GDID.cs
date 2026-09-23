using ROIO.Utils;

public partial class ZC {

    // Official ZC_UPDATE_GDID for PACKETVER_MAIN_NUM >= 20220216 (server
    // struct PACKET_ZC_UPDATE_GDID, packets_struct.hpp:5564). This client
    // has no guild UI, so this is a pure parse-and-discard registration -
    // same reasoning as GUILD_INFO3.cs. Wire size: int16(2) + uint32(4) +
    // int(4) + uint32(4) + uint8(1) + int32(4) + guildName[24] +
    // uint32(4) = 47 bytes total, computed field-by-field against the
    // server struct.
    [PacketHandler(HEADER, "ZC_UPDATE_GDID", SIZE)]
    public class UPDATE_GDID : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_UPDATE_GDID;
        public const int SIZE = 47;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
            // 45-byte body; nothing to act on (no guild UI in this client).
        }
    }
}
