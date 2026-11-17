using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Uninarok.Networking.Generator
{
    /// <summary>
    /// Emits <c>GeneratedPacketRouter</c> from every <c>[InPacket]</c> struct and checks packet declarations
    /// (SPEC-0001 FR-4, FR-18; plan 4.6). Emits nothing when the compilation has no <c>[InPacket]</c> type.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class PacketRouterGenerator : IIncrementalGenerator
    {
        private const string InAttribute = "Uninarok.Networking.Core.Protocol.InPacketAttribute";
        private const string OutAttribute = "Uninarok.Networking.Core.Protocol.OutPacketAttribute";
        private const string OutgoingInterface = "Uninarok.Networking.Core.Transport.IOutgoingPacket";
        private const string Category = "Uninarok.Networking";

        public static readonly DiagnosticDescriptor DuplicateId = new DiagnosticDescriptor(
            "UPG0001", "Duplicate packet ID", "Packet ID 0x{0:X4} is declared by {1} and {2} in the same direction",
            Category, DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor InvalidSize = new DiagnosticDescriptor(
            "UPG0002", "Invalid packet size", "Packet {0} declares size {1}; use -1 (variable) or a fixed total length of at least 2",
            Category, DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor MissingConstructor = new DiagnosticDescriptor(
            "UPG0003", "Incoming packet needs a decoding constructor", "[InPacket] struct {0} must declare a constructor taking a single ReadOnlySpan<byte> payload",
            Category, DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor MissingInterface = new DiagnosticDescriptor(
            "UPG0004", "Outgoing packet must implement IOutgoingPacket", "[OutPacket] struct {0} must implement Uninarok.Networking.Core.Transport.IOutgoingPacket",
            Category, DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor NotReadonlyStruct = new DiagnosticDescriptor(
            "UPG0005", "Packet must be a readonly struct", "Packet type {0} must be declared as a readonly struct",
            Category, DiagnosticSeverity.Error, true);

        private sealed class PacketInfo
        {
            public bool IsIncoming;
            public ushort Id;
            public int Size;
            public string DisplayName;       // for messages
            public string GlobalName;        // global::Namespace.Type, for emitted code
            public bool IsReadOnlyStruct;
            public bool HasSpanConstructor;
            public bool ImplementsOutgoing;
            public Location Location;
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<PacketInfo> packets = context.SyntaxProvider.CreateSyntaxProvider(
                    static (node, _) => node is StructDeclarationSyntax s && s.AttributeLists.Count > 0,
                    static (ctx, ct) => Describe(ctx, ct))
                .Where(static p => p != null);

            context.RegisterSourceOutput(packets.Collect(), static (spc, all) => Execute(spc, all));
        }

        private static PacketInfo Describe(GeneratorSyntaxContext ctx, System.Threading.CancellationToken ct)
        {
            var declaration = (StructDeclarationSyntax)ctx.Node;
            if (!(ctx.SemanticModel.GetDeclaredSymbol(declaration, ct) is INamedTypeSymbol symbol)) return null;

            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                string attributeName = attribute.AttributeClass?.ToDisplayString();
                bool isIn = attributeName == InAttribute;
                if (!isIn && attributeName != OutAttribute) continue;
                if (attribute.ConstructorArguments.Length < 2) continue;
                if (!(attribute.ConstructorArguments[0].Value is ushort id)) continue;
                if (!(attribute.ConstructorArguments[1].Value is int size)) continue;

                return new PacketInfo
                {
                    IsIncoming = isIn,
                    Id = id,
                    Size = size,
                    DisplayName = symbol.ToDisplayString(),
                    GlobalName = "global::" + symbol.ToDisplayString(),
                    IsReadOnlyStruct = symbol.IsReadOnly,
                    HasSpanConstructor = symbol.Constructors.Any(c =>
                        !c.IsStatic && c.Parameters.Length == 1 && c.Parameters[0].Type.ToDisplayString() == "System.ReadOnlySpan<byte>"),
                    ImplementsOutgoing = symbol.AllInterfaces.Any(i => i.ToDisplayString() == OutgoingInterface),
                    Location = declaration.Identifier.GetLocation(),
                };
            }
            return null;
        }

        private static void Execute(SourceProductionContext context, ImmutableArray<PacketInfo> all)
        {
            // Stable order regardless of syntax-tree enumeration order.
            var ordered = all.OrderBy(p => p.IsIncoming ? 0 : 1).ThenBy(p => p.Id).ThenBy(p => p.DisplayName, StringComparer.Ordinal).ToList();
            var routable = new List<PacketInfo>();
            var seen = new Dictionary<(bool, ushort), PacketInfo>();

            foreach (PacketInfo p in ordered)
            {
                bool ok = true;

                if (p.Size == 0 || p.Size == 1 || p.Size < -1)
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidSize, p.Location, p.DisplayName, p.Size));
                    ok = false;
                }
                if (!p.IsReadOnlyStruct)
                {
                    context.ReportDiagnostic(Diagnostic.Create(NotReadonlyStruct, p.Location, p.DisplayName));
                    ok = false;
                }
                if (p.IsIncoming && !p.HasSpanConstructor)
                {
                    context.ReportDiagnostic(Diagnostic.Create(MissingConstructor, p.Location, p.DisplayName));
                    ok = false;
                }
                if (!p.IsIncoming && !p.ImplementsOutgoing)
                {
                    context.ReportDiagnostic(Diagnostic.Create(MissingInterface, p.Location, p.DisplayName));
                    ok = false;
                }

                var key = (p.IsIncoming, p.Id);
                if (seen.TryGetValue(key, out PacketInfo first))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DuplicateId, p.Location, p.Id, first.DisplayName, p.DisplayName));
                    ok = false;
                }
                else
                {
                    seen.Add(key, p);
                }

                if (ok && p.IsIncoming) routable.Add(p);
            }

            if (!ordered.Any(p => p.IsIncoming)) return;          // no [InPacket] type: emit nothing
            context.AddSource("GeneratedPacketRouter.g.cs", EmitRouter(routable));
        }

        private static string EmitRouter(List<PacketInfo> packets)
        {
            var sb = new StringBuilder(4096);
            sb.Append("// <auto-generated/>\n");
            sb.Append("// Generated by Uninarok.Networking.Generator from the [InPacket] structs. Do not edit.\n");
            sb.Append("#nullable disable\n");
            sb.Append("using System;\n\n");
            sb.Append("namespace Uninarok.Networking.Packets\n{\n");
            sb.Append("    /// <summary>Routes each incoming packet to its decoder and then to the dispatcher (generated).</summary>\n");
            sb.Append("    public sealed class GeneratedPacketRouter : global::Uninarok.Networking.Core.Framing.IPacketRouter\n    {\n");
            sb.Append("        private static readonly ushort[] s_handledIds = new ushort[]\n        {\n");
            foreach (PacketInfo p in packets) sb.Append("            0x").Append(p.Id.ToString("X4")).Append(",\n");
            sb.Append("        };\n\n");
            sb.Append("        private readonly global::Uninarok.Networking.Core.Dispatch.PacketDispatcher _dispatcher;\n");
            sb.Append("        private readonly Action<ushort, global::Uninarok.Networking.Core.Wire.PacketFormatException> _onDecodeError;\n\n");
            sb.Append("        public GeneratedPacketRouter(global::Uninarok.Networking.Core.Dispatch.PacketDispatcher dispatcher,\n");
            sb.Append("                                     Action<ushort, global::Uninarok.Networking.Core.Wire.PacketFormatException> onDecodeError)\n        {\n");
            sb.Append("            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));\n");
            sb.Append("            _onDecodeError = onDecodeError ?? throw new ArgumentNullException(nameof(onDecodeError));\n");
            sb.Append("        }\n\n");
            sb.Append("        /// <summary>IDs this router decodes, ascending.</summary>\n");
            sb.Append("        public static ReadOnlySpan<ushort> HandledIds => s_handledIds;\n\n");
            sb.Append("        /// <summary>Decodes and dispatches one packet. False means the ID has no decoder (the framer skips it by length).</summary>\n");
            sb.Append("        public bool Route(ushort id, ReadOnlySpan<byte> payload)\n        {\n");
            sb.Append("            switch (id)\n            {\n");
            foreach (PacketInfo p in packets)
            {
                sb.Append("                case 0x").Append(p.Id.ToString("X4")).Append(":\n");
                sb.Append("                    try\n                    {\n");
                sb.Append("                        var packet = new ").Append(p.GlobalName).Append("(payload);\n");
                sb.Append("                        _dispatcher.Dispatch(in packet);\n");
                sb.Append("                    }\n");
                sb.Append("                    catch (global::Uninarok.Networking.Core.Wire.PacketFormatException e)\n                    {\n");
                sb.Append("                        _onDecodeError(id, e);\n");
                sb.Append("                    }\n");
                sb.Append("                    return true;\n");
            }
            sb.Append("                default:\n                    return false;\n");
            sb.Append("            }\n        }\n    }\n}\n");
            return sb.ToString();
        }
    }
}
