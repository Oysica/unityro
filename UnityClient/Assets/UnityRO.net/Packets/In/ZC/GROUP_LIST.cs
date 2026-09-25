using ROIO.Utils;
using ROIO.Utils.Extensions;
using System.Collections.Generic;

public partial class ZC {

    // The whole party (clif.cpp clif_party_info, PACKET_ZC_GROUP_LIST): at login, on joining and on
    // changes. 0ae5 <len>.W <party name>.24B { <account id>.L <char id>.L <name>.24B <map>.16B
    // <leader: 0 yes>.B <offline>.B <job>.W <base level>.W }*
    [PacketHandler(HEADER, "ZC_GROUP_LIST", SIZE)]
    public class GROUP_LIST : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_GROUP_LIST;
        public const int SIZE = -1;
        private const int MEMBER_SIZE = 54;
        public PacketHeader Header => HEADER;

        public class Member {
            public uint AID;
            public uint GID;
            public string Name;
            public string Map;
            public bool IsLeader;
            public bool IsOnline;
            public short Job;
            public short BaseLevel;
        }

        public string PartyName;
        public List<Member> Members = new List<Member>();

        public void Read(MemoryStreamReader br, int size) {
            PartyName = br.ReadBinaryString(24).NetworkToText();
            var count = (size - 24) / MEMBER_SIZE;
            for (var i = 0; i < count; i++) {
                Members.Add(new Member {
                    AID = br.ReadUInt(),
                    GID = br.ReadUInt(),
                    Name = br.ReadBinaryString(24).NetworkToText(),
                    Map = br.ReadBinaryString(16),
                    IsLeader = br.ReadByte() == 0,
                    IsOnline = br.ReadByte() == 0,
                    Job = br.ReadShort(),
                    BaseLevel = br.ReadShort()
                });
            }
        }
    }
}
