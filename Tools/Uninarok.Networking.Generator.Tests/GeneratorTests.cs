using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Uninarok.Networking.Core.Dispatch;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Generator.Tests
{
    // SPEC-0001 FR-4, FR-18; plan 4.6 and 6 (GeneratorTests).
    public class GeneratorTests
    {
        private const string Header = @"
using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;
";

        private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(() =>
        {
            var refs = new List<MetadataReference>();
            var trusted = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            foreach (string path in trusted.Split(Path.PathSeparator))
                refs.Add(MetadataReference.CreateFromFile(path));
            refs.Add(MetadataReference.CreateFromFile(typeof(PacketDispatcher).Assembly.Location));   // Core, compiled into this test assembly
            return refs;
        });

        private sealed class Result
        {
            public Compilation Output;
            public ImmutableDiagnostics GeneratorDiagnostics;
            public GeneratorDriverRunResult Run;
            public string RouterSource => Run.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("GeneratedPacketRouter.g.cs"))?.ToString();
            public bool HasRouter => RouterSource != null;
            public IEnumerable<string> CompileErrors =>
                Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString());
        }

        private sealed class ImmutableDiagnostics : List<Diagnostic>
        {
            public ImmutableDiagnostics(IEnumerable<Diagnostic> d) : base(d) { }
            public bool Has(string id) => this.Any(x => x.Id == id);
        }

        private static Result Generate(string source)
        {
            var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp9);
            var tree = CSharpSyntaxTree.ParseText(Header + source, parseOptions);
            var compilation = CSharpCompilation.Create("GeneratorTestAssembly", new[] { tree }, References.Value,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: new[] { new PacketRouterGenerator().AsSourceGenerator() },
                additionalTexts: null,
                parseOptions: parseOptions,
                optionsProvider: null);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            return new Result { Output = output, GeneratorDiagnostics = new ImmutableDiagnostics(diagnostics), Run = driver.GetRunResult() };
        }

        private const string GoodPacket = @"
