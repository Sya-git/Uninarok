// Runtime assembly of SPEC-0012.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Uninarok.Effects.PlayTests")]
[assembly: InternalsVisibleTo("Uninarok.World.Tests")]   // EffectHandle for the /effect bookkeeping tests
[assembly: InternalsVisibleTo("Uninarok.World.PlayTests")]   // EffectDevStopPlayTests (ResetForTests)
[assembly: InternalsVisibleTo("Uninarok.Effects.Editor")]   // the preview window reads an instance's length and frame rate (T8.2)
