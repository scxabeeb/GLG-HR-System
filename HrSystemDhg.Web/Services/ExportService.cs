using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace HrSystemDhg.Web.Services;
public static class ExportService
{
    public static string Safe(string? value)
    {
        var s = value ?? "";
        return s.TrimStart().StartsWith('=') || s.TrimStart().StartsWith('+') || s.TrimStart().StartsWith('-') || s.TrimStart().StartsWith('@') ? "'" + s : s;
    }

    public static byte[] Csv(IEnumerable<string[]> rows)
    {
        var text = string.Join("\r\n", rows.Select(row => string.Join(",", row.Select(v => "\"" + Safe(v).Replace("\"", "\"\"") + "\""))));
        return new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();
    }

    public static byte[] Excel(IEnumerable<string[]> rows)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void Add(string name, string text)
            {
                var entry = zip.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(text);
            }

            Add("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Add("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Add("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Report\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var sheet = new XElement(ns + "worksheet", new XElement(ns + "sheetData"));
            var data = sheet.Element(ns + "sheetData")!;
            int rowNumber = 0;
            foreach (var row in rows)
            {
                var rowElement = new XElement(ns + "row", new XAttribute("r", ++rowNumber));
                foreach (var cell in row)
                {
                    rowElement.Add(new XElement(ns + "c", new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), cell ?? ""))));
                }

                data.Add(rowElement);
            }

            Add("xl/worksheets/sheet1.xml", new XDocument(sheet).ToString());
        }

        return stream.ToArray();
    }
}
