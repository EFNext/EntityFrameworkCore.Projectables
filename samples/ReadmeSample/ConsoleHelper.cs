using System.Text.RegularExpressions;
using Spectre.Console;

namespace ReadmeSample;

/// <summary>Spectre.Console rendering helpers used by Program.cs.</summary>
internal static class ConsoleHelper
{
    /// <summary>
    /// Applies Spectre.Console markup to a raw SQL string:
    /// string literals → green, SQL keywords → bold cyan, ef_* helpers → dim grey.
    /// </summary>
    public static string SqlMarkup(string sql)
    {
        // Escape [ and ] so Spectre doesn't misinterpret them as markup tags.
        var esc = Markup.Escape(sql);

        // One-pass regex — order of alternatives matters:
        //   group 1 → single-quoted string literals
        //   group 2 → multi-word keywords (INNER JOIN, LEFT JOIN, ORDER BY, GROUP BY)
        //   group 3 → single-word SQL keywords
        //   group 4 → SQLite-specific ef_* helper functions
        return Regex.Replace(
            esc,
            @"('[^']*')"
            + @"|(\bINNER JOIN\b|\bLEFT JOIN\b|\bORDER BY\b|\bGROUP BY\b)"
            + @"|(\bSELECT\b|\bFROM\b|\bWHERE\b|\bCASE\b|\bWHEN\b|\bTHEN\b|\bELSE\b|\bEND\b"
            +   @"|\bAND\b|\bOR\b|\bNOT\b|\bNULL\b|\bIS\b|\bIN\b|\bON\b|\bAS\b"
            +   @"|\bLIMIT\b|\bCOALESCE\b|\bCAST\b)"
            + @"|(\bef_\w+\b)",
            m =>
            {
                if (m.Groups[1].Success) return $"[green]{m.Value}[/]";
                if (m.Groups[2].Success || m.Groups[3].Success) return $"[bold deepskyblue1]{m.Value}[/]";
                if (m.Groups[4].Success) return $"[grey50]{m.Value}[/]";
                return m.Value;
            },
            RegexOptions.IgnoreCase);
    }

    /// <summary>Renders a numbered feature section header using a yellow rule.</summary>
    public static void Section(int n, string title)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Rule($"[bold yellow]Feature {n}[/] — [white]{Markup.Escape(title)}[/]")
                .LeftJustified()
                .RuleStyle("dim yellow"));
        AnsiConsole.WriteLine();
    }

    /// <summary>Renders a SQL string inside a rounded panel with syntax highlighting.</summary>
    public static void ShowSql(string sql)
    {
        AnsiConsole.Write(new Panel(new Markup(SqlMarkup(sql)))
        {
            Header = new PanelHeader("[grey50] SQL [/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("grey"),
            Padding = new Padding(1, 0, 1, 0),
        });
        AnsiConsole.WriteLine();
    }

    /// <summary>Formats a currency amount in bold spring-green.</summary>
    public static string Money(decimal v) =>
        $"[bold springgreen2]{Markup.Escape(v.ToString("C"))}[/]";

    /// <summary>Formats a boolean as coloured true/false.</summary>
    public static string BoolMark(bool v) =>
        v ? "[bold chartreuse1]true[/]" : "[bold red1]false[/]";

    /// <summary>Formats a priority label with traffic-light colouring.</summary>
    public static string PriorityMark(string? p) => p switch
    {
        "High"   => "[bold red1]High[/]",
        "Medium" => "[bold yellow]Medium[/]",
        _        => "[bold green]Low[/]",
    };

    /// <summary>Formats an order status string with semantic colouring.</summary>
    public static string StatusMark(string? s) => s switch
    {
        "Fulfilled" => $"[bold green]{Markup.Escape(s)}[/]",
        "Cancelled" => $"[bold red1]{Markup.Escape(s)}[/]",
        _           => $"[bold yellow]{Markup.Escape(s ?? "?")}[/]",
    };
}

