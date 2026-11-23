using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace HrSystemDhg.Web.Services;

public static class ReportPdf
{
    public static void ConfigureFonts() => GlobalFontSettings.FontResolver = new ReportFontResolver();

    public static byte[] Create(string title, IEnumerable<string[]> source, string organization = "Garowe Local Government", string systemName = "Garowe City Management System")
    {
        var rows = source.ToList();
        var document = new Document();
        document.Info.Title = title;
        var normal = document.Styles["Normal"]!;
        normal.Font.Name = "Noto Sans";
        normal.Font.Size = 8;
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A3;
        section.PageSetup.Orientation = Orientation.Landscape;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.2);
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.5);
        var heading = section.AddParagraph(organization);
        heading.Format.Font.Size = 17;
        heading.Format.Font.Color = Color.FromRgb(23, 69, 195);
        var subtitle = section.AddParagraph(title);
        subtitle.Format.Font.Size = 12;
        subtitle.Format.SpaceAfter = Unit.FromCentimeter(.4);
        var footer = section.Footers.Primary.AddParagraph();
        footer.AddText(systemName + " · ");
        footer.AddPageField();
        footer.AddText(" / ");
        footer.AddNumPagesField();
        if (rows.Count > 0 && rows[0].Length > 0)
        {
            var columns = rows[0].Length;
            var table = section.AddTable();
            table.Borders.Width = .3;
            table.Borders.Color = Color.FromRgb(210, 218, 230);
            table.TopPadding = 4;
            table.BottomPadding = 4;
            for (var column = 0; column < columns; column++)
                table.AddColumn(Unit.FromCentimeter(39.6 / columns));
            for (var index = 0; index < rows.Count; index++)
            {
                var row = table.AddRow();
                if (index == 0)
                {
                    row.HeadingFormat = true;
                    row.Shading.Color = Color.FromRgb(23, 69, 195);
                    row.Format.Font.Color = Colors.White;
                }
                else if (index % 2 == 0)
                    row.Shading.Color = Color.FromRgb(243, 246, 252);
                for (var column = 0; column < columns; column++)
                    row.Cells[column].AddParagraph(column < rows[index].Length ? rows[index][column] ?? "" : "");
            }
        }
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    private sealed class ReportFontResolver : IFontResolver
    {
        private readonly byte[] font = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", "NotoSans.ttf"));
        public byte[] GetFont(string faceName) => font;
        public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) => new("NotoSans", bold, italic);
    }
}
