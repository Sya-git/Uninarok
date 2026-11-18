using System.Runtime.CompilerServices;

// The importer fills the asset through MapData.Assign (PLAN-0005 4.3); the runtime assembly does not reference it.
[assembly: InternalsVisibleTo("Uninarok.RoImport.Editor")]
[assembly: InternalsVisibleTo("Uninarok.RoImport.Tests")]
[assembly: InternalsVisibleTo("Uninarok.Map.Tests")]
