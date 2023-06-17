using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Protocol
{
    public class Packet
    {
        public byte PacketType { get; private set; }
        public byte PacketSubtype { get; private set; }
        public List<PacketField> Fields { get; set; } = new List<PacketField>();
        public bool Protected { get; set; }
        private bool ChangeHeaders { get; set; }

        private Packet() {}

        public PacketField GetField(byte id)
        {
            foreach (var field in Fields)
            {
                if (field.FieldID == id)
                {
                    return field;
                }
            }

            return null;
        }

        public bool HasField(byte id)
        {
            return GetField(id) != null;
        }

        private T ByteArrayToFixedObject<T>(byte[] bytes) where T: struct 
        {
            T structure;
            
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            
            try
            {
                structure = (T) Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }

            return structure;
        }

        public byte[] FixedObjectToByteArray(object value)
        {
            var rawsize = Marshal.SizeOf(value);
            var rawdata = new byte[rawsize];

            var handle =
                GCHandle.Alloc(rawdata,
                    GCHandleType.Pinned);

            Marshal.StructureToPtr(value,
                handle.AddrOfPinnedObject(),
                false);

            handle.Free();
            return rawdata;
        }

        public T GetValue<T>(byte id) where T : struct
        {
            var field = GetField(id);

            if (field == null)
            {
                throw new Exception($"Поле с идентификатором {id} не найдено :(");
            }

            var neededSize = Marshal.SizeOf(typeof(T));

            if (field.FieldSize != neededSize)
            {
                throw new Exception($"Невозможно преобразовать поле в тип {typeof(T).FullName}.\n" +
                                    $"У нас есть {field.FieldSize} байт, но нам надо ровно {neededSize}");
            }

            return ByteArrayToFixedObject<T>(field.Contents);
        }

        public void SetValue(byte id, object structure)
        {
            if (!structure.GetType().IsValueType)
            {
                throw new Exception("Доступны только типы значений.");
            }

            var field = GetField(id);

            if (field == null)
            {
                field = new PacketField
                {
                    FieldID = id
                };

                Fields.Add(field);
            }

            var bytes = FixedObjectToByteArray(structure);

            if (bytes.Length > byte.MaxValue)
            {
                throw new Exception("Объект слишком большой. Максимальная длина 255 байт.");
            }

            field.FieldSize = (byte) bytes.Length;
            field.Contents = bytes;
        }

        public byte[] GetValueRaw(byte id)
        {
            var field = GetField(id);

            if (field == null)
            {
                throw new Exception($"Поле с идентификатором {id} не найдено.");
            }

            return field.Contents;
        }

        public void SetValueRaw(byte id, byte[] rawData)
        {
            var field = GetField(id);

            if (field == null)
            {
                field = new PacketField
                {
                    FieldID = id
                };

                Fields.Add(field);
            }

            if (rawData.Length > byte.MaxValue)
            {
                throw new Exception("Объект слишком большой. Максимальная длина 255 байт.");
            }

            field.FieldSize = (byte) rawData.Length;
            field.Contents = rawData;
        }

        public static Packet Create(PacketType type)
        {
            var t = XPacketTypeManager.GetType(type);
            return Create(t.Item1, t.Item2);
        }

        public static Packet Create(byte type, byte subtype)
        {
            return new Packet
            {
                PacketType = type,
                PacketSubtype = subtype
            };
        }

        public byte[] ToPacket()
        {
            var packet = new MemoryStream();

            packet.Write(
                ChangeHeaders
                    ? new byte[] {0x95, 0xAA, 0xFF, PacketType, PacketSubtype}
                    : new byte[] {0xC0, 0xFF, 0xEE, PacketType, PacketSubtype}, 0, 5);

            // Сортируем поля по ID
            var fields = Fields.OrderBy(field => field.FieldID);

            // Записываем поля
            foreach (var field in fields)
            {
                packet.Write(new[] {field.FieldID, field.FieldSize}, 0, 2);
                packet.Write(field.Contents, 0, field.Contents.Length);
            }

            // Записываем конец пакета
            packet.Write(new byte[] {0xFF, 0x00}, 0, 2);

            return packet.ToArray();
        }

        public static Packet Parse(byte[] packet, bool markAsEncrypted = false)
        {
            /*
             * Минимальный размер пакета - 7 байт
             * HEADER (3) + TYPE (1) + SUBTYPE (1) + PACKET ENDING (2)
             */
            if (packet.Length < 7)
            {
                return null;
            }

            var encrypted = false;

            // Проверяем заголовок
            if (packet[0] != 0xC0 ||
                packet[1] != 0xFF ||
                packet[2] != 0xEE)
            {
                if (packet[0] == 0x95 ||
                    packet[1] == 0xAA ||
                    packet[2] == 0xFF)
                {
                    encrypted = true;
                }
                else
                {
                    return null;
                }
            }

            var mIndex = packet.Length - 1;

            // Проверяем, что бы пакет заканчивался нужными байтами
            if (packet[mIndex - 1] != 0xFF ||
                packet[mIndex] != 0x00)
            {
                return null;
            }

            var type = packet[3];
            var subtype = packet[4];

            var packet = new XPacket {PacketType = type, PacketSubtype = subtype, Protected = markAsEncrypted};
            
            var fields = packet.Skip(5).ToArray();
            
            while (true)
            {
                if (fields.Length == 2) // Остались последние два байта, завершающие пакет.
                {
                    return encrypted ? DecryptPacket(packet) : packet;
                }

                var id = fields[0];
                var size = fields[1];

                var contents = size != 0 ?
                    fields.Skip(2).Take(size).ToArray() : null;

                xpacket.Fields.Add(new PacketField
                {
                    FieldID = id,
                    FieldSize = size,
                    Contents = contents
                });

                fields = fields.Skip(2 + size).ToArray();
            }
        }

        public static Packet EncryptPacket(Packet packet)
        {
            if (packet == null)
            {
                return null; // Нам попросту нехуй шифровать
            }

            var rawBytes = packet.ToPacket();                     // получаем пакет в байтах
            var encrypted = ProtocolEncryptor.Encrypt(rawBytes); // шифруем его

            var p = Create(0, 0);           // создаем пакет
            p.SetValueRaw(0, encrypted);    // записываем данные
            p.ChangeHeaders = true;         // помечаем, что нам нужен другой заголовок

            return p;
        }

        public Packet Encrypt()
        {
            return EncryptPacket(this);
        }

        public Packet Decrypt()
        {
            return DecryptPacket(this);
        }

        private static Packet DecryptPacket(Packet packet)
        {
            if (!packet.HasField(0))
            {
                return null; // Зашифрованные данные должны быть в 0 поле
            }

            var rawData = packet.GetValueRaw(0); // получаем зашифрованный пакет
            var decrypted = ProtocolEncryptor.Decrypt(rawData);

            return Parse(decrypted, true);
        }
    }
}
