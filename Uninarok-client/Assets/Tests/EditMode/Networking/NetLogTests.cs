using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-7 (intentional disconnect logs no error), FR-16 (tracing is opt-in at build time).
    public class NetLogTests
    {
#if !UNINAROK_NET_TRACE
        [Test]
        public void Trace_WithoutTheDefine_CostsNothing_ArgumentsAreNotEvenEvaluated()
        {
            int evaluated = 0;
            string Expensive() { evaluated++; return "x"; }

            Assert.That(() => { for (int i = 0; i < 100; i++) NetLog.Trace("packet " + i + Expensive()); },
                        UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.AreEqual(0, evaluated, "a [Conditional] call is removed, so its arguments are never evaluated");
        }

        [Test]
        public void Trace_WithoutTheDefine_LogsNothing()
        {
            NetLog.Trace("hidden");
            LogAssert.NoUnexpectedReceived();
        }
#endif

        [Test]
        public void ReportClosure_LocalDispose_LogsNothing()
        {
            NetLog.ReportClosure(ConnectionRole.Map, new CloseInfo(CloseReason.LocalDispose, null));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ReportClosure_RemoteClosed_IsAWarning_NotAnError()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Login server closed the connection"));
            NetLog.ReportClosure(ConnectionRole.Login, new CloseInfo(CloseReason.RemoteClosed, "Server closed the connection"));
        }

        [TestCase(CloseReason.Error, "connection error")]
        [TestCase(CloseReason.ProtocolFault, "protocol fault")]
        [TestCase(CloseReason.ConnectFailed, "connect failed")]
        public void ReportClosure_RealFailures_AreErrors(CloseReason reason, string text)
        {
            LogAssert.Expect(LogType.Error, new Regex("Character " + text + ": boom"));
            NetLog.ReportClosure(ConnectionRole.Character, new CloseInfo(reason, "boom"));
        }

        [Test]
        public void ReportClosure_Null_IsIgnored()
        {
            NetLog.ReportClosure(ConnectionRole.Map, null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Info_And_Warn_AreNotErrors()
        {
            LogAssert.Expect(LogType.Log, "[Net] state changed");
            LogAssert.Expect(LogType.Warning, "[Net] careful");
            NetLog.Info("state changed");
            NetLog.Warn("careful");
        }

        [Test]
        public void DecodeError_NamesTheId_AndIsAnError()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"0x006B could not be decoded: bad payload"));
            NetLog.DecodeError(0x006B, new PacketFormatException("bad payload"));
        }

        [Test]
        public void HandlerError_LogsTheException()
        {
            LogAssert.Expect(LogType.Exception, new Regex("handler boom"));
            NetLog.HandlerError(new InvalidOperationException("handler boom"));
        }
    }
}
