using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DelphiModernizer.Engine.Agents;

/// <summary>Parses the lines of one run's <c>claude -p --output-format stream-json</c> output into events.</summary>
/// <remarks>
/// The CLI's event set grows between versions, so unknown types become <see cref="UnknownEvent"/> rather than errors.
/// One instance per run: it keeps each open tool call until its result, to name what a refusal refused.
/// </remarks>
internal sealed class StreamJsonParser
{
    private const int _displayLength = 400;

    private static readonly string[] _targetProperties = ["file_path", "notebook_path", "command", "path"];

    private readonly Dictionary<string, (string Name, string? Target)> _openCalls = new(StringComparer.Ordinal);

    public IEnumerable<AgentEvent> Parse(string line)
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

    private IEnumerable<AgentEvent> Assistant(JsonElement root)
    {
        bool fromSubagent = root.TryGetProperty("parent_tool_use_id", out var parent) && parent.ValueKind == JsonValueKind.String;

        foreach (var block in ContentBlocks(root))
        {
            switch (Text(block, "type"))
            {
                case "text":
                    yield return new AssistantText(Text(block, "text") ?? string.Empty, fromSubagent);
                    break;
                case "tool_use":
                    bool hasInput = block.TryGetProperty("input", out var value);
                    string id = Text(block, "id") ?? string.Empty;
                    string name = Text(block, "name") ?? string.Empty;
                    _openCalls[id] = (name, hasInput ? TargetOf(value) : null);

                    yield return new ToolCall(id, name, Shorten(hasInput ? value.GetRawText() : "{}"), fromSubagent);
                    break;
            }
        }
    }

    private IEnumerable<AgentEvent> ToolResults(JsonElement root)
    {
        foreach (var block in ContentBlocks(root))
        {
            if (Text(block, "type") != "tool_result")
            {
                continue;
            }

            string id = Text(block, "tool_use_id") ?? string.Empty;
            bool isError = block.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True;
            string content = block.TryGetProperty("content", out var value) ? ContentText(value) : string.Empty;
            bool isOpen = _openCalls.Remove(id, out var call);

            if (isError && ToolRefusals.Recognise(content) is var (tool, reason))
            {
                yield return isOpen
                    ? new GuardRefused(id, call.Name, call.Target, Shorten(reason))
                    : new GuardRefused(id, tool, null, Shorten(reason));

                continue;
            }

            yield return new ToolResult(id, isError, Shorten(content));
        }
    }

    private static string? TargetOf(JsonElement input)
    {
        foreach (string property in _targetProperties)
        {
            if (Text(input, property) is { } target)
            {
                return target;
            }
        }

        return null;
    }

    private static RunCompleted Result(JsonElement root)
    {
        bool isError = root.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True;
        int denials = root.TryGetProperty("permission_denials", out var list) && list.ValueKind == JsonValueKind.Array ? list.GetArrayLength() : 0;

        return new RunCompleted(
            Text(root, "subtype") ?? "unknown",
            isError,
            Text(root, "result"),
            root.TryGetProperty("total_cost_usd", out var cost) && cost.TryGetDecimal(out decimal usd) ? usd : null,
            root.TryGetProperty("duration_ms", out var duration) && duration.TryGetInt64(out long ms) ? ms : null,
            root.TryGetProperty("num_turns", out var turns) && turns.TryGetInt32(out int count) ? count : null,
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
