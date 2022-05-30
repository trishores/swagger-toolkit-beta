using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;

namespace Swagger
{
    internal class ObjectModel
    {
        // Swagger file path.
        internal string FilePath;

        // Specific swagger file test.
        internal bool IsPowerBiClient;

        internal ObjectModel(JsonNode jsonNode)
        {
            JsonNode = jsonNode;

            // Parse JsonNode.
            Info = new Info(jsonNode["info"]);
            ApiPaths = jsonNode["paths"].AsObject().Select(x => new ApiPath(x.Key, x.Value)).ToArray();
            ApiDefinitions = jsonNode["definitions"].AsObject().Select(x => new ApiDefinition(x.Key, x.Value)).ToArray();

            // Specific swagger file test.
            IsPowerBiClient = string.Equals(Info?.Title, "Power BI Client");
        }

        internal JsonNode JsonNode { get; set; }

        internal Info Info { get; set; }

        internal ApiPath[] ApiPaths { get; set; }

        internal ApiDefinition[] ApiDefinitions { get; set; }

        internal async Task SaveAsync()
        {
            try
            {
                JsonSerializerOptions serializerOptions = new()
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true
                };
                string jsonStr = JsonNode.ToJsonString(serializerOptions);
                await File.WriteAllTextAsync(FilePath, jsonStr);

                if (IsPowerBiClient)
                {
                    DoubleIndent(FilePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, MainWindow.s_appDisplayName);
            }

            // Change default 2-space indentation to 4-space.
            static void DoubleIndent(string filePath)
            {
                List<string> fixedLines = new();

                string[] lines = File.ReadAllLines(filePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int indentSpaceCount = line.Length - line.TrimStart().Length;
                    line = "".PadLeft(indentSpaceCount, ' ') + line;
                    fixedLines.Add(line);
                }

                File.WriteAllLines(filePath, fixedLines);
            }
        }
    }

    internal class Info
    {
        internal readonly string Title;
        internal readonly string Version;

        public Info(JsonNode jsonNode)
        {
            JsonNode = jsonNode;
            Title = JsonNode["title"]?.GetValue<string>() ?? "";
            Version = JsonNode["version"]?.GetValue<string>() ?? "";
        }

        internal JsonNode JsonNode { get; set; }
    }

    internal class ApiPath
    {
        internal ApiPath(string jsonKey, JsonNode jsonNode)
        {
            JsonKey = jsonKey;
            JsonNode = jsonNode;

            // Parse JsonNode.
            HttpMethods = JsonNode.AsObject().Select(x => new HttpMethod(x.Key, x.Value)).ToArray();
        }

        internal string JsonKey { get; set; }
        internal JsonNode JsonNode { get; set; }

        internal HttpMethod[] HttpMethods { get; set; }
    }

    internal class HttpMethod
    {
        private string summary;
        private string description;

        internal HttpMethod(string jsonKey, JsonNode jsonNode)
        {
            JsonKey = jsonKey;
            JsonNode = jsonNode;

            // Parse JsonNode.
            Tags = JsonNode["tags"]?.AsArray()?.Select(x => x?.GetValue<string>())?.ToArray();
            OperationId = JsonNode["operationId"]?.GetValue<string>();
            Summary = JsonNode["summary"]?.GetValue<string>();
            Description = JsonNode["description"]?.GetValue<string>();
        }

        internal string JsonKey { get; set; }

        internal JsonNode JsonNode { get; set; }

        internal string[] Tags { get; set; }

        internal string Summary
        {
            get => summary ?? string.Empty;
            set
            {
                summary = value;
                JsonNode["summary"] = value;
            }
        }

        internal string Description
        {
            get => description ?? string.Empty;
            set
            {
                description = value;
                JsonNode["description"] = value;
            }
        }

        internal string OperationId { get; set; }

        internal string[] ApiTag
        {
            get
            {
                return Tags ?? Array.Empty<string>();
            }
        }

        internal string ApiOperation
        {
            get
            {
                return OperationId?.Replace("_", " - ");
            }
        }

        internal string WebUrl
        {
            get
            {
                string apiType = Tags[0];
                string opId1 = OperationId[..OperationId.IndexOf("_")];
                string opId2 = OperationId[(OperationId.IndexOf("_") + 1)..];
                string opId = apiType.Equals(opId1) ? opId2 : opId1 + opId2;
                string urlSegment1 = Hyphenate(apiType);
                string urlSegment2 = Hyphenate(opId);
                return $"https://docs.microsoft.com/en-us/rest/api/power-bi/{urlSegment1}/{urlSegment2}";

                static string Hyphenate(string text)
                {
                    text = text.Replace("PowerBI", "PowerBi").Replace("ID", "Id");
                    List<char> urlSegmentCharList = new();
                    foreach (char ch in text)
                    {
                        if (ch.ToString().Equals(ch.ToString().ToUpper()))
                        {
                            urlSegmentCharList.Add('-');
                        }
                        urlSegmentCharList.Add(ch);
                    }
                    string urlSegment = string.Join("", urlSegmentCharList).Trim('-').ToLower();
                    return urlSegment;
                }
            }
        }
    }

    internal class ApiDefinition
    {
        internal ApiDefinition(string jsonKey, JsonNode jsonNode)
        {
            JsonKey = jsonKey;
            JsonNode = jsonNode;

            // Parse JsonNode.
            Required = JsonNode["required"]?.AsArray()?.Select(x => x?.GetValue<string>())?.ToArray();
            Description = JsonNode["description"]?.GetValue<string>();
            Properties = JsonNode["properties"]?.AsObject()?.Select(x => new Property(x.Key, x.Value))?.ToArray();
        }

        internal string JsonKey { get; set; }
        internal JsonNode JsonNode { get; set; }

        internal string[] Required { get; set; }
        internal string Description { get; set; }
        internal Property[] Properties { get; set; }
    }

    internal class Property
    {
        private string description;

        internal Property(string jsonKey, JsonNode jsonNode)
        {
            JsonKey = jsonKey;
            JsonNode = jsonNode;

            // Parse JsonNode.
            Type = JsonNode["type"]?.GetValue<string>();
            Format = JsonNode["format"]?.GetValue<string>();
            Description = JsonNode["description"]?.GetValue<string>();

            // Handle missing description.
            if (Description == null)
            {
                JsonNode.AsObject().Remove("description");
            }
        }

        internal string JsonKey { get; set; }

        internal JsonNode JsonNode { get; set; }

        internal string Description
        {
            get => description;
            set
            {
                description = value;
                JsonNode["description"] = value;
            }
        }

        internal string Type { get; set; }

        internal string Format { get; set; }
    }
}