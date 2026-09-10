using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Utilities;

public static class HtmlDocumentRenderer
{
    public static string RenderProjectDocument(Project project, List<TreeNode<ProjectSection>> tree)
    {
        var descriptionHtml = string.IsNullOrEmpty(project.Description) ? "" :
            $"""<div class="description">{RenderDescription(project.Description)}</div>""";

        var now = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC";
        var sectionCount = CountSections(tree);
        var meta = $"Status: {Encode(project.StatusLabel)} &middot; Generated: {now} &middot; Sections: {sectionCount}";

        var toc = tree.Count > 0 ? BuildToc(tree) : "";
        var body = string.Join("\n", tree.Select(node => RenderSectionHtml(node, 2)));

        return HtmlTemplate
            .Replace("{title}", Encode(project.Title))
            .Replace("{description_html}", descriptionHtml)
            .Replace("{meta}", meta)
            .Replace("{toc}", toc)
            .Replace("{body}", body);
    }

    public static string RenderWikiDocument(object sections)
        => throw new NotImplementedException("Wiki document rendering not yet implemented");

    // ── Helpers ──────────────────────────────────────────────────────

    private static string Encode(string? text) => WebUtility.HtmlEncode(text ?? "");

    private static int CountSections(List<TreeNode<ProjectSection>> tree)
    {
        var count = 0;
        foreach (var node in tree) { count++; count += CountSections(node.Children); }
        return count;
    }

    private static bool IsNumberedLine(string line)
    {
        var s = line.TrimStart();
        return s.Length > 2 && char.IsDigit(s[0]) && s.Contains(". ");
    }

    private static string PostprocessHtml(string html)
    {
        // Convert http/https URLs to clickable links, stopping at whitespace or unsafe HTML chars
        html = Regex.Replace(html,
            @"(https?://[^\s<>&'""]+)",
            """<a href="$1" target="_blank" rel="noopener">$1</a>""");

        // Convert **text** markers to <strong>text</strong>
        html = Regex.Replace(html, @"\*\*(.+?)\*\*", "<strong>$1</strong>");

        return html;
    }

