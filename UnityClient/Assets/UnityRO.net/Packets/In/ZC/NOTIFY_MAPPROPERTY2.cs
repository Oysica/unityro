using ROIO.Utils;

public partial class ZC {

    // The map's rules, sent on entering it (clif.cpp clif_map_property).
    // 099b <type>.W <flags>.L: bit 0 PvP (anyone out of the party may be attacked), bit 1 GvG,
    // battlegrounds or a guild war (anyone out of the guild), bit 4 players only with shift, ...
    [PacketHandler(HEADER, "ZC_NOTIFY_MAPPROPERTY2", SIZE)]
    public class NOTIFY_MAPPROPERTY2 : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_NOTIFY_MAPPROPERTY2;
        public PacketHeader Header => HEADER;
        public const int SIZE = 8;

        public short Type;
        public int Flags;

        public void Read(MemoryStreamReader br, int size) {
            Type = br.ReadShort();
            Flags = br.ReadInt();
        }
    }
}
