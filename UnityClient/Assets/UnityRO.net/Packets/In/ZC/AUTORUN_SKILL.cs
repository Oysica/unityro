using ROIO.Utils;
using ROIO.Utils.Extensions;

public partial class ZC {

    // A skill an item hands the client to cast, as if picked from the skill list (clif.cpp
    // clif_item_skill): Fly Wing (Teleport 1), Butterfly Wing, magic scrolls. The item is only used
    // up once the client casts it.
    // 0147 <skill id>.W <type>.L <level>.W <sp cost>.W <range>.W <skill name>.24B <upgradable>.B
    [PacketHandler(HEADER, "ZC_AUTORUN_SKILL", SIZE)]
    public class AUTORUN_SKILL : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_AUTORUN_SKILL;
        public const int SIZE = 39;
        public PacketHeader Header => HEADER;

        public SkillInfo SkillInfo;

        public void Read(MemoryStreamReader br, int size) {
            SkillInfo = new SkillInfo {
                SkillID = br.ReadShort(),
                SkillType = br.ReadInt(),
                Level = br.ReadShort(),
                SpCost = br.ReadShort(),
                AttackRange = br.ReadShort(),
                SkillName = br.ReadBinaryString(24).NetworkToText()
            };
            SkillInfo.CanUpgrade = br.ReadByte() == 1;
        }
    }
}
