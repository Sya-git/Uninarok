using System.Runtime.CompilerServices;

// The lookup tests build the facade from in-memory indices and count the entry dictionary builds (PLAN-0008 §6).
[assembly: InternalsVisibleTo("Uninarok.SpriteCatalog.Tests")]
// The editor's lookup preview builds the facade from the imported index assets, with no Addressables load (PLAN-0008 4.9).
[assembly: InternalsVisibleTo("Uninarok.RoImport.Editor")]
