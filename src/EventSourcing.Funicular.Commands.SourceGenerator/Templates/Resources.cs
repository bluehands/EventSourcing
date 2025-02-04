using System.IO;

namespace EventSourcing.Commands.SourceGenerator.Templates;

static class Resources
{
    static readonly string Namespace = $"{typeof(TemplatesContents).Namespace}";

    public static string ReadResource(string filename)
    {
        var resourcePath = $"{Namespace}.{filename}";
        using var stream = typeof(TemplatesContents).Assembly.GetManifestResourceStream(resourcePath);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }
}

static class TemplatesContents
{
    public static string FailureTypeExtensions => Resources.ReadResource("FailureTypeExtensions.cs");
    public static string ResultTypeExtensions => Resources.ReadResource("ResultTypeExtensions.cs");
}