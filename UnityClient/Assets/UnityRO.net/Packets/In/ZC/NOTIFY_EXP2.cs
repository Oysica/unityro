using ROIO.Utils;
using System;
public partial class ZC {

    [PacketHandler(HEADER, "ZC_NOTIFY_EXP2", SIZE)]
    public class NOTIFY_EXP2 : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_NOTIFY_EXP2;
        public PacketHeader Header => HEADER;
        public const int SIZE = 18;

        public uint id;
        public long exp;
        public short expType;
        public short questExp;

        // 0acc <account id>.L <exp>.Q <type>.W <quest>.W (clif.cpp clif_displayexp, PACKETVER >= 20170830):
        // the exp is 8 bytes, which read as 4 left the type read off the wrong bytes
        public void Read(MemoryStreamReader br, int size) {
            id = br.ReadUInt();
            exp = br.ReadLong(); //negative if losing
            expType = br.ReadShort(); //SP_BASEEXP, SP_JOBEXP
            questExp = br.ReadShort();
        }
    }
}
