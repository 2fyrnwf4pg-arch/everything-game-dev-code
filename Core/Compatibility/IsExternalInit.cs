// Compiler-support shim, not game logic.
//
// Core targets netstandard2.1 so it can be dropped into Unity later without a
// rewrite. netstandard2.1 predates C# 9, so the compiler cannot find the marker
// type it needs to emit `init` accessors and positional records — which CLAUDE.md
// requires for immutable state objects. Declaring the type here is the standard
// polyfill; it contains no behaviour and disappears on frameworks that ship it.

#if !NET5_0_OR_GREATER

using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}

#endif
