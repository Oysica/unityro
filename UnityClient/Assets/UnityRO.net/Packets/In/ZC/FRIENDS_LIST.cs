using ROIO.Utils;
using System.Collections.Generic;

public partial class ZC {

    // 0201 <len>.W { <account id>.L <char id>.L <name>.24B }* (names again since 2020-09-02)
    [PacketHandler(HEADER, "ZC_FRIENDS_LIST", SIZE)]
    public class FRIENDS_LIST : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_FRIENDS_LIST;
        public const int SIZE = -1;
        public PacketHeader Header => HEADER;

        public List<FriendListItem> FriendList;

        public void Read(MemoryStreamReader br, int size) {
            // This packet's entries only, not whatever else the reader holds
            int n = size / FriendListItem.BLOCK_SIZE;
            FriendList = new List<FriendListItem>();

            for (int i = 0; i < n; i++) {
                FriendList.Add(new FriendListItem(br));
            }
        }
    }
}
