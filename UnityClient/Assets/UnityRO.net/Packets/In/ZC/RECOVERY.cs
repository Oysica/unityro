using ROIO.Utils;

public partial class ZC {

    // HP/SP an item or regeneration effect gave back (clif.cpp clif_heal); only the one healed gets it.
    // 0a27 <type>.W <amount>.L
    [PacketHandler(HEADER, "ZC_RECOVERY", SIZE)]
    public class RECOVERY : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_RECOVERY;
        public const int SIZE = 8;
        public PacketHeader Header => HEADER;

        // EntityStatus.SP_HP or SP_SP
        public ushort Type;
        public int Amount;

        public void Read(MemoryStreamReader br, int size) {
            Type = br.ReadUShort();
            Amount = br.ReadInt();
        }
    }
}
