using System.Reflection;
using System.Xml.Linq;
using Msmq.NetCore.Messaging;

namespace Msmq.NetCore.Test.Tests;

public class ApiDocumentationTests
{
    [Fact]
    public void ExternalDocumentationMatchesThePublicApi()
    {
        var assembly = typeof(Message).Assembly;
        var path = Path.ChangeExtension(assembly.Location, ".xml");
        Assert.True(File.Exists(path), $"Missing external documentation: {path}");
        var document = XDocument.Load(path);
        Assert.Equal(assembly.GetName().Name, document.Root!.Element("assembly")!.Element("name")!.Value);
        var entries = document.Root.Element("members")!.Elements("member").ToArray();
        var documented = entries.Select(x => (string)x.Attribute("name")!).ToHashSet();
        Assert.Equal(entries.Length, documented.Count);
        var expected = assembly.GetExportedTypes()
            .Where(type => type.Namespace == "Msmq.NetCore.Messaging")
            .SelectMany(DocumentationIds).ToHashSet();
        Assert.Empty(expected.Except(documented).Order());
        Assert.Empty(documented.Except(expected).Order());
        Assert.All(entries, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Element("summary")?.Value)));
        Assert.DoesNotContain(document.Descendants().Attributes("cref"),
            reference => reference.Value.Contains("System.Messaging.", StringComparison.Ordinal));
    }

    [Fact]
    public void CompatibilityDifferencesAreIncludedInIntelliSense()
    {
        var document = XDocument.Load(Path.ChangeExtension(typeof(Message).Assembly.Location, ".xml"));
        foreach (string id in new[]
        {
            "M:Msmq.NetCore.Messaging.ActiveXMessageFormatter.Read(Msmq.NetCore.Messaging.Message)",
            "P:Msmq.NetCore.Messaging.XmlMessageFormatter.TargetTypes",
            "P:Msmq.NetCore.Messaging.XmlMessageFormatter.TargetTypeNames"
        })
        {
            var entry = document.Descendants("member").Single(x => (string)x.Attribute("name")! == id);
            Assert.Contains("Compatibility difference:", entry.Element("remarks")!.Value);
        }
    }

    private static IEnumerable<string> DocumentationIds(Type type)
    {
        yield return "T:" + type.FullName;
        if (typeof(MulticastDelegate).IsAssignableFrom(type))
            yield break; // Delegate declarations are documented; compiler-generated methods are not.
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var member in type.GetMembers(flags))
        {
            string name = type.FullName + "." + member.Name;
            switch (member)
            {
                case ConstructorInfo ctor when IsVisible(ctor):
                    yield return "M:" + type.FullName + ".#ctor" + Parameters(ctor.GetParameters());
                    break;
                case MethodInfo method when IsVisible(method) && !method.IsSpecialName:
                    yield return "M:" + name + Parameters(method.GetParameters());
                    break;
                case PropertyInfo property when property.GetAccessors(true).Any(IsVisible):
                    yield return "P:" + name + Parameters(property.GetIndexParameters());
                    break;
                case FieldInfo field when !field.IsSpecialName && (field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly):
                    yield return "F:" + name;
                    break;
                case EventInfo evt when evt.GetAddMethod(true) is { } add && IsVisible(add):
                    yield return "E:" + name;
                    break;
            }
        }
    }

    private static bool IsVisible(MethodBase method) => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

    private static string Parameters(ParameterInfo[] parameters) => parameters.Length == 0 ? "" :
        "(" + string.Join(",", parameters.Select(p => p.ParameterType.FullName!.Replace('&', '@').Replace('+', '.'))) + ")";
}
