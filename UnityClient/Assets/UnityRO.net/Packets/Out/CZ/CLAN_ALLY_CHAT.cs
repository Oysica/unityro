public partial class CZ {

    // Clan (代表公會) chat; "name : message", as for public chat (clif_parse_clan_chat).
    // 098d <len>.W <message>.?B
    public class CLAN_CHAT : OutPacket {

        private readonly string message;

        public CLAN_CHAT(string message) : base(PacketHeader.CZ_CLAN_CHAT, -1) {
            this.message = $"{Session.CurrentSession.Entity.GetBaseStatus().name} : {message}";
        }

        public override void Send() {
            Write(message);
            base.Send();
        }
    }

    // Guild alliance (公會聯盟) chat; "name : message" (Pandas clif_parse_AllyMessage).
    // 0bdd <len>.W <message>.?B
    public class ALLY_CHAT : OutPacket {

        private readonly string message;

        public ALLY_CHAT(string message) : base(PacketHeader.CZ_ALLY_CHAT, -1) {
            this.message = $"{Session.CurrentSession.Entity.GetBaseStatus().name} : {message}";
        }

        public override void Send() {
            Write(message);
            base.Send();
        }
    }
}
