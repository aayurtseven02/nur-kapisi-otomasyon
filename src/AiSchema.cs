namespace AnkaStudio;

public static class AiSchema
{
    static object Text => new { type = "string" };
    static object Array(object item) => new { type = "array", items = item };
    static object Obj(Dictionary<string, object> fields) => new { type = "object", properties = fields, required = fields.Keys.ToArray(), additionalProperties = false };
    public static object Response(bool spelling)
    {
        var change = Obj(new() { ["Field"] = Text, ["Before"] = Text, ["After"] = Text, ["Reason"] = Text });
        var scene = Obj(new() { ["Narration"] = Text, ["VisualQuery"] = Text, ["OnScreen"] = Text });
        var script = Obj(new() { ["Title"] = Text, ["Description"] = Text, ["ThumbnailText"] = Text, ["Tags"] = Array(Text), ["Scenes"] = Array(scene) });
        var plan = Obj(new() { ["Research"] = Text, ["Warnings"] = Array(Text), ["Long"] = new { anyOf = new object[] { script, new { type = "null" } } }, ["Shorts"] = Array(script) });
        return new { type = "json_schema", json_schema = new { name = spelling ? "spelling_review" : "content_plan", strict = true, schema = spelling ? Obj(new() { ["Changes"] = Array(change) }) : plan } };
    }
}
