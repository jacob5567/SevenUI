using System.Runtime.CompilerServices;

// The dev-only SevenUI.Tests plugin (tests/, built by ModTestRunner) reads internals. It is never shipped.
[assembly: InternalsVisibleTo("SevenUI.Tests")]
