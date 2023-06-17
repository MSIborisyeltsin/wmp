using Protocol.Serializator;

namespace Protocol
{
    public class PacketHandshake
    {
        [XField(1)]
        public int MagicHandshakeNumber;
    }
}
