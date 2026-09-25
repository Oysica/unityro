public partial class CZ {

    // Uses a skill on a cell (clif.cpp clif_parse_UseSkillToPos)
    // 0366 <skill lv>.W <skill id>.W <x>.W <y>.W
    public class USE_SKILL_TOGROUND2 : OutPacket {

        public const PacketHeader HEADER = PacketHeader.CZ_USE_SKILL_TOGROUND2;
        public const int SIZE = 10;

        private short level;
        private short skillId;
        private short x;
        private short y;

        public USE_SKILL_TOGROUND2(short skillId, short level, int x, int y) : base(HEADER, SIZE) {
            this.skillId = skillId;
            this.level = level;
            this.x = (short) x;
            this.y = (short) y;
        }

        public override void Send() {
            Write(level);
            Write(skillId);
            Write(x);
            Write(y);

            base.Send();
        }
    }
}
