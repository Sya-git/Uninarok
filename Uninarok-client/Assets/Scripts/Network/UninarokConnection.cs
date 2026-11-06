using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Uninarok.Networking
{
    internal sealed class UninarokTcpConnection : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly UninarokPacketsSerializer _serializer = new UninarokPacketsSerializer();

        // Mantemos o buffer único estático por conexão para reuso completo
        private readonly byte[] _buffer = new byte[ushort.MaxValue];
        private CancellationTokenSource _readCancellationTokenSource = new CancellationTokenSource();

        public bool IsConnected => _client.Connected;

        private UninarokTcpConnection(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
            
            ReadAsync(_readCancellationTokenSource.Token).ConfigureAwait(false);
        }

        public static async Task<UninarokTcpConnection> ConnectAsync(string host, int port, CancellationToken token)
        {
            var client = new TcpClient { NoDelay = true };
            try
            {
                // O bootstrap de conexão pode usar Task comum sem problemas, pois roda uma única vez.
                var connectTask = client.ConnectAsync(host, port);
                var cancellationTask = Task.Delay(Timeout.Infinite, token);
                if (await Task.WhenAny(connectTask, cancellationTask).ConfigureAwait(false) != connectTask)
                {
                    client.Dispose();
                    try { await connectTask.ConfigureAwait(false); } catch { }
                    token.ThrowIfCancellationRequested();
                }

                await connectTask.ConfigureAwait(false);

                return new UninarokTcpConnection(client);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        public async ValueTask SendAsync(Memory<byte> buffer, CancellationToken token)
        {
            await _stream.WriteAsync(buffer, token).ConfigureAwait(false);
        }

        private async ValueTask ReadAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var received = await _stream.ReadAsync(_buffer, 0, _buffer.Length).ConfigureAwait(false);
                if (received > 0)
                {
                    _serializer.AppendData(_buffer, received);
                }
                else
                {
                    throw new EndOfStreamException("The server closed the connection.");
                }
            }
        }

        public void SerializePackages()
        {
            _serializer.ProcessPackages();
        }

        public async ValueTask<RagnarokPacket> ReadPacketAsync(CancellationToken token)
        {
            await ReadExactlyAsync(0, 2, token).ConfigureAwait(false);
            ushort id = BinaryPrimitives.ReadUInt16LittleEndian(_buffer);

            int length = RagnarokPacketCodec.GetPacketLength(id);
            bool isVariableLength = length == -1;

            if (isVariableLength)
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

            await ReadExactlyAsync(2, length - 2, token).ConfigureAwait(false);

            return new RagnarokPacket(id, length, _buffer);
        }

        private async ValueTask ReadExactlyAsync(int offset, int count, CancellationToken token)
        {
            while (count > 0)
            {
                var memoryBuffer = new Memory<byte>(_buffer, offset, count);
                var received = await _stream.ReadAsync(memoryBuffer, token).ConfigureAwait(false);

                if (received == 0)
                    throw new EndOfStreamException("The server closed the connection.");

                offset += received;
                count -= received;
            }
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _client?.Dispose();
            _readCancellationTokenSource.Cancel();
            _readCancellationTokenSource.Dispose();
        }
    }
}