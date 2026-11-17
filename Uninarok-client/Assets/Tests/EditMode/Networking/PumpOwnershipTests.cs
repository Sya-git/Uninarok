using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-5, FR-20; plan 4.7: "No other class calls PacketFramer.Process (a source-scan test enforces it)" and
    // only the connection point opens sockets. Production code lives under Assets/Scripts; tests are not scanned.
    public class PumpOwnershipTests
    {
        const string FramerFile = "Networking/Core/Framing/PacketFramer.cs";
        const string ConnectionFile = "Networking/Core/Transport/TcpConnection.cs";
        const string ClientFile = "Networking/Runtime/Client/NetworkClient.cs";
        const string ProfilingFile = "Networking/Runtime/Client/NetProfiling.cs";   // installs static thread hooks; never opens a connection
        const string ProbeFile = "Networking/Runtime/Dev/NetworkProbe.cs";             // dev-only measurement tool: opens a loopback listener for its thread check

        static string ScriptsRoot => Path.Combine(Application.dataPath, "Scripts");

        static string StripComments(string source)
        {
            source = Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline);
            return Regex.Replace(source, @"//[^\n]*", "");
        }

        /// <summary>Production source files (relative path, code without comments) that match <paramref name="pattern"/>.</summary>
        static List<string> FilesMatching(string pattern, IEnumerable<(string Path, string Code)> files)
            => files.Where(f => Regex.IsMatch(f.Code, pattern)).Select(f => f.Path).OrderBy(p => p).ToList();

        static List<(string Path, string Code)> ProductionSources()
        {
            string root = ScriptsRoot;
            return Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Select(f => (Path: f.Substring(root.Length + 1).Replace('\\', '/'), Code: StripComments(File.ReadAllText(f))))
                .ToList();
        }

        [Test]
        public void OnlyNetworkClient_UsesPacketFramer()
        {
            var users = FilesMatching(@"\bPacketFramer\b", ProductionSources());
            CollectionAssert.AreEquivalent(new[] { FramerFile, ClientFile }, users,
                "PacketFramer.Process is pumped by NetworkClient alone (FR-5). Offenders: " + string.Join(", ", users.Except(new[] { FramerFile, ClientFile })));
        }

        [Test]
        public void OnlyNetworkClient_CallsProcessOnTheFramer()
        {
            var callers = FilesMatching(@"\.Process\s*\(", ProductionSources());
            CollectionAssert.AreEqual(new[] { ClientFile }, callers, "Callers of .Process(: " + string.Join(", ", callers));
        }

        [Test]
        public void OnlyNetworkClient_UsesTcpConnection()
        {
            var allowed = new[] { ConnectionFile, ClientFile, ProfilingFile };
            var users = FilesMatching(@"\bTcpConnection\b", ProductionSources());
            CollectionAssert.AreEquivalent(allowed, users,
                "Only the connection point opens sockets (FR-20). Offenders: " + string.Join(", ", users.Except(allowed)));
        }

        [Test]
        public void NetProfiling_OnlyInstallsThreadHooks_ItNeverConnectsOrSends()
        {
            var profiling = ProductionSources().Where(f => f.Path == ProfilingFile).ToList();
            Assert.AreEqual(1, profiling.Count);
            Assert.IsFalse(Regex.IsMatch(profiling[0].Code, @"\b(ConnectAsync|Send|Close|Dispose)\b|new\s+TcpConnection"),
                "NetProfiling may only set TcpConnection.SocketThreadStarted/Stopping");
        }

        [Test]
        public void ManagersAndFlow_NeverTouchSockets()
        {
            // Phase 6 code must talk through INetworkClient only; no socket types outside the transport and the client.
            var offenders = FilesMatching(@"\b(System\.Net\.Sockets|TcpClient|TcpListener|Socket)\b", ProductionSources())
                .Where(p => p != ConnectionFile && p != ClientFile && p != ProbeFile).ToList();
            CollectionAssert.IsEmpty(offenders, "Socket types used outside the transport: " + string.Join(", ", offenders));
        }

        // ---- the scanner itself ----

        [Test]
        public void Scanner_IgnoresComments_ButCatchesRealUse()
        {
            var sources = new[]
            {
                ("A.cs", StripComments("// framer.Process(10) is only mentioned here\n/* new TcpConnection() */ int x;")),
                ("B.cs", StripComments("void F(PacketFramer f) { f.Process(5); }")),
                ("C.cs", StripComments("var c = new TcpConnection();")),
            };
            CollectionAssert.AreEqual(new[] { "B.cs" }, FilesMatching(@"\.Process\s*\(", sources));
            CollectionAssert.AreEqual(new[] { "C.cs" }, FilesMatching(@"\bTcpConnection\b", sources));
            CollectionAssert.AreEqual(new[] { "B.cs" }, FilesMatching(@"\bPacketFramer\b", sources));
        }

        [Test]
        public void Scanner_ReadsTheProductionSources()
        {
            var sources = ProductionSources();
            Assert.Greater(sources.Count, 50, "the scan must actually see the networking sources");
            Assert.IsTrue(sources.Any(s => s.Path == ClientFile));
        }
    }
}
