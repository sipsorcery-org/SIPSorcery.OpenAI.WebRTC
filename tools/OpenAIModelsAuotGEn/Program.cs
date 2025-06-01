using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using YamlDotNet.RepresentationModel;

class Program
{
    static void Main(string[] args)
    {
        var yamlText = File.ReadAllText("openai.yaml");
        var yaml = new YamlStream();
        yaml.Load(new StringReader(yamlText));

        // Safely navigate the YAML structure
        var rootNode = yaml.Documents[0].RootNode as YamlMappingNode;
        if (rootNode == null)
        {
            Console.WriteLine("Invalid YAML structure - root is not a mapping node");
            return;
        }

        // Get components node
        if (!rootNode.Children.TryGetValue(new YamlScalarNode("components"), out var componentsNode))
        {
            Console.WriteLine("No 'components' section found");
            return;
        }

        var components = componentsNode as YamlMappingNode;
        if (components == null)
        {
            Console.WriteLine("Components section is not a mapping node");
            return;
        }

        // Get schemas node
        if (!components.Children.TryGetValue(new YamlScalarNode("schemas"), out var schemasNode))
        {
            Console.WriteLine("No 'schemas' section found");
            return;
        }

        var schemas = schemasNode as YamlMappingNode;
        if (schemas == null)
        {
            Console.WriteLine("Schemas section is not a mapping node");
            return;
        }

        var output = new StringBuilder();
        output.AppendLine("using System.Text.Json.Serialization;");
        output.AppendLine();
        output.AppendLine("namespace SIPSorcery.OpenAI.WebRTC.Models");
        output.AppendLine("{");

        foreach (var schemaEntry in schemas.Children)
        {
            var schemaName = ((YamlScalarNode)schemaEntry.Key).Value;
            if (!(schemaEntry.Value is YamlMappingNode schemaNode))
            {
                Console.WriteLine($"Skipping non-object schema: {schemaName}");
                continue;
            }

            output.AppendLine($"    public class {schemaName} : OpenAIServerEventBase");
            output.AppendLine("    {");

            // Handle type constant
            if (schemaNode.Children.TryGetValue(new YamlScalarNode("x-oaiMeta"), out var metaNode))
            {
                var meta = (YamlMappingNode)metaNode;
                if (meta.Children.TryGetValue(new YamlScalarNode("name"), out var typeNameNode))
                {
                    var typeName = ((YamlScalarNode)typeNameNode).Value;
                    output.AppendLine($"        public const string TypeName = \"{typeName}\";");
                    output.AppendLine();
                    output.AppendLine("        [JsonPropertyName(\"type\")]");
                    output.AppendLine("        public override string Type => TypeName;");
                }
            }

            // Process properties
            if (schemaNode.Children.TryGetValue(new YamlScalarNode("properties"), out var propertiesNode))
            {
                var properties = (YamlMappingNode)propertiesNode;
                foreach (var propEntry in properties.Children)
                {
                    var propName = ((YamlScalarNode)propEntry.Key).Value;
                    var propNode = (YamlMappingNode)propEntry.Value;

                    // Skip type property since we already handled it
                    if (propName == "type") continue;

                    var propType = GetCSharpType(propNode);
                    output.AppendLine();
                    output.AppendLine($"        [JsonPropertyName(\"{propName}\")]");
                    output.AppendLine($"        public {propType} {ToPascalCase(propName)} {{ get; set; }}");
                }
            }

            output.AppendLine();
            output.AppendLine("        public override string ToJson()");
            output.AppendLine("        {");
            output.AppendLine("            return JsonSerializer.Serialize(this, JsonOptions.Default);");
            output.AppendLine("        }");
            output.AppendLine("    }");
            output.AppendLine();
        }

        output.AppendLine("}");
        File.WriteAllText("OpenAIModels.cs", output.ToString());
    }

    static string GetCSharpType(YamlMappingNode propNode)
    {
        if (propNode.Children.TryGetValue(new YamlScalarNode("$ref"), out var refNode))
        {
            var refValue = ((YamlScalarNode)refNode).Value;
            var typeName = refValue.Split('/').Last();
            return typeName + "?";
        }

        if (!propNode.Children.TryGetValue(new YamlScalarNode("type"), out var typeNode))
            return "object?";

        var type = ((YamlScalarNode)typeNode).Value;
        return type switch
        {
            "string" => "string?",
            "integer" => "int?",
            "boolean" => "bool?",
            "number" => "double?",
            "array" => "List<object>?", // Simplified for example
            _ => "object?"
        };
    }

    static string ToPascalCase(string s) =>
        string.Concat(s[0].ToString().ToUpper(), s.AsSpan(1));
}