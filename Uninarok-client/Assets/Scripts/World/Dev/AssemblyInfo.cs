using System.Runtime.CompilerServices;

// Dev only: the assembly does not exist in release builds (its define constraint, PLAN-0010 D17).
[assembly: InternalsVisibleTo("Uninarok.World.Tests")]
[assembly: InternalsVisibleTo("Uninarok.World.PlayTests")]
