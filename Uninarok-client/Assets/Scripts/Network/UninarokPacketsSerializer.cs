using System;
using UnityEngine;

namespace Uninarok.Networking
{
    public class UninarokPacketsSerializer
    {
        private readonly byte[] _packetBytes = new byte[ushort.MaxValue];
        private int _currentReadIndex;
        private int _currentWriteIndex;
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
                    _currentReadIndex += 2;
                    remainingBytes = GetBufferOffset();

                    // Check if this is a valid packet ID (non-zero)
                    if (packetId == 0)
                    {
                        Debug.LogError("Received packet with ID 0. Disconnecting.");
                        // TODO: Handle invalid packet ID (e.g., disconnect the client)
                        continue;
                    }

                    int packetLength = Packets.PacketLengthTable.GetLength(packetId);
                    if (packetLength == 0)
                    {
                        Debug.LogError($"Unknown packet ID: 0x{packetId:X4}. Disconnecting.");
                        // TODO: Handle unknown packet ID (e.g., disconnect the client)
                        break;
                    }

                    // Check if we have enough bytes to read the length for variable-length packets
                    if (remainingBytes < 2)
                    {
                        _currentReadIndex -= 2; // Move back the read index to re-read the packet ID next time
                        break;
                    }

                    // Check if the packet is fixed-size or variable-size
                    var isFixedSizePacket = packetLength > 0;
                    if (!isFixedSizePacket)
                    {
                        packetLength = System.BitConverter.ToUInt16(_packetBytes, _currentReadIndex);
                    }

                    if (remainingBytes < packetLength)
                    {
                        // Not enough data for a complete packet, wait for more data
                        _currentReadIndex -= isFixedSizePacket ? 2 : 4; // Move back the read index to re-read the packet ID and length next time
                        break;
                    }

                    var packetData = new byte[packetLength];
                    Buffer.BlockCopy(_packetBytes, _currentReadIndex, packetData, 0, packetLength);
                    _currentReadIndex += packetLength;

                    // Reseta o buffer de leitura se todos os dados foram processados
                    if (_currentReadIndex == _currentWriteIndex)
                    {
                        _currentReadIndex = 0;
                        _currentWriteIndex = 0;
                    }

                    UninarokPackageRouter.RoutePacket(packetId, packetData);
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