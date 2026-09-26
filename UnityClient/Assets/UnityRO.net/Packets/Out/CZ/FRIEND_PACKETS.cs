// Friend list requests (clif.cpp clif_parse_FriendsListAdd, FriendsListRemove, FriendsListReply)
public partial class CZ {

    // Asks them to be our friend. 0202 <name>.24B
    public class ADD_FRIENDS : OutPacket {
        public const int SIZE = 26;

        private readonly string name;

        public ADD_FRIENDS(string name) : base(PacketHeader.CZ_ADD_FRIENDS, SIZE) {
            this.name = name;
        }

        public override void Send() {
            // The name must end in a zero byte: 23 bytes of it at most
            var bytes = ROIO.Utils.Extensions.StringExtensions.ClientEncoding.GetBytes(name);
            var chunk = new byte[24];
            System.Array.Copy(bytes, chunk, System.Math.Min(bytes.Length, 23));
            Write(chunk);
            base.Send();
        }
    }

    // 0203 <account id>.L <char id>.L
    public class DELETE_FRIENDS : OutPacket {
        public const int SIZE = 10;

        private readonly uint accountId;
        private readonly uint charId;

        public DELETE_FRIENDS(uint accountId, uint charId) : base(PacketHeader.CZ_DELETE_FRIENDS, SIZE) {
            this.accountId = accountId;
            this.charId = charId;
        }

        public override void Send() {
            Write(accountId);
            Write(charId);
            base.Send();
        }
    }

    // Our answer to a friend request. 0208 <their account id>.L <their char id>.L <reply: 0 refuse, 1 accept>.L
    public class ACK_REQ_ADD_FRIENDS : OutPacket {
        public const int SIZE = 14;

        private readonly uint accountId;
        private readonly uint charId;
        private readonly bool accept;

        public ACK_REQ_ADD_FRIENDS(uint accountId, uint charId, bool accept) : base(PacketHeader.CZ_ACK_REQ_ADD_FRIENDS, SIZE) {
            this.accountId = accountId;
            this.charId = charId;
            this.accept = accept;
        }

        public override void Send() {
            Write(accountId);
            Write(charId);
            Write(accept ? 1 : 0);
            base.Send();
        }
    }
}
