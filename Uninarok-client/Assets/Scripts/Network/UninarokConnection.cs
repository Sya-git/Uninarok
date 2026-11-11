using System;
using System.Buffers.Binary;
using System.Diagnostics;
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
        private CancellationTokenSource _writeCancellationTokenSource = new CancellationTokenSource();
        private CancellationTokenSource _connectionCancellationTokenSource = new CancellationTokenSource();

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
                UnityEngine.Debug.LogError($"Connecting to: {host}:{port}");
                // O bootstrap de conexão pode usar Task comum sem problemas, pois roda uma única vez.
                var connectTask = client.ConnectAsync(host, port);
                var cancellationTask = Task.Delay(Timeout.Infinite, token);
                if (await Task.WhenAny(connectTask, cancellationTask).ConfigureAwait(false) != connectTask)
                {
                    client.Dispose();
                    try { await connectTask.ConfigureAwait(false); } catch { }
                    token.ThrowIfCancellationRequested();
                }
                UnityEngine.Debug.LogError($"Awating to: {host}:{port}");
                await connectTask.ConfigureAwait(false);
                UnityEngine.Debug.LogError($"Connected to: {host}:{port}");
                return new UninarokTcpConnection(client);
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

        public async ValueTask SendAsync(Memory<byte> buffer, CancellationToken token)
        {
            if (token == CancellationToken.None)
            {
                await _stream.WriteAsync(buffer, _writeCancellationTokenSource.Token).ConfigureAwait(false);
            }
            else
            {
                await _stream.WriteAsync(buffer, token).ConfigureAwait(false);
            }
        }

        private async ValueTask ReadAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var received = await _stream.ReadAsync(_buffer, 0, _buffer.Length, token).ConfigureAwait(false);
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

        public void Dispose()
        {
            _stream?.Dispose();
            _client?.Dispose();
            if (_readCancellationTokenSource != null)
            { 
                _readCancellationTokenSource.Cancel(); 
                _readCancellationTokenSource.Dispose();
                _readCancellationTokenSource = null;
            }
            if (_writeCancellationTokenSource != null)
            {
                _writeCancellationTokenSource.Cancel();
                _writeCancellationTokenSource.Dispose();
                _writeCancellationTokenSource = null;
            }
        }
    }
}