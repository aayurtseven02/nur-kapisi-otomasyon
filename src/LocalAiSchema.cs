using System.Text.Json;

using System.Text.Json.Nodes;



namespace AnkaStudio;



public static class LocalAiSchema

{

    public static JsonNode Build(bool spelling, string prompt, int? expectedShorts = null, bool? expectedLong = null)

    {

        var schema = JsonNode.Parse(JsonSerializer.Serialize(AiSchema.Response(spelling)))!["json_schema"]!["schema"]!.DeepClone();

        if (spelling) {

            using var doc = JsonDocument.Parse(prompt.StartsWith('{') ? prompt : "{}");

            if (doc.RootElement.TryGetProperty("Fields", out var fields) && fields.ValueKind == JsonValueKind.Object) {

                var variants = new JsonArray();

                foreach (var field in fields.EnumerateObject()) {

                    variants.Add(new JsonObject {

                        ["type"] = "object", ["additionalProperties"] = false,

                        ["required"] = new JsonArray("Field", "Before", "After", "Reason"),

                        ["properties"] = new JsonObject {

                            ["Field"] = new JsonObject { ["type"] = "string", ["const"] = field.Name },

                            ["Before"] = new JsonObject { ["type"] = "string", ["const"] = field.Value.GetString() },

                            ["After"] = new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = 6000 },

                            ["Reason"] = new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = 300 }

                        }

                    });

                }

                var changes = schema["properties"]!["Changes"]!; changes["maxItems"] = variants.Count;

                if (variants.Count > 0) changes["items"] = new JsonObject { ["anyOf"] = variants };

            }

        } else {

            var shorts = schema["properties"]!["Shorts"]!; shorts["maxItems"] = expectedShorts ?? 10;

            if (expectedShorts != null) shorts["minItems"] = expectedShorts.Value;

            shorts["items"]!["properties"]!["Scenes"]!["minItems"] = 1;

            shorts["items"]!["properties"]!["Scenes"]!["maxItems"] = 8;

            schema["properties"]!["Long"]!["anyOf"]![0]!["properties"]!["Scenes"]!["maxItems"] = 60;

            if (expectedLong == false) schema["properties"]!["Long"] = new JsonObject { ["type"] = "null" };

        }

        return schema;

    }

}

