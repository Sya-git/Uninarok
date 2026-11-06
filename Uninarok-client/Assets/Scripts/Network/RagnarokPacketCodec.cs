using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Uninarok.Networking.Packets.In;
using Uninarok.Networking.Packets.Out;

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

    public readonly struct UninarokServerEndpoint
    {
        public readonly IPAddress Address;
        public readonly int Port;

        public UninarokServerEndpoint(IPAddress address, int port)
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
            lengths[CA.LOGIN.PacketId] = CA.LOGIN.Size;
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
            var packetLength = GetPacketLength(CA.LOGIN.PacketId);
            RequireLength(packet, packetLength);
            packet.Slice(0, packetLength).Clear();
            var loginPack = new CA.LOGIN
            {
                AccountName = new Unity.Collections.FixedString32Bytes(username),
                AccountPassword = new Unity.Collections.FixedString32Bytes(password)
            };
            loginPack.Serialize().Span.CopyTo(packet);
        }

        public static void WriteCharacterServerEnter(Span<byte> packet, uint accountId, int authCode, uint userLevel, byte sex)
        {
            RequireLength(packet, 17);
            packet.Slice(0, 17).Clear();
            new CH.ENTER(accountId, authCode, userLevel, sex).Serialize().Span.CopyTo(packet);
        }

        public static void WriteRequestCharacterList(Span<byte> packet)
        {
            RequireLength(packet, 2);
            var writer = new Packets.PacketWriter(packet);
            writer.WriteUInt16((ushort)RagnarokPacketId.RequestCharacterList);
        }

        public static void WriteCreateCharacter(Span<byte> packet, string name, byte slot, ushort hairColor, ushort hairStyle, ushort jobClass, byte sex)
        {
            RequireLength(packet, 36);
            packet.Slice(0, 36).Clear();
            new CH.MAKE_CHAR(name, slot, hairColor, hairStyle, jobClass, sex).Serialize(packet);
        }

        public static void WriteSelectCharacter(Span<byte> packet, byte slot)
        {
            RequireLength(packet, 3);
            new CH.SELECT_CHAR(slot).Serialize(packet);
        }

        public static void WriteMapEnter(Span<byte> packet, uint accountId, uint characterId, int authCode, uint tick, byte sex)
        {
            RequireLength(packet, 19);
            packet.Slice(0, 19).Clear();
            new Packets.CZ.ENTER_MAP(accountId, characterId, authCode, tick, sex).Serialize(packet);
        }

        public static bool TryGetCharacterServer(byte[] packet, int length, int serverIndex, out UninarokServerEndpoint endpoint)
        {
            endpoint = default;
            if (length < LoginListOffset || packet.Length < length)
                return false;

            try
            {
                var login = new AC.ACCEPT_LOGIN(packet.AsSpan(0, length));
                endpoint = login.ServersEndpointList;
                return true;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        public static int GetCharacterCount(byte[] packet, int length)
        {
            if (packet.Length < length || length < 4)
                throw new InvalidDataException("Invalid character list packet.");
            return Packets.HC.ACK_CHARINFO_PER_PAGE.GetCharacterCount(packet.AsSpan(0, length));
        }

        public static byte FindCharacterSlot(byte[] packet, int length, byte requestedSlot)
        {
            if (length < 4 || packet.Length < length)
                throw new InvalidDataException("Invalid character list packet.");
            return Packets.HC.ACK_CHARINFO_PER_PAGE.FindCharacterSlot(packet.AsSpan(0, length), requestedSlot);
        }

        public static bool TryGetMapEndpoint(byte[] packet, int length, out UninarokServerEndpoint endpoint)
        {
            endpoint = default;
            if (length < 28 || packet.Length < length)
                return false;
            var reader = new Packets.PacketReader(packet.AsSpan(0, length));
            if (reader.ReadUInt16() != (ushort)RagnarokPacketId.MapServerRedirect)
                return false;
            reader.Skip(20);
            var address = new IPAddress(new[] { reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte() });
            int port = reader.ReadUInt16();
            if (port == 0)
                return false;
            endpoint = new UninarokServerEndpoint(address, port);
            return true;
        }

        public static uint ReadAccountId(byte[] packet) => new AC.ACCEPT_LOGIN(packet).AccountId;
        public static int ReadAuthCode(byte[] packet) => new AC.ACCEPT_LOGIN(packet).AuthCode;
        public static uint ReadUserLevel(byte[] packet) => new AC.ACCEPT_LOGIN(packet).UserLevel;
        public static byte ReadSex(byte[] packet) => new AC.ACCEPT_LOGIN(packet).Sex;

        public static uint ReadCharacterId(byte[] packet)
        {
            var reader = new Packets.PacketReader(packet);
            reader.Skip(2);
            return reader.ReadUInt32();
        }

        public static byte ReadCreatedCharacterSlot(byte[] packet) => Packets.HC.ACCEPT_MAKECHAR.ReadSlot(packet);

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
                var received = await _stream.ReadAsync(_buffer, offset, count, token).ConfigureAwait(false);
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
