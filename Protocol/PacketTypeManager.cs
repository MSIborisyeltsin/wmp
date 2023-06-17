using System;
using System.Collections.Generic;

namespace Protocol
{
    public static class PacketTypeManager
    {
        private static readonly Dictionary<PacketType, Tuple<byte, byte>> TypeDictionary =
            new Dictionary<PacketType, Tuple<byte, byte>>();

        static PacketTypeManager()
        {
            RegisterType(PacketType.Handshake, 1, 0);
        }

        public static void RegisterType(PacketType type, byte btype, byte bsubtype)
        {
            if (TypeDictionary.ContainsKey(type))
            {
                throw new Exception($"Пакет типа {type:G} вже зареган.");
            }

            TypeDictionary.Add(type, Tuple.Create(btype, bsubtype));
        }

        public static Tuple<byte, byte> GetType(PacketType type)
        {
            if (!TypeDictionary.ContainsKey(type))
            {
                throw new Exception($"Пакет типа {type:G} ще не зареган.");
            }

            return TypeDictionary[type];
        }

        public static PacketType GetTypeFromPacket(Packet packet)
        {
            var type = packet.PacketType;
            var subtype = packet.PacketSubtype;

            foreach (var tuple in TypeDictionary)
            {
                var value = tuple.Value;

                if (value.Item1 == type && value.Item2 == subtype)
                {
                    return tuple.Key;
                }
            }

            return PacketType.Unknown;
        }
    }
}
