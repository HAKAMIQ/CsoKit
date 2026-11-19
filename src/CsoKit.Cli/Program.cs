using System.Globalization;
using CsoKit.Cli.Commands;

// Output is English text and JSON: format numbers identically on every Windows
// region instead of mixing in the user's locale (for example Arabic percent signs).
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return CsoCommandDispatcher.Run(args);
