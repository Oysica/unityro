// A whisper (clif.cpp clif_parse_WisMessage). 0096 <len>.W <target>.24B <message>.?B
public partial class CZ {

    public class WHISPER : OutPacket {

        private readonly string target;
        private readonly string message;

        public WHISPER(string target, string message) : base(PacketHeader.CZ_WHISPER, -1) {
            this.target = target;
            this.message = message;
        }

        public override void Send() {
            // The name must end in a zero byte: 23 bytes of it at most
            var name = ROIO.Utils.Extensions.StringExtensions.ClientEncoding.GetBytes(target);
            var chunk = new byte[24];
            System.Array.Copy(name, chunk, System.Math.Min(name.Length, 23));
            Write(chunk);
            Write(message);
            Write((byte) 0);
            base.Send();
        }
    }
}
