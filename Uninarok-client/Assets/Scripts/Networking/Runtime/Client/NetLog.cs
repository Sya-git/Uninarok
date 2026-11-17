using System;
using System.Diagnostics;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;
using Debug = UnityEngine.Debug;

namespace Uninarok.Networking.Client
{
    /// <summary>
    /// Logging of the networking layer (SPEC-0001 FR-7, FR-16; plan 4.7). Only real errors use <c>LogError</c>;
    /// state changes and information use <c>Log</c> or <c>LogWarning</c>. Main thread only.
    /// </summary>
    public static class NetLog
    {
        /// <summary>
        /// Per-packet tracing. The call, including the evaluation of its arguments, is removed by the compiler unless
        /// <c>UNINAROK_NET_TRACE</c> is defined, so no cost remains in normal builds.
        /// </summary>
        [Conditional("UNINAROK_NET_TRACE")]
        public static void Trace(string message) => Debug.Log("[Net.Trace] " + message);

        /// <summary>A state change or other information. Not an error.</summary>
        public static void Info(string message) => Debug.Log("[Net] " + message);

        public static void Warn(string message) => Debug.LogWarning("[Net] " + message);

        /// <summary>
        /// Reports how a connection ended, once. <c>LocalDispose</c> logs nothing (an intentional disconnect is not an error);
        /// a server close is a warning; errors, protocol faults and connect failures are errors.
        /// </summary>
        public static void ReportClosure(ConnectionRole role, CloseInfo info)
        {
            if (info == null) return;
            switch (info.Reason)
            {
                case CloseReason.LocalDispose:
                    break;
                case CloseReason.RemoteClosed:
                    Debug.LogWarning($"[Net] {role} server closed the connection: {info.Message}");
                    break;
                case CloseReason.Error:
                    Debug.LogError($"[Net] {role} connection error: {info.Message}");
                    break;
                case CloseReason.ProtocolFault:
                    Debug.LogError($"[Net] {role} protocol fault: {info.Message}");
                    break;
                case CloseReason.ConnectFailed:
                    Debug.LogError($"[Net] {role} connect failed: {info.Message}");
                    break;
            }
        }

        /// <summary>An exception thrown by a packet handler (the dispatcher already isolated it).</summary>
        public static void HandlerError(Exception e) => Debug.LogException(e);

        /// <summary>A packet whose payload could not be decoded; the stream continues with the next packet.</summary>
        public static void DecodeError(ushort packetId, PacketFormatException e) =>
            Debug.LogError($"[Net] Packet 0x{packetId:X4} could not be decoded: {e.Message}");
    }
}
