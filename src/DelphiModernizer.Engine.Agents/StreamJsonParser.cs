using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DelphiModernizer.Engine.Agents;

/// <summary>Parses one line of <c>claude -p --output-format stream-json</c> output into events.</summary>
/// <remarks>
/// The CLI's event set grows between versions, so unknown types become <see cref="UnknownEvent"/> rather than errors.
/// Fields are read through <see cref="JsonDocument"/>, without reflection. See docs/design/agent-event-stream.md.
/// </remarks>
internal static class StreamJsonParser
{
    private const int _displayLength = 400;

    public static IEnumerable<AgentEvent> Parse(string line)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return [new Diagnostic("stdout", line)];
        }

        using (document)
        {
            var root = document.RootElement;

            return Text(root, "type") switch {
                "system" when Text(root, "subtype") == "init" => [new SessionStarted(Text(root, "session_id") ?? string.Empty, Text(root, "model"), Text(root, "claude_code_version"))],
                "assistant" => [.. Assistant(root)],
                "user" => [.. ToolResults(root)],
                "result" => [Result(root)],
                var type => [new UnknownEvent(type ?? "(none)", line)],
            };
        }
    }

    private static IEnumerable<AgentEvent> Assistant(JsonElement root)
    {
        var fromSubagent = root.TryGetProperty("parent_tool_use_id", out var parent) && parent.ValueKind == JsonValueKind.String;

        foreach (var block in ContentBlocks(root))
        {
            switch (Text(block, "type"))
            {
                case "text":
                    yield return new AssistantText(Text(block, "text") ?? string.Empty, fromSubagent);
                    break;
                case "tool_use":
                    var input = block.TryGetProperty("input", out var value) ? value.GetRawText() : "{}";
                    yield return new ToolCall(Text(block, "id") ?? string.Empty, Text(block, "name") ?? string.Empty, Shorten(input), fromSubagent);
                    break;
            }
        }
    }

    private static IEnumerable<AgentEvent> ToolResults(JsonElement root)
    {
        foreach (var block in ContentBlocks(root))
        {
            if (Text(block, "type") != "tool_result")
            {
                continue;
            }

            var isError = block.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True;
            var content = block.TryGetProperty("content", out var value) ? ContentText(value) : string.Empty;

            yield return new ToolResult(Text(block, "tool_use_id") ?? string.Empty, isError, Shorten(content));
        }
    }

    private static RunCompleted Result(JsonElement root)
    {
        var isError = root.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True;
        var denials = root.TryGetProperty("permission_denials", out var list) && list.ValueKind == JsonValueKind.Array ? list.GetArrayLength() : 0;

        return new RunCompleted(
            Text(root, "subtype") ?? "unknown",
            isError,
            Text(root, "result"),
            root.TryGetProperty("total_cost_usd", out var cost) && cost.TryGetDecimal(out var usd) ? usd : null,
            root.TryGetProperty("duration_ms", out var duration) && duration.TryGetInt64(out var ms) ? ms : null,
            root.TryGetProperty("num_turns", out var turns) && turns.TryGetInt32(out var count) ? count : null,
            denials);
    }

    private static IEnumerable<JsonElement> ContentBlocks(JsonElement root)
    {
        if (root.TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Array)
        {
            return content.EnumerateArray();
        }

        return [];
    }

    private static string ContentText(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return content.GetRawText();
        }

        var parts = new List<string>();

        foreach (var part in content.EnumerateArray())
        {
            parts.Add(Text(part, "text") ?? part.GetRawText());
        }

        return string.Join('\n', parts);
    }

    private static string? Text(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string Shorten(string text)
    {
        return text.Length <= _displayLength ? text : string.Concat(text.AsSpan(0, _displayLength), "…");
    }
}
