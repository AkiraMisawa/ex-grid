namespace System.Runtime.CompilerServices;

// netstandard2.0 lacks the marker type a record's init accessors need; the compiler only asks
// that it exist.
internal static class IsExternalInit;
