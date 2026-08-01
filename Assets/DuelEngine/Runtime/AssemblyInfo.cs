using System.Runtime.CompilerServices;

// The authored Unity presentation and networking assembly needs to deserialize
// verified Core snapshots. Production game logic remains authoritative in the
// DuelEngine assembly; this friendship only exposes snapshot hydration.
[assembly: InternalsVisibleTo("Assembly-CSharp")]