    private static string RenderDescription(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // Normalize literal \n sequences (from DB storage) to actual newlines
        text = text.Replace("\\n", "\n");
        var escaped = WebUtility.HtmlEncode(text);
        var paragraphs = escaped.Split("\n\n");

        var classified = new List<(string Kind, string Html)>();
        foreach (var para in paragraphs)
        {
            var lines = para.Trim().Split('\n');
            if (lines.Any(l => l.TrimStart().StartsWith("- ")))
            {
                var items = new List<string>();
                var prose = new List<string>();
                foreach (var line in lines)
                {
                    var stripped = line.TrimStart();
                    if (stripped.StartsWith("- "))
                    {
                        if (prose.Count > 0) { items.Add($"<p>{string.Join(" ", prose)}</p>"); prose.Clear(); }
                        items.Add($"<li>{stripped[2..]}</li>");
                    }
                    else prose.Add(stripped);
                }
                if (prose.Count > 0) items.Add($"<p>{string.Join(" ", prose)}</p>");
                classified.Add(("ul", string.Join("\n", items)));
            }
            else if (lines.Any(l => IsNumberedLine(l)))
            {
                var items = new List<string>();
                var prose = new List<string>();
                foreach (var line in lines)
                {
                    var stripped = line.Trim();
                    if (IsNumberedLine(stripped))
                    {
                        if (prose.Count > 0) { items.Add($"<p>{string.Join(" ", prose)}</p>"); prose.Clear(); }
                        var itemText = stripped.Contains(". ") ? stripped[(stripped.IndexOf(". ") + 2)..] : stripped;
                        items.Add($"<li>{itemText}</li>");
                    }
                    else prose.Add(stripped);
                }
                if (prose.Count > 0) items.Add($"<p>{string.Join(" ", prose)}</p>");
                classified.Add(("ol", string.Join("\n", items)));
            }
            else
            {
                var joined = string.Join("<br>\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
                if (!string.IsNullOrEmpty(joined))
                    classified.Add(("p", $"<p>{joined}</p>"));
            }
        }

        // Coalesce consecutive same-type list blocks into a single <ul> or <ol>
        var parts = new List<string>();
        for (int i = 0; i < classified.Count;)
        {
            var (kind, html) = classified[i];
            if (kind is "ol" or "ul")
            {
                var merged = new List<string> { html };
                while (i + 1 < classified.Count && classified[i + 1].Kind == kind)
                {
                    i++;
                    merged.Add(classified[i].Html);
                }
                parts.Add($"<{kind}>\n{string.Join("\n", merged)}\n</{kind}>");
            }
            else parts.Add(html);
            i++;
        }
        return PostprocessHtml(string.Join("\n", parts));
    }

    private static string RenderSectionHtml(TreeNode<ProjectSection> node, int level)
    {
        var s = node.Data;
        var tag = $"h{Math.Min(level, 6)}";
        var sb = new StringBuilder();
        sb.AppendLine($"""<section id="section-{s.SectionId}">""");
        sb.AppendLine($"<{tag}>{Encode(s.Title)}</{tag}>");
        if (!string.IsNullOrEmpty(s.Description))
            sb.AppendLine($"""<div class="section-body">{RenderDescription(s.Description)}</div>""");
        if (!string.IsNullOrEmpty(s.FilePath))
            sb.AppendLine($"""<p class="file-path">Associated file: {Encode(s.FilePath)}</p>""");
        foreach (var child in node.Children)
            sb.AppendLine(RenderSectionHtml(child, level + 1));
        sb.AppendLine("</section>");
        return sb.ToString();
    }

    private static string BuildToc(List<TreeNode<ProjectSection>> tree, int level = 0)
    {
        var items = new StringBuilder();
        foreach (var node in tree)
        {
            items.AppendLine($"""<li><a href="#section-{node.Data.SectionId}">{Encode(node.Data.Title)}</a>""");
            if (node.Children.Count > 0)
                items.AppendLine(BuildToc(node.Children, level + 1));
            items.AppendLine("</li>");
        }
        return "<ul>\n" + items + "\n</ul>";
    }

    // ── HTML Template ────────────────────────────────────────────────

    private const string HtmlTemplate = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{title}</title>
        <link rel="preconnect" href="https://fonts.googleapis.com">
        <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
        <link href="https://fonts.googleapis.com/css2?family=Lexend:wght@300;400;500;600&display=swap" rel="stylesheet">
        <style>
          :root {
            --mountain-blue: #4A6FA5;
            --deep-slate: #3D5A80;
            --sky-blue: #87CEEB;
            --snow-white: #E8F4F8;
            --charcoal: #2D3748;
            --bg-body: #1a1a2e;
            --bg-card: #252540;
            --bg-section: #2d2d4a;
            --text-primary: #E8F4F8;
            --text-secondary: #b0bec5;
            --text-muted: #78909c;
            --border: rgba(74, 111, 165, 0.3);
            --divider: rgba(135, 206, 235, 0.15);
          }
          * { margin: 0; padding: 0; box-sizing: border-box; }
          body {
            font-family: 'Lexend', sans-serif;
            font-weight: 300;
            background: var(--bg-body);
            color: var(--text-primary);
            line-height: 1.7;
            padding: 2rem 1rem;
          }
          .container { max-width: 900px; margin: 0 auto; }
          .project-header {
            border-bottom: 2px solid var(--mountain-blue);
            padding-bottom: 1.5rem;
            margin-bottom: 2rem;
          }
          .project-header h1 {
            font-weight: 600;
            font-size: 2rem;
            color: var(--sky-blue);
            margin-bottom: 0.75rem;
          }
          .project-header .description {
            color: var(--text-secondary);
            font-size: 0.95rem;
            line-height: 1.8;
          }
          .meta {
            margin-top: 1rem;
            font-size: 0.8rem;
            color: var(--text-muted);
            font-weight: 400;
            letter-spacing: 0.03em;
          }
          .toc {
            background: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 8px;
            padding: 1.25rem 1.5rem;
            margin-bottom: 2.5rem;
          }
          .toc-title {
            font-weight: 500;
            font-size: 0.85rem;
            color: var(--text-muted);
            text-transform: uppercase;
            letter-spacing: 0.08em;
            margin-bottom: 0.75rem;
          }
          .toc ul { list-style: none; padding-left: 0; }
          .toc ul ul {
            padding-left: 1.25rem;
            border-left: 1px solid var(--divider);
            margin-left: 0.5rem;
          }
          .toc li { margin: 0.3rem 0; }
          .toc a {
            color: var(--mountain-blue);
            text-decoration: none;
            font-size: 0.9rem;
            font-weight: 400;
            transition: color 0.2s;
          }
          .toc a:hover { color: var(--sky-blue); }
          section { margin-bottom: 2rem; }
          section > section {
            margin-bottom: 1.25rem;
            padding-left: 1rem;
            border-left: 2px solid var(--divider);
          }
          h2 {
            font-weight: 500;
            font-size: 1.35rem;
            color: var(--sky-blue);
            padding-bottom: 0.4rem;
            border-bottom: 1px solid var(--divider);
            margin-bottom: 1rem;
          }
          h3 {
            font-weight: 500;
            font-size: 1.1rem;
            color: var(--mountain-blue);
            margin-bottom: 0.6rem;
          }
          h4, h5, h6 {
            font-weight: 500;
            font-size: 0.95rem;
            color: var(--text-secondary);
            margin-bottom: 0.5rem;
          }
          .section-body {
            font-size: 0.92rem;
            color: var(--text-secondary);
            line-height: 1.8;
          }
          .section-body p { margin-bottom: 0.8rem; }
          .section-body ul, .section-body ol {
            margin: 0.6rem 0 0.8rem 1.5rem;
            color: var(--text-secondary);
          }
          .section-body li { margin-bottom: 0.35rem; }
          .section-body a {
            color: var(--sky-blue);
            text-decoration: none;
            border-bottom: 1px solid rgba(135, 206, 235, 0.3);
            transition: color 0.2s, border-color 0.2s;
          }
          .section-body a:hover {
            color: #b8e4f9;
            border-bottom-color: var(--sky-blue);
          }
          .file-path {
            font-size: 0.8rem;
            color: var(--text-muted);
            font-style: italic;
            margin-top: 0.5rem;
          }
          .footer {
            margin-top: 3rem;
            padding-top: 1rem;
            border-top: 1px solid var(--divider);
            text-align: center;
            font-size: 0.75rem;
            color: var(--text-muted);
          }
        </style>
        </head>
        <body>
        <div class="container">
          <div class="project-header">
            <h1>{title}</h1>
            {description_html}
            <div class="meta">{meta}</div>
          </div>
          <nav class="toc">
            <div class="toc-title">Contents</div>
            {toc}
          </nav>
          <main>
            {body}
          </main>
          <div class="footer">
            Generated by LucyAPI &middot; Snowcap Systems
          </div>
        </div>
        </body>
        </html>
        """;
}
