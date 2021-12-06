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
            Swagger = jsonNode["swagger"];
            Info = jsonNode["info"];
            Host = jsonNode["host"];
            Schemes = jsonNode["schemes"];
            Consumes = jsonNode["consumes"];
            Produces = jsonNode["produces"];
            ApiPaths = jsonNode["paths"].AsObject().Select(x => new ApiPath(x.Key, x.Value)).ToArray();
            Definitions = jsonNode["definitions"];
            Parameters = jsonNode["parameters"];
            Responses = jsonNode["responses"];
            Security = jsonNode["security"];
            Tags = jsonNode["tags"];
        }

        internal JsonNode JsonNode { get; set; }

        internal JsonNode Swagger { get; set; }
        internal JsonNode Info { get; set; }
        internal JsonNode Host { get; set; }
        internal JsonNode Schemes { get; set; }
        internal JsonNode Consumes { get; set; }
        internal JsonNode Produces { get; set; }
        internal ApiPath[] ApiPaths { get; set; }
        internal JsonNode Definitions { get; set; }
        internal JsonNode Parameters { get; set; }
        internal JsonNode Responses { get; set; }
        internal JsonNode Security { get; set; }
        internal JsonNode Tags { get; set; }

        internal async Task SaveAsync()
        {
            try
            {    
                var serializerOptions = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true
                };
                string jsonStr = JsonNode.ToJsonString(serializerOptions);
                await File.WriteAllTextAsync(FilePath, jsonStr);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, MainWindow.s_appDisplayName);
            }
            MessageBox.Show($"Save to {FilePath}", MainWindow.s_appDisplayName);
        }
    }

    internal class ApiPath
    {
        internal ApiPath(string key, JsonNode jsonNode)
        {
            Key = key;  // e.g. /v7.0/myorg/mydatasets
            JsonNode = jsonNode;

            // Parse JsonNode.
            HttpMethods = JsonNode.AsObject().Select(x => new HttpMethod(x.Key, x.Value)).ToArray();
        }

        internal string Key { get; set; }
        internal JsonNode JsonNode { get; set; }

        internal HttpMethod[] HttpMethods { get; set; }
    }

    internal class HttpMethod
    {
        private string summary;
        private string description;

        internal HttpMethod(string key, JsonNode jsonNode)
        {
            Key = key;
            JsonNode = jsonNode;

            // Parse JsonNode.
            Tags = JsonNode["tags"].AsArray().Select(x => x.GetValue<string>()).ToArray();
            Summary = JsonNode["summary"].GetValue<string>();
            Description = JsonNode["description"].GetValue<string>();
            OperationId = JsonNode["operationId"].GetValue<string>();
            Consumes = JsonNode["consumes"];
            Produces = JsonNode["produces"];
            Parameters = JsonNode["parameters"];
            Responses = JsonNode["responses"];
            Examples = JsonNode["x-ms-examples"];
            Deprecated = JsonNode["deprecated"];

            var test = JsonNode["description"];
        }

        internal string Key { get; set; }
        internal JsonNode JsonNode { get; set; }

        internal string[] Tags { get; set; }
        internal string Summary
        {
            get => summary;
            set
            {
                summary = value;
                JsonNode["summary"] = value;
            }
        }
        internal string Description
        {
            get => description;
            set
            {
                description = value;
                JsonNode["description"] = value;
            }
        }
        internal string OperationId { get; set; }
        internal JsonNode Consumes { get; set; }
        internal JsonNode Produces { get; set; }
        internal JsonNode Parameters { get; set; }
        internal JsonNode Responses { get; set; }
        internal JsonNode Examples { get; set; }
        internal JsonNode Deprecated { get; set; }

        internal string ApiCategory
        {
            get
            {
                if (Tags.Length != 1) throw new Exception("Multiple tags");
                return SpaceOut(Tags[0]);
            }
        }
        internal string ApiName
        {
            get
            {
                string[] parts = OperationId.Split('_');
                if (parts.Length != 2) return OperationId;
                return parts[0] + " - " + parts[1];
            }
        }

        private static string SpaceOut(string str)
        {
            string str2 = "";
            foreach (char ch in str)
            {
                if (ch.ToString().ToUpper() == ch.ToString()) str2 += " ";
                str2 += ch;
            }
            string res = str2.Trim().Replace(" B I", " BI").Replace("   ", " ").Replace("  ", " ");
            return res;
        }
    }
}