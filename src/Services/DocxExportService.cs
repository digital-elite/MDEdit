using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MDEdit.Services;

public static class DocxExportService
{
    public static void Export(string markdown, string outputPath)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var document = Markdown.Parse(markdown, pipeline);

        using var wordDoc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        var mainPart = wordDoc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        AddStyles(mainPart);

        foreach (var block in document)
        {
            ProcessBlock(block, body);
        }
    }

    private static void AddStyles(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        // Heading styles
        for (int i = 1; i <= 6; i++)
        {
            var fontSize = (48 - (i - 1) * 6) + ""; // 48, 42, 36, 30, 24, 18
            styles.Append(CreateHeadingStyle($"Heading{i}", fontSize));
        }

        // Code style
        styles.Append(new Style(
            new StyleName { Val = "Code" },
            new StyleRunProperties(
                new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
                new FontSize { Val = "20" },
                new Shading { Val = ShadingPatternValues.Clear, Fill = "F4F4F4" }
            )
        )
        { Type = StyleValues.Character, StyleId = "Code" });

        stylesPart.Styles = styles;
    }

    private static Style CreateHeadingStyle(string styleId, string fontSize)
    {
        return new Style(
            new StyleName { Val = styleId },
            new BasedOn { Val = "Normal" },
            new StyleParagraphProperties(new SpacingBetweenLines { Before = "240", After = "120" }),
            new StyleRunProperties(
                new Bold(),
                new FontSize { Val = fontSize }
            )
        )
        { Type = StyleValues.Paragraph, StyleId = styleId };
    }

    private static void ProcessBlock(Block block, Body body)
    {
        switch (block)
        {
            case HeadingBlock heading:
                ProcessHeading(heading, body);
                break;

            case ParagraphBlock paragraph:
                ProcessParagraph(paragraph, body);
                break;

            case ListBlock list:
                ProcessList(list, body);
                break;

            case FencedCodeBlock codeBlock:
                ProcessCodeBlock(codeBlock, body);
                break;

            case CodeBlock codeBlock:
                ProcessCodeBlock(codeBlock, body);
                break;

            case QuoteBlock quote:
                ProcessQuote(quote, body);
                break;

            case ThematicBreakBlock:
                body.Append(new Paragraph(
                    new ParagraphProperties(new ParagraphBorders(
                        new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "CCCCCC" }
                    ))
                ));
                break;

            case ContainerBlock container:
                foreach (var child in container)
                {
                    ProcessBlock(child, body);
                }
                break;
        }
    }

    private static void ProcessHeading(HeadingBlock heading, Body body)
    {
        var para = new Paragraph();
        var paraProps = new ParagraphProperties(
            new ParagraphStyleId { Val = $"Heading{heading.Level}" }
        );
        para.Append(paraProps);

        if (heading.Inline != null)
        {
            foreach (var inline in heading.Inline)
            {
                ProcessInline(inline, para);
            }
        }

        body.Append(para);
    }

    private static void ProcessParagraph(ParagraphBlock paragraph, Body body)
    {
        var para = new Paragraph();

        if (paragraph.Inline != null)
        {
            foreach (var inline in paragraph.Inline)
            {
                ProcessInline(inline, para);
            }
        }

        body.Append(para);
    }

    private static void ProcessList(ListBlock list, Body body)
    {
        int itemNumber = 1;
        foreach (var item in list)
        {
            if (item is ListItemBlock listItem)
            {
                foreach (var child in listItem)
                {
                    if (child is ParagraphBlock para)
                    {
                        var paragraph = new Paragraph();

                        // Add bullet or number
                        string prefix = list.IsOrdered ? $"{itemNumber}. " : "- ";
                        paragraph.Append(new Run(new Text(prefix)));

                        if (para.Inline != null)
                        {
                            foreach (var inline in para.Inline)
                            {
                                ProcessInline(inline, paragraph);
                            }
                        }

                        body.Append(paragraph);
                    }
                    else
                    {
                        ProcessBlock(child, body);
                    }
                }
                itemNumber++;
            }
        }
    }

    private static void ProcessCodeBlock(CodeBlock codeBlock, Body body)
    {
        var lines = codeBlock.Lines.ToString().Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line) && line != lines[^1]) continue;

            var para = new Paragraph(
                new ParagraphProperties(
                    new Shading { Val = ShadingPatternValues.Clear, Fill = "F4F4F4" },
                    new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }
                ),
                new Run(
                    new RunProperties(
                        new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
                        new FontSize { Val = "20" }
                    ),
                    new Text(line) { Space = SpaceProcessingModeValues.Preserve }
                )
            );
            body.Append(para);
        }
    }

    private static void ProcessQuote(QuoteBlock quote, Body body)
    {
        foreach (var block in quote)
        {
            if (block is ParagraphBlock para)
            {
                var paragraph = new Paragraph(
                    new ParagraphProperties(
                        new LeftBorder { Val = BorderValues.Single, Size = 24, Color = "CCCCCC", Space = 4 },
                        new Indentation { Left = "400" }
                    )
                );

                if (para.Inline != null)
                {
                    foreach (var inline in para.Inline)
                    {
                        ProcessInline(inline, paragraph);
                    }
                }

                body.Append(paragraph);
            }
            else
            {
                ProcessBlock(block, body);
            }
        }
    }

    private static void ProcessInline(Inline inline, Paragraph paragraph)
    {
        switch (inline)
        {
            case LiteralInline literal:
                paragraph.Append(new Run(new Text(literal.Content.ToString()) { Space = SpaceProcessingModeValues.Preserve }));
                break;

            case EmphasisInline emphasis:
                ProcessEmphasis(emphasis, paragraph);
                break;

            case CodeInline code:
                paragraph.Append(new Run(
                    new RunProperties(
                        new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
                        new FontSize { Val = "20" },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = "F4F4F4" }
                    ),
                    new Text(code.Content) { Space = SpaceProcessingModeValues.Preserve }
                ));
                break;

            case LineBreakInline:
                paragraph.Append(new Run(new Break()));
                break;

            case LinkInline link:
                ProcessLink(link, paragraph);
                break;

            case ContainerInline container:
                foreach (var child in container)
                {
                    ProcessInline(child, paragraph);
                }
                break;
        }
    }

    private static void ProcessEmphasis(EmphasisInline emphasis, Paragraph paragraph)
    {
        bool isBold = emphasis.DelimiterCount == 2;
        bool isItalic = emphasis.DelimiterCount == 1;

        foreach (var child in emphasis)
        {
            if (child is LiteralInline literal)
            {
                var runProps = new RunProperties();
                if (isBold) runProps.Append(new Bold());
                if (isItalic) runProps.Append(new Italic());

                paragraph.Append(new Run(
                    runProps,
                    new Text(literal.Content.ToString()) { Space = SpaceProcessingModeValues.Preserve }
                ));
            }
            else if (child is EmphasisInline nestedEmphasis)
            {
                // Handle bold-italic
                foreach (var nested in nestedEmphasis)
                {
                    if (nested is LiteralInline nestedLiteral)
                    {
                        paragraph.Append(new Run(
                            new RunProperties(new Bold(), new Italic()),
                            new Text(nestedLiteral.Content.ToString()) { Space = SpaceProcessingModeValues.Preserve }
                        ));
                    }
                }
            }
            else
            {
                ProcessInline(child, paragraph);
            }
        }
    }

    private static void ProcessLink(LinkInline link, Paragraph paragraph)
    {
        foreach (var child in link)
        {
            if (child is LiteralInline literal)
            {
                paragraph.Append(new Run(
                    new RunProperties(
                        new Color { Val = "0366D6" },
                        new Underline { Val = UnderlineValues.Single }
                    ),
                    new Text(literal.Content.ToString()) { Space = SpaceProcessingModeValues.Preserve }
                ));
            }
        }
    }
}
