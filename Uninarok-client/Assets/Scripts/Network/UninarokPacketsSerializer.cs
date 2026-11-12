using JetBrains.Annotations;
using System;
using Uninarok.Networking.Packets;
using UnityEngine;

namespace Uninarok.Networking
{
    public class UninarokPacketsSerializer
    {
        private readonly byte[] _packetBytes = new byte[ushort.MaxValue];
        private int _currentReadIndex;
        private int _currentWriteIndex;
        private int _ignoredBytesAmount;
        private readonly object _lock = new object();

        public void AppendData(byte[] data, int length)
        {
            lock (_lock)
            {
                if (length <= 0 || data == null || data.Length < length)
                {
                    Debug.LogError("Invalid data or length provided to AppendData.");
                    return;
                }
                var remainingSpace = _packetBytes.Length - _currentWriteIndex;
                if (length > remainingSpace)
                {
                    // TODO: Throw analytics?
                    Debug.LogError("Buffer overflow: Not enough space to append data.");
                    ResetBuffer();
                }
                Buffer.BlockCopy(data, 0, _packetBytes, _currentWriteIndex, length);
                _currentWriteIndex += length;

                // TODO: Length bigger than buffer size, etc
            }
        }

        public void IgnoreBytes(int amount)
        {
            _ignoredBytesAmount += amount;
        }

        public void ProcessPackages()
        {
            lock (_lock)
            {
                while (_currentReadIndex < _currentWriteIndex)
                {
                    int remainingBytes = GetBufferOffset();
                    if (remainingBytes < 2)
                        break;

                    var packetId = System.BitConverter.ToUInt16(_packetBytes, _currentReadIndex);

                    // Check if this is a valid packet ID (non-zero)
                    if (packetId == 0)
                    {
                        Debug.LogError($"Received packet with ID 0 and size {remainingBytes}. Disconnecting.");
                        // TODO: Handle invalid packet ID (e.g., disconnect the client)
                        break;
                    }

                    if (_ignoredBytesAmount > 0)
                    {
                        Debug.Log($"Ignoring {_ignoredBytesAmount} bytes for package with id {packetId:X4}");
                        _currentReadIndex += _ignoredBytesAmount;
                        _ignoredBytesAmount = 0;
                    }
                    else
                    {
                        int packetLength = UninarokNetworkingRouter.GetLength(packetId);
                        if (packetLength == -2)
                        {
                            Debug.LogError($"Unknown packet ID: 0x{packetId:X4}. Disconnecting.");
                            // TODO: Handle unknown packet ID (e.g., disconnect the client)
                            break;
                        }

                        // Check if the packet is fixed-size or variable-size
                        var isFixedSizePacket = packetLength > 0;

                        // Check if we have enough bytes to read the length for variable-length packets
                        if (!isFixedSizePacket && remainingBytes < 2)
                        {
                            // _currentReadIndex -= 2; // Move back the read index to re-read the packet ID next time
                            break;
                        }

                        if (!isFixedSizePacket)
                        {
                            packetLength = System.BitConverter.ToUInt16(_packetBytes, _currentReadIndex + 2);
                        }

                        if (remainingBytes < packetLength)
                        {
                            // Not enough data for a complete packet, wait for more data
                            // _currentReadIndex -= 2; // Move back the read index to re-read the packet ID and length next time
                            break;
                        }
                        //TODO: Check gc
                        var packetDataOffset = isFixedSizePacket ? 2 : 4;
                        var packetData = new byte[packetLength - packetDataOffset];
                        // _currentReadIndex +4 to ignore packet id and size
                        Buffer.BlockCopy(_packetBytes, _currentReadIndex + packetDataOffset, packetData, 0, packetLength - packetDataOffset);
                        _currentReadIndex += packetLength;

                        Debug.Log($"Packet received: 0x{packetId:X4} ({(PacketIds)packetId})");
                        UninarokNetworkingRouter.RoutePacket(packetId, packetData);
                    }

                    // Reseta o buffer de leitura se todos os dados foram processados
                    if (_currentReadIndex == _currentWriteIndex)
                    {
                        _currentReadIndex = 0;
                        _currentWriteIndex = 0;
                    }
                    else if (_currentReadIndex > _currentWriteIndex)
                    {
                        Debug.LogError("WTF");
                        _currentReadIndex = 0;
                        _currentWriteIndex = 0;
                    }
                }
            }
        }

        private void ResetBuffer()
        {
            var offset = GetBufferOffset();
            Buffer.BlockCopy(_packetBytes, _currentReadIndex, _packetBytes, 0, offset);

            _currentReadIndex = 0;
            _currentWriteIndex = offset;

            // TODO: Add super edge cases, offset bigger than buffer size, etc
        }

        /// <summary>
        /// Returns the offset between the read index and the write index
        /// </summary>
        /// <returns></returns>
        private int GetBufferOffset()
        {
            return _currentWriteIndex - _currentReadIndex;
        }
    }
}