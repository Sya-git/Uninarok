using System;

namespace Uninarok.World.Dev
{
    /// <summary>What a /effect command asks for (SPEC-0012 FR-8, PLAN-0012 4.8).</summary>
    public enum EffectCommandKind
    {
        PlayOnSelf,
        PlayAtCell,
        Stop,
    }

    /// <summary>A parsed /effect command line (the text after "/effect").</summary>
    public readonly struct EffectCommand
    {
        public EffectCommandKind Kind { get; }
        public string Path { get; }
        public int CellX { get; }
        public int CellY { get; }
        public bool Loop { get; }

        public EffectCommand(EffectCommandKind kind, string path, int cellX, int cellY, bool loop)
        {
            Kind = kind;
            Path = path;
            CellX = cellX;
            CellY = cellY;
            Loop = loop;
        }

        /// <summary>Parses the arguments of /effect; false with a message in <paramref name="error"/> when they do not fit a form.</summary>
        public static bool TryParse(string args, out EffectCommand cmd, out string error)
        {
            throw new NotImplementedException("T7.2");
        }
    }
}
