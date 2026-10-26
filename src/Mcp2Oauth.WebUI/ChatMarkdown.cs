using System.Net;
using Ganss.Xss;
using Markdig;
using Microsoft.AspNetCore.Components;

namespace Mcp2Oauth.WebUI;

/// <summary>
/// Renders the agent's Markdown replies as HTML. A reply can repeat text from
/// tool results, such as an expense description anyone could have written, so
/// raw HTML in the Markdown is escaped and the output is sanitized as well
/// (no scripts, event handlers or <c>javascript:</c> links).
/// </summary>
/// <remarks>
/// Markdig 1.5 renders a pipe table that directly follows a line of text, as GFM
/// does. Older versions (0.41 in rockbot) needed a blank line before the header row.
/// </remarks>
public static class ChatMarkdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    private static readonly HtmlSanitizer Sanitizer = new();

    public static MarkupString ToHtml(string markdown)
    {
        try
        {
            return new(Sanitizer.Sanitize(Markdown.ToHtml(markdown, Pipeline)));
        }
        catch
        {
            // Fully encoded, so it needs no sanitizing.
            return new(WebUtility.HtmlEncode(markdown).Replace("\n", "<br/>"));
        }
    }
}