namespace Game
{
    [InPacket(0x0064, 6)]
    public readonly struct Login { public readonly int Value; public Login(ReadOnlySpan<byte> p) { var r = new PacketReader(p); Value = r.ReadInt32(); } }
}";

        // ---- generation ----

        [Test]
        public void OneInPacket_GeneratesARouter_ThatCompiles()
        {
            var r = Generate(GoodPacket);
            Assert.IsTrue(r.HasRouter, "router must be generated");
            CollectionAssert.IsEmpty(r.GeneratorDiagnostics, string.Join("\n", r.GeneratorDiagnostics));
            CollectionAssert.IsEmpty(r.CompileErrors.ToList(), "generated code must compile");
            StringAssert.Contains("case 0x0064:", r.RouterSource);
            StringAssert.Contains("global::Game.Login", r.RouterSource);
        }

        [Test]
        public void NoInPacket_GeneratesNothing_AndCompiles()
        {
            var r = Generate(@"namespace Game { public struct Plain { public int X; } }");
            Assert.IsFalse(r.HasRouter);
            CollectionAssert.IsEmpty(r.Run.GeneratedTrees);
            CollectionAssert.IsEmpty(r.CompileErrors.ToList());
        }

        [Test]
        public void OnlyOutPackets_GenerateNoRouter()
        {
            var r = Generate(@"
namespace Game
{
    [OutPacket(0x0065, 4)]
    public readonly struct Ping : IOutgoingPacket { public int WireSize => 4; public void Serialize(Span<byte> d) { } }
}");
            Assert.IsFalse(r.HasRouter);
            CollectionAssert.IsEmpty(r.GeneratorDiagnostics);
        }

        [Test]
        public void HandledIds_AreSortedAscending_WhateverTheDeclarationOrder()
        {
            string packet(ushort id, string name) => $@"
    [InPacket(0x{id:X4}, 2)] public readonly struct {name} {{ public {name}(ReadOnlySpan<byte> p) {{ }} }}";
            var r = Generate("namespace Game {" + packet(0x0300, "C") + packet(0x0010, "A") + packet(0x0200, "B") + "}");
            string src = r.RouterSource;
            Assert.Less(src.IndexOf("0x0010,"), src.IndexOf("0x0200,"));
            Assert.Less(src.IndexOf("0x0200,"), src.IndexOf("0x0300,"));
            CollectionAssert.IsEmpty(r.CompileErrors.ToList());
        }

        // ---- behaviour of the generated router ----

        private static (object router, Type packetType, MethodInfo route, PacketDispatcher dispatcher, List<(ushort, PacketFormatException)> decodeErrors, Func<int> last)
            Load(string source)
        {
            var r = Generate(source);
            using var ms = new MemoryStream();
            var emit = r.Output.Emit(ms);
            Assert.IsTrue(emit.Success, string.Join("\n", emit.Diagnostics));
            var asm = Assembly.Load(ms.ToArray());

            var dispatcher = new PacketDispatcher(e => throw e);
            var errors = new List<(ushort, PacketFormatException)>();
            var routerType = asm.GetType("Uninarok.Networking.Packets.GeneratedPacketRouter");
            var router = Activator.CreateInstance(routerType, dispatcher, new Action<ushort, PacketFormatException>((id, e) => errors.Add((id, e))));

            var packetType = asm.GetType("Game.Login");
            var probe = asm.GetType("Game.Probe");
            var handlerType = typeof(PacketHandler<>).MakeGenericType(packetType);
            var handler = Delegate.CreateDelegate(handlerType, probe.GetMethod("Handle"));
            typeof(PacketDispatcher).GetMethod("Subscribe").MakeGenericMethod(packetType).Invoke(dispatcher, new object[] { handler });
            Func<int> last = () => (int)probe.GetField("Last").GetValue(null);
            return (router, packetType, routerType.GetMethod("Route"), dispatcher, errors, last);
        }

        private const string ProbeSource = GoodPacket + @"
namespace Game { public static class Probe { public static int Last = -1; public static void Handle(in Login p) { Last = p.Value; } } }";

        [Test]
        public void Route_DecodesAndDispatches()
        {
            var (router, _, route, _, errors, last) = Load(ProbeSource);
            var handled = (bool)RouteBytes(router, route, 0x0064, new byte[] { 0x2A, 0, 0, 0 });
            Assert.IsTrue(handled);
            Assert.AreEqual(42, last());
            CollectionAssert.IsEmpty(errors);
        }

        [Test]
        public void Route_UnhandledId_ReturnsFalse()
        {
            var (router, _, route, _, errors, _) = Load(ProbeSource);
            Assert.IsFalse((bool)RouteBytes(router, route, 0x0999, new byte[] { 1, 2 }));
            CollectionAssert.IsEmpty(errors);
        }

        [Test]
        public void Route_MalformedPayload_ReportsDecodeErrorOnce_AndNextPacketStillWorks()
        {
            var (router, _, route, _, errors, last) = Load(ProbeSource);
            Assert.IsTrue((bool)RouteBytes(router, route, 0x0064, new byte[] { 1, 2 }));     // too short for an int
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual(0x0064, errors[0].Item1);
            Assert.AreEqual(-1, last(), "a failed decode dispatches nothing");

            Assert.IsTrue((bool)RouteBytes(router, route, 0x0064, new byte[] { 7, 0, 0, 0 }));
            Assert.AreEqual(7, last());
            Assert.AreEqual(1, errors.Count);
        }

        // Route takes a ReadOnlySpan<byte>, which reflection cannot box: call it through a compiled delegate shape instead.
        private static object RouteBytes(object router, MethodInfo route, ushort id, byte[] payload)
        {
            var del = (RouteDelegate)Delegate.CreateDelegate(typeof(RouteDelegate), router, route);
            return del(id, payload);
        }

        private delegate bool RouteDelegate(ushort id, ReadOnlySpan<byte> payload);

        // ---- diagnostics ----

        [Test]
        public void DuplicateIdInTheSameDirection_IsAnError()
        {
            var r = Generate(@"
namespace Game
{
    [InPacket(0x0064, 2)] public readonly struct A { public A(ReadOnlySpan<byte> p) { } }
    [InPacket(0x0064, 2)] public readonly struct B { public B(ReadOnlySpan<byte> p) { } }
}");
            Assert.IsTrue(r.GeneratorDiagnostics.Has("UPG0001"), string.Join("\n", r.GeneratorDiagnostics));
            Assert.IsTrue(r.GeneratorDiagnostics.Any(d => d.Id == "UPG0001" && d.Severity == DiagnosticSeverity.Error),
                "a duplicate ID must be an error, so the Unity compilation fails");
        }

        [Test]
        public void SameIdInOppositeDirections_IsAllowed()
        {
            var r = Generate(@"
namespace Game
{
    [InPacket(0x0064, 2)] public readonly struct A { public A(ReadOnlySpan<byte> p) { } }
    [OutPacket(0x0064, 2)] public readonly struct B : IOutgoingPacket { public int WireSize => 2; public void Serialize(Span<byte> d) { } }
}");
            Assert.IsFalse(r.GeneratorDiagnostics.Has("UPG0001"));
            CollectionAssert.IsEmpty(r.CompileErrors.ToList());
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(-2)]
        [TestCase(-7)]
        public void InvalidSize_IsAnError(int size)
        {
            var r = Generate($@"
namespace Game
{{
    [InPacket(0x0064, {size})] public readonly struct A {{ public A(ReadOnlySpan<byte> p) {{ }} }}
}}");
            Assert.IsTrue(r.GeneratorDiagnostics.Has("UPG0002"), string.Join("\n", r.GeneratorDiagnostics));
        }

        [TestCase(-1)]
        [TestCase(2)]
        [TestCase(55)]
        public void ValidSize_IsAccepted(int size)
        {
            var r = Generate($@"
namespace Game
{{
    [InPacket(0x0064, {size})] public readonly struct A {{ public A(ReadOnlySpan<byte> p) {{ }} }}
}}");
            Assert.IsFalse(r.GeneratorDiagnostics.Has("UPG0002"));
        }

        [Test]
        public void InPacketWithoutSpanConstructor_IsAnError()
        {
            var r = Generate(@"
namespace Game
{
    [InPacket(0x0064, 2)] public readonly struct A { public A(int x) { } }
}");
            Assert.IsTrue(r.GeneratorDiagnostics.Has("UPG0003"), string.Join("\n", r.GeneratorDiagnostics));
        }

        [Test]
        public void OutPacketWithoutInterface_IsAnError()
        {
            var r = Generate(@"
namespace Game
{
    [OutPacket(0x0064, 2)] public readonly struct A { }
}");
            Assert.IsTrue(r.GeneratorDiagnostics.Has("UPG0004"), string.Join("\n", r.GeneratorDiagnostics));
        }

        [Test]
        public void NonReadonlyStructPacket_IsAnError()
        {
            var r = Generate(@"
namespace Game
{
    [InPacket(0x0064, 2)] public struct A { public int X; public A(ReadOnlySpan<byte> p) { X = 0; } }
}");
            Assert.IsTrue(r.GeneratorDiagnostics.Has("UPG0005"), string.Join("\n", r.GeneratorDiagnostics));
        }

        [Test]
        public void InvalidPacketsAreLeftOutOfTheRouter_ButValidOnesStay()
        {
            var r = Generate(@"
namespace Game
{
    [InPacket(0x0064, 2)] public readonly struct Good { public Good(ReadOnlySpan<byte> p) { } }
    [InPacket(0x0065, 0)] public readonly struct Bad { public Bad(ReadOnlySpan<byte> p) { } }
}");
            StringAssert.Contains("case 0x0064:", r.RouterSource);
            StringAssert.DoesNotContain("case 0x0065:", r.RouterSource);
        }
    }
}
