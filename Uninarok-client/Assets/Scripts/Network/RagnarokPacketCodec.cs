using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Uninarok.Networking
{
    public enum RagnarokPacketId : ushort
    {
        Login = 0x0064,
        CharacterServerEnter = 0x0065,
        SelectCharacter = 0x0066,
        CreateCharacter = 0x0A39,
        CharacterCreated = 0x006D,
        CharacterCreateRejected = 0x006E,
        CharacterServerRejected = 0x006C,
        LoginRejected = 0x006A,
        LoginAccepted = 0x0AC4,
        CharacterList = 0x099D,
        RequestCharacterList = 0x09A1,
        MapServerRedirect = 0x0AC5,
        MapEnter = 0x0436,
        MapEnterAccepted = 0x02EB,
        MapEnterRejected = 0x0074
    }

    public readonly struct RagnarokPacket
    {
        public readonly ushort Id;
        public readonly int Length;
        public readonly byte[] Buffer;

        public RagnarokPacket(ushort id, int length, byte[] buffer)
        {
            Id = id;
            Length = length;
            Buffer = buffer;
        }
    }

    public readonly struct RagnarokServerEndpoint
    {
        public readonly IPAddress Address;
        public readonly int Port;

        public RagnarokServerEndpoint(IPAddress address, int port)
        {
            Address = address;
            Port = port;
        }
    }

    public static class RagnarokPacketCodec
    {
        public const int PacketVersion = 20190530;
        public const int CharacterInfoLength = 147;
        public const int LoginListOffset = 64;
        public const int LoginServerEntryLength = 160;
        private static readonly short[] PacketLengths = CreatePacketLengths();

        private static short[] CreatePacketLengths()
        {
            var lengths = new short[ushort.MaxValue + 1];
            lengths[Packets.CA.LOGIN.PacketId] = Packets.CA.LOGIN.Size;
            lengths[0x0065] = 17;
            lengths[0x0066] = 3;
            lengths[0x006A] = 23;
            lengths[0x006C] = 3;
            lengths[0x006D] = 149;
            lengths[0x006E] = 3;
            lengths[0x0074] = 3;
            lengths[0x0081] = 3;
            lengths[0x083E] = 27;
            lengths[0x099D] = -1;
            lengths[0x09A1] = 2;
            lengths[0x0A39] = 36;
            lengths[0x0AC4] = -1;
            lengths[0x0AC5] = 156;
            lengths[0x0B02] = 27;
            lengths[0x02EB] = 13;
            return lengths;
        }

        public static int GetPacketLength(ushort id) => PacketLengths[id];

        public static void WriteLogin(Span<byte> packet, string username, string password)
        {
            var packetLength = GetPacketLength(Packets.CA.LOGIN.PacketId);
            RequireLength(packet, packetLength);
            packet.Slice(0, packetLength).Clear();
            var loginPack = new Packets.CA.LOGIN
            {
                AccountName = new Unity.Collections.FixedString32Bytes(username),
                AccountPassword = new Unity.Collections.FixedString32Bytes(password)
            };
            loginPack.Serialize(packet);

            //BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)RagnarokPacketId.Login);
            //BinaryPrimitives.WriteUInt32LittleEndian(packet.Slice(2), PacketVersion);
            //WriteFixedAscii(packet.Slice(6, 24), username);
            //WriteFixedAscii(packet.Slice(30, 24), password);
        }

        public static void WriteCharacterServerEnter(Span<byte> packet, uint accountId, int authCode, uint userLevel, byte sex)
        {
            RequireLength(packet, 17);
            packet.Slice(0, 17).Clear();
            BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)RagnarokPacketId.CharacterServerEnter);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.Slice(2), accountId);
            BinaryPrimitives.WriteInt32LittleEndian(packet.Slice(6), authCode);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.Slice(10), userLevel);
            packet[16] = sex;
        }

        public static void WriteRequestCharacterList(Span<byte> packet)
        {
            RequireLength(packet, 2);
            BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)RagnarokPacketId.RequestCharacterList);
        }

        public static void WriteCreateCharacter(Span<byte> packet, string name, byte slot, ushort hairColor, ushort hairStyle, ushort jobClass, byte sex)
        {
            RequireLength(packet, 36);
            packet.Slice(0, 36).Clear();
            BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)RagnarokPacketId.CreateCharacter);
            WriteFixedAscii(packet.Slice(2, 24), name);
            packet[26] = slot;
            BinaryPrimitives.WriteUInt16LittleEndian(packet.Slice(27), hairColor);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.Slice(29), hairStyle);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.Slice(31), jobClass);
            packet[35] = sex;
        }

        public static void WriteSelectCharacter(Span<byte> packet, byte slot)
        {
            RequireLength(packet, 3);
            BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)RagnarokPacketId.SelectCharacter);
            packet[2] = slot;
        }

        public static void WriteMapEnter(Span<byte> packet, uint accountId, uint characterId, int authCode, uint tick, byte sex)
        {
            RequireLength(packet, 19);
            packet.Slice(0, 19).Clear();
            BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)RagnarokPacketId.MapEnter);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.Slice(2), accountId);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.Slice(6), characterId);
            BinaryPrimitives.WriteInt32LittleEndian(packet.Slice(10), authCode);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.Slice(14), tick);
            packet[18] = sex;
        }

        public static bool TryGetCharacterServer(byte[] packet, int length, int serverIndex, out RagnarokServerEndpoint endpoint)
        {
            endpoint = default;
            if (length < LoginListOffset || packet.Length < length || packet[0] != 0xC4 || packet[1] != 0x0A)
                return false;

            int serverCount = (length - LoginListOffset) / LoginServerEntryLength;
            if (serverCount == 0)
                return false;

            int index = serverIndex >= 0 && serverIndex < serverCount ? serverIndex : 0;
            int offset = LoginListOffset + index * LoginServerEntryLength;
            var address = new IPAddress(new[] { packet[offset], packet[offset + 1], packet[offset + 2], packet[offset + 3] });
            int port = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(offset + 4, 2));
            if (port == 0)
                return false;
            endpoint = new RagnarokServerEndpoint(address, port);
            return true;
        }

        public static int GetCharacterCount(byte[] packet, int length)
        {
            if (packet.Length < length || length < 4 || packet[0] != 0x9D || packet[1] != 0x09)
                throw new InvalidDataException("Invalid character list packet.");
            int payloadLength = length - 4;
            if (payloadLength % CharacterInfoLength != 0)
                throw new InvalidDataException("Character list entry size does not match Renewal PACKETVER 20190530.");
            return payloadLength / CharacterInfoLength;
        }

        public static byte FindCharacterSlot(byte[] packet, int length, byte requestedSlot)
        {
            int count = GetCharacterCount(packet, length);
            if (count == 0)
                throw new InvalidDataException("The character list is empty.");

            byte firstSlot = 0;
            for (int i = 0; i < count; i++)
            {
                int offset = 4 + i * CharacterInfoLength + 104;
                ushort slot = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(offset, 2));
                if (i == 0)
                    firstSlot = (byte)slot;
                if (slot == requestedSlot)
                    return requestedSlot;
            }
            return firstSlot;
        }

        public static bool TryGetMapEndpoint(byte[] packet, int length, out RagnarokServerEndpoint endpoint)
        {
            endpoint = default;
            if (length < 28 || packet.Length < length || packet[0] != 0xC5 || packet[1] != 0x0A)
                return false;
            var address = new IPAddress(new[] { packet[22], packet[23], packet[24], packet[25] });
            int port = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(26, 2));
            if (port == 0)
                return false;
            endpoint = new RagnarokServerEndpoint(address, port);
            return true;
        }

        public static uint ReadAccountId(byte[] packet) => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(8, 4));
        public static int ReadAuthCode(byte[] packet) => BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(4, 4));
        public static uint ReadUserLevel(byte[] packet) => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(12, 4));
        public static byte ReadSex(byte[] packet) => packet[46];
        public static uint ReadCharacterId(byte[] packet) => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(2, 4));
        public static byte ReadCreatedCharacterSlot(byte[] packet) => (byte)BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(106, 2));

        private static void WriteFixedAscii(Span<byte> destination, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;
            int count = Math.Min(value.Length, destination.Length);
            for (int i = 0; i < count; i++)
                destination[i] = value[i] <= 0x7F ? (byte)value[i] : (byte)'?';
        }

        private static void RequireLength(Span<byte> packet, int required)
        {
            if (packet.Length < required)
                throw new ArgumentException("The packet buffer is too small.", nameof(packet));
        }
    }

    internal sealed class RagnarokTcpConnection : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly byte[] _buffer = new byte[ushort.MaxValue];

        private RagnarokTcpConnection(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
        }

        public static async Task<RagnarokTcpConnection> ConnectAsync(string host, int port, CancellationToken token)
        {
            var client = new TcpClient { NoDelay = true };
            try
            {
                Task connectTask = client.ConnectAsync(host, port);
                Task cancellationTask = Task.Delay(Timeout.Infinite, token);
                if (await Task.WhenAny(connectTask, cancellationTask).ConfigureAwait(false) != connectTask)
                {
                    client.Dispose();
                    try
                    {
                        await connectTask.ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                    token.ThrowIfCancellationRequested();
                }

                await connectTask.ConfigureAwait(false);
                return new RagnarokTcpConnection(client);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        public Task SendAsync(byte[] buffer, int length, CancellationToken token)
        {
            return _stream.WriteAsync(buffer, 0, length, token);
        }

        public async Task<RagnarokPacket> ReadPacketAsync(CancellationToken token)
        {
            await ReadExactlyAsync(0, 2, token).ConfigureAwait(false);
            ushort id = BinaryPrimitives.ReadUInt16LittleEndian(_buffer);
            int length = RagnarokPacketCodec.GetPacketLength(id);
            bool variable = length == -1;
            if (variable)
            {
                await ReadExactlyAsync(2, 2, token).ConfigureAwait(false);
                length = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(2, 2));
                if (length < 4)
                    throw new InvalidDataException($"Invalid packet length {length} for 0x{id:X4}.");
            }
            else if (length == 0)
            {
                throw new InvalidDataException($"Unsupported packet 0x{id:X4}.");
            }

            if (length > _buffer.Length)
                throw new InvalidDataException($"Packet 0x{id:X4} exceeds the receive buffer.");
            int alreadyRead = variable ? 4 : 2;
            await ReadExactlyAsync(alreadyRead, length - alreadyRead, token).ConfigureAwait(false);
            return new RagnarokPacket(id, length, _buffer);
        }

        public async Task<ushort> ReadPacketHeaderAsync(CancellationToken token)
        {
            await ReadExactlyAsync(0, 2, token).ConfigureAwait(false);
            return BinaryPrimitives.ReadUInt16LittleEndian(_buffer);
        }

        private async Task ReadExactlyAsync(int offset, int count, CancellationToken token)
        {
            while (count > 0)
            {
                int received = await _stream.ReadAsync(_buffer, offset, count, token).ConfigureAwait(false);
                if (received == 0)
                    throw new EndOfStreamException("The server closed the connection.");
                offset += received;
                count -= received;
            }
        }

        public void Dispose()
        {
            _stream.Dispose();
            _client.Dispose();
        }
    }
}
