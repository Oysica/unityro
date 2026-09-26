using ROIO.Utils;

public partial class ZC {

    // A skill left on the ground in sight, one cell of it (clif.cpp clif_getareachar_skillunit).
    // 09ca <len>.W <id>.L <creator id>.L <x>.W <y>.W <unit id>.L <range>.B <visible>.B <skill level>.B
    [PacketHandler(HEADER, "ZC_SKILL_ENTRY5", SIZE)]
    public class SKILL_ENTRY5 : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_SKILL_ENTRY5;
        public const int SIZE = -1;
        public PacketHeader Header => HEADER;

        public uint AID;
        public uint CreatorAID;
        public short X;
        public short Y;
        public int UnitId;      // skill.hpp e_skill_unit_id: UNT_SAFETYWALL, UNT_PNEUMA, ...
        public byte Range;
        public bool Visible;
        public byte SkillLevel;

        public void Read(MemoryStreamReader br, int size) {
            AID = br.ReadUInt();
            CreatorAID = br.ReadUInt();
            X = br.ReadShort();
            Y = br.ReadShort();
            UnitId = br.ReadInt();
            Range = (byte) br.ReadByte();
            Visible = br.ReadByte() != 0;
            SkillLevel = (byte) br.ReadByte();
        }
    }
}
