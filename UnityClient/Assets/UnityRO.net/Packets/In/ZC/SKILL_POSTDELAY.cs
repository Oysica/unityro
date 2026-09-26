using ROIO.Utils;

public partial class ZC {

    // One of our skills can't be used again for a while, its own cooldown (clif.cpp
    // clif_skill_cooldown); only the one using it gets it.
    // 043d <skill id>.W <msec>.L
    [PacketHandler(HEADER, "ZC_SKILL_POSTDELAY", SIZE)]
    public class SKILL_POSTDELAY : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_SKILL_POSTDELAY;
        public const int SIZE = 8;
        public PacketHeader Header => HEADER;

        public ushort SkillId;
        public uint DelayMs;

        public void Read(MemoryStreamReader br, int size) {
            SkillId = br.ReadUShort();
            DelayMs = br.ReadUInt();
        }
    }
}
