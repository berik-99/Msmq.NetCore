//
// Module-level FxCop supressions are kept in this file
//
using System.Diagnostics.CodeAnalysis;

//naming messages
[module: SuppressMessage("Microsoft.Naming", "CA1703:ResourceStringsShouldBeSpelledCorrectly", Scope = "resource", Target = "System.Messaging.resources", MessageId = "msmq")]
[module: SuppressMessage("Microsoft.Naming", "CA1703:ResourceStringsShouldBeSpelledCorrectly", Scope = "resource", Target = "System.Messaging.resources", MessageId = "propid")]
