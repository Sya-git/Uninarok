using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Uninarok.Networking
{
    internal sealed class TcpConnection : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly UninarokPacketsSerializer _serializer = new UninarokPacketsSerializer();

        private readonly byte[] _receiveBuffer = new byte[ushort.MaxValue];
        private readonly byte[] _sendBuffer = new byte[ushort.MaxValue];
        private readonly byte[] _writeBuffer = new byte[ushort.MaxValue];
        private readonly object _sendLock = new object();
        private readonly SemaphoreSlim _sendSignal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _connectionCancellationTokenSource = new CancellationTokenSource();
        private int _queuedBytes;
        private bool _disposed;

        public bool IsConnected => _client.Connected;

        private TcpConnection(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();

            _ = RunConnectionAsync();
        }

        public static async Task<TcpConnection> ConnectAsync(string host, int port, CancellationToken token)
        {
            var client = new TcpClient { NoDelay = true };
            try
            {
                UnityEngine.Debug.Log($"Connecting to: {host}:{port}");
                // O bootstrap de conexão pode usar Task comum sem problemas, pois roda uma única vez.
                var connectTask = client.ConnectAsync(host, port);
                var cancellationTask = Task.Delay(Timeout.Infinite, token);
                if (await Task.WhenAny(connectTask, cancellationTask).ConfigureAwait(false) != connectTask)
                {
                    client.Dispose();
                    try { await connectTask.ConfigureAwait(false); } catch { }
                    token.ThrowIfCancellationRequested();
                }
                UnityEngine.Debug.Log($"Awating to: {host}:{port}");
                await connectTask.ConfigureAwait(false);
                UnityEngine.Debug.Log($"Connected to: {host}:{port}");
                return new TcpConnection(client);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        public void IgnoreBytes(int amount)
        {
            _serializer.IgnoreBytes(amount);
        }

        public ValueTask SendAsync(Memory<byte> buffer, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            UnityEngine.Debug.LogError($"SENDING BYTES ({(Uninarok.Networking.Packets.PacketIds)BinaryPrimitives.ReadInt16LittleEndian(buffer.Span)} - {buffer.Span.Length} bytes)");
            if (buffer.Length == 0)
                return default;

            lock (_sendLock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(TcpConnection));

                if (buffer.Length > _sendBuffer.Length - _queuedBytes)
                {
                    UnityEngine.Debug.LogWarning("TCP send buffer is full; rejecting outgoing data.");
                    throw new InvalidOperationException("The TCP send buffer is full.");
                }

                buffer.Span.CopyTo(_sendBuffer.AsSpan(_queuedBytes, buffer.Length));
                _queuedBytes += buffer.Length;
                _sendSignal.Release();
            }

            return default;
        }

        private async Task RunConnectionAsync()
        {
            var token = _connectionCancellationTokenSource.Token;
            var readTask = ReadAsync(token);
            var writeTask = WriteAsync(token);

            try
            {
                await Task.WhenAny(readTask, writeTask).ConfigureAwait(false);
                _connectionCancellationTokenSource.Cancel();
                await Task.WhenAll(readTask, writeTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                _connectionCancellationTokenSource.Cancel();
                UnityEngine.Debug.LogException(exception);
            }
        }

        private async Task ReadAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var received = await _stream.ReadAsync(_receiveBuffer, 0, _receiveBuffer.Length, token).ConfigureAwait(false);
                if (received > 0)
                {
                    _serializer.AppendData(_receiveBuffer, received);
                }
                else
                {
                    throw new EndOfStreamException("The server closed the connection.");
                }
            }
        }

        private async Task WriteAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await _sendSignal.WaitAsync(token).ConfigureAwait(false);

                while (true)
                {
                    int bytesToWrite;
                    lock (_sendLock)
                    {
                        if (_queuedBytes == 0)
                            break;

                        bytesToWrite = _queuedBytes;
                        Array.Copy(_sendBuffer, 0, _writeBuffer, 0, bytesToWrite);
                    }

                    await _stream.WriteAsync(_writeBuffer, 0, bytesToWrite, token).ConfigureAwait(false);

                    lock (_sendLock)
                    {
                        if (_disposed)
                            return;

                        int remainingBytes = _queuedBytes - bytesToWrite;
                        if (remainingBytes > 0)
                            Array.Copy(_sendBuffer, bytesToWrite, _sendBuffer, 0, remainingBytes);

                        _queuedBytes = remainingBytes;
                    }
                }
            }
        }

        public void SerializePackages()
        {
            _serializer.ProcessPackages();
        }

        public void Dispose()
        {
            lock (_sendLock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _queuedBytes = 0;
            }

            _connectionCancellationTokenSource.Cancel();
            _stream?.Dispose();
            _client?.Dispose();
        }
    }
}