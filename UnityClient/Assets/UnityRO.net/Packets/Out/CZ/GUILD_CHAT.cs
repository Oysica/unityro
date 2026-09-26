public partial class CZ {

    // Guild chat; the server wants "name : message", as for public chat (clif_parse_GuildMessage).
    // 017e <len>.W <message>.?B
    public class GUILD_CHAT : OutPacket {

        private readonly string message;

        public GUILD_CHAT(string message) : base(PacketHeader.CZ_GUILD_CHAT, -1) {
            this.message = $"{Session.CurrentSession.Entity.GetBaseStatus().name} : {message}";
        }

        public override void Send() {
            Write(message);
            base.Send();
        }
    }
}
