using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Windows;

namespace Swagger
{
    internal class ObjectModel
    {
        // Swagger file path.
        internal string FilePath;

        internal ObjectModel(JsonNode jsonNode)
        {
            JsonNode = jsonNode;

            // Parse JsonNode.
            Info = new Info(jsonNode["info"]);
            ApiPaths = jsonNode["paths"].AsObject().Select(x => new ApiPath(x.Key, x.Value)).ToArray();
        }

        internal JsonNode JsonNode { get; set; }

        internal Info Info { get; set; }

        internal ApiPath[] ApiPaths { get; set; }

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

                DoubleIndentation(FilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, MainWindow.s_appDisplayName);
            }

            // Change 2-space indentation to 4-space.
            static void DoubleIndentation(string filePath)
            {
                List<string> fixedLines = new();

                var lines = File.ReadAllLines(filePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var indentSpaceCount = line.Length - line.TrimStart().Length;
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
    }
}