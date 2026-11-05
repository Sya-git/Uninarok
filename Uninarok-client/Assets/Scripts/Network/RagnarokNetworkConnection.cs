using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Uninarok.Networking.Packets;

namespace Uninarok.Networking
{
    internal sealed class RagnarokNetworkConnection : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly byte[] _receiveBuffer = new byte[ushort.MaxValue];

        private RagnarokNetworkConnection(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
        }

        public static async Task<RagnarokNetworkConnection> ConnectAsync(string host, int port, CancellationToken cancellationToken)
        {
            var client = new TcpClient { NoDelay = true };
            try
            {
                Task connectTask = client.ConnectAsync(host, port);
                Task cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
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
                    cancellationToken.ThrowIfCancellationRequested();
                }

                await connectTask.ConfigureAwait(false);
                return new RagnarokNetworkConnection(client);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        public Task SendAsync(byte[] buffer, int length, CancellationToken cancellationToken)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (length < 0 || length > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(length));
            return _stream.WriteAsync(buffer, 0, length, cancellationToken);
        }

        public async Task<RagnarokPacket> ReceivePacketAsync(CancellationToken cancellationToken)
        {
            await ReadExactlyAsync(0, 2, cancellationToken).ConfigureAwait(false);
            ushort packetId = BinaryPrimitives.ReadUInt16LittleEndian(_receiveBuffer);
            int packetLength = PacketLengthTable.GetLength(packetId);
            bool variableLength = packetLength == -1;
            if (packetLength == 0)
                throw new InvalidDataException($"Unsupported packet 0x{packetId:X4}.");

            int headerLength = 2;
            if (variableLength)
            {
                await ReadExactlyAsync(2, 2, cancellationToken).ConfigureAwait(false);
                packetLength = BinaryPrimitives.ReadUInt16LittleEndian(_receiveBuffer.AsSpan(2, 2));
                headerLength = 4;
                if (packetLength < headerLength)
                    throw new InvalidDataException($"Invalid packet length {packetLength} for 0x{packetId:X4}.");
            }

            if (packetLength > _receiveBuffer.Length)
                throw new InvalidDataException($"Packet 0x{packetId:X4} exceeds the receive buffer.");

            await ReadExactlyAsync(headerLength, packetLength - headerLength, cancellationToken).ConfigureAwait(false);
            return new RagnarokPacket(packetId, packetLength, _receiveBuffer);
        }

        private async Task ReadExactlyAsync(int offset, int count, CancellationToken cancellationToken)
        {
            while (count > 0)
            {
                int received = await _stream.ReadAsync(_receiveBuffer, offset, count, cancellationToken).ConfigureAwait(false);
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
