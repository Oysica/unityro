using System.Text;

namespace ROIO.Utils.Extensions
{
    public static class StringExtensions
    {

        /// <summary>
        /// Text encoding the TW server and client data use (Big5). Strings are kept as their raw
        /// bytes, one char per byte, so paths built from them still match the GRF; only text shown
        /// to the player is decoded, and text sent to the server is encoded with this.
        /// </summary>
        public static readonly Encoding ClientEncoding = Encoding.GetEncoding(950);

        private static readonly Encoding Cp1252 = Encoding.GetEncoding(1252);

        public static string KoreanTo1252(this string str) => Encoding.GetEncoding(1252).GetString(Encoding.GetEncoding(949).GetBytes(str));

        /// <summary>
        /// Readable text from a network string (MemoryStreamReader reads one char per byte).
        /// </summary>
        public static string NetworkToText(this string raw) {
            if (string.IsNullOrEmpty(raw)) {
                return raw;
            }

            var bytes = new byte[raw.Length];
            for (var i = 0; i < raw.Length; i++) {
                if (raw[i] > 0xFF) {
                    return raw; // already decoded
                }
                bytes[i] = (byte) raw[i];
            }
            return ClientEncoding.GetString(bytes);
        }

        /// <summary>
        /// Readable text from a Lua table string (the tables are extracted as CP1252 text).
        /// </summary>
        public static string LuaToText(this string raw) {
            return string.IsNullOrEmpty(raw) ? raw : ClientEncoding.GetString(Cp1252.GetBytes(raw));
        }

    }
}