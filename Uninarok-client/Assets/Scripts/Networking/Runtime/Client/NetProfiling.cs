using System.Threading;
using UnityEngine.Profiling;
using Uninarok.Networking.Core.Transport;

namespace Uninarok.Networking.Client
{
    /// <summary>
    /// Makes the socket reader and writer threads visible in the Unity Profiler (SPEC-0001 T8.1, FR-6). A thread that is not
    /// registered does not appear in the CPU module, so its allocations could not be seen. Core is engine-free, so this
    /// Runtime class installs the registration into <see cref="TcpConnection"/>'s thread hooks. Registration is done only in
    /// the editor and in development builds, where a Profiler exists.
    /// </summary>
    public static class NetProfiling
    {
        public const string ThreadGroup = "Uninarok.Net";
        private const string NamePrefix = "Uninarok.Net.";

        private static bool s_installed;
        private static int s_started;
        private static int s_stopped;

        /// <summary>Socket threads that announced they started since <see cref="Install"/> (editor and development builds).</summary>
        public static int SocketThreadsStarted => Volatile.Read(ref s_started);

        /// <summary>Socket threads that announced they are stopping.</summary>
        public static int SocketThreadsStopped => Volatile.Read(ref s_stopped);

        /// <summary>Socket threads still running: started minus stopped. Zero once every connection is closed (SPEC-0001 T9.7).</summary>
        public static int SocketThreadsRunning => SocketThreadsStarted - SocketThreadsStopped;

        /// <summary>Installs the hooks once. Idempotent; call on the main thread before any connection is opened.</summary>
        public static void Install()
        {
            if (s_installed) return;
            s_installed = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TcpConnection.SocketThreadStarted = OnThreadStarted;
            TcpConnection.SocketThreadStopping = OnThreadStopping;
#endif
        }

        /// <summary>The Profiler thread name for a socket thread name: "Uninarok.Net.Login.Read" becomes "Login.Read".</summary>
        public static string ProfilerThreadName(string threadName)
        {
            if (string.IsNullOrEmpty(threadName)) return "NetThread";
            return threadName.StartsWith(NamePrefix) ? threadName.Substring(NamePrefix.Length) : threadName;
        }

        private static void OnThreadStarted(string threadName)
        {
            Interlocked.Increment(ref s_started);
            Profiler.BeginThreadProfiling(ThreadGroup, ProfilerThreadName(threadName));
        }

        private static void OnThreadStopping()
        {
            Profiler.EndThreadProfiling();
            Interlocked.Increment(ref s_stopped);
        }
    }
}
