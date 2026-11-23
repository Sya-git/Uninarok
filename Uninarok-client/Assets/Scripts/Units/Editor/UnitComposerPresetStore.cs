using System;
using System.Collections.Generic;

namespace Uninarok.Units.Editor
{
    /// <summary>Outcome of <see cref="UnitComposerPresetStore.Save"/>.</summary>
    public enum SaveResult
    {
        Saved,
        NeedsConfirm,
        Refused,
    }

    /// <summary>Composer presets: read-only starters plus per-user presets (PLAN-0009 4.12, FR-18). Stub, implemented by T11.2.</summary>
    public sealed class UnitComposerPresetStore
    {
        public UnitComposerPresetStore(string userFile, string starterFile) { }
        public static UnitComposerPresetStore ForProject() => throw new NotImplementedException("T11.2");
        public IReadOnlyList<(UnitComposerPreset Preset, bool Starter)> All => throw new NotImplementedException("T11.2");
        public string Problem => throw new NotImplementedException("T11.2");
        public bool IsStarter(string name) => throw new NotImplementedException("T11.2");
        public bool IsTaken(string name) => throw new NotImplementedException("T11.2");
        public SaveResult Save(UnitComposerPreset preset, bool overwrite) => throw new NotImplementedException("T11.2");
        public bool Rename(string from, string to) => throw new NotImplementedException("T11.2");
        public bool Delete(string name) => throw new NotImplementedException("T11.2");
        public void Reload() => throw new NotImplementedException("T11.2");
    }
}
