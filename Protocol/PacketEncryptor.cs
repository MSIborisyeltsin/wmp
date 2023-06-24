namespace Protocol
{
    public class ProtocolEncryptor
    {
        private static string Key { get; } = "md752v2bwyvn68drkqgy3pxe0";

        public static byte[] Encrypt(byte[] data)
        {
            return RijndaelHandler.Encrypt(data, Key);
        }

        public static byte[] Decrypt(byte[] data)
        {
            return RijndaelHandler.Decrypt(data, Key);
        }
    }
}
