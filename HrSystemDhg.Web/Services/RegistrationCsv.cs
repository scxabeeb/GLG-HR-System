using System.ComponentModel;
using System.Globalization;
using System.Text;
using HrSystemDhg.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;

namespace HrSystemDhg.Web.Services;

public static class RegistrationCsv
{
    public static FileContentResult Download(string name, string columns, IEnumerable<object?[]> rows)
    {
        var data = rows.Select(row => row.Select(Format).ToArray()).Prepend(columns.Split(','));
        return new FileContentResult(ExportService.Csv(data), "text/csv") { FileDownloadName = name + ".csv" };
    }

    private static string Format(object? value) => value switch
    {
        DateTime date => date.ToString(date.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? ""
    };

    public sealed class Row(Dictionary<string, string> cells)
    {
        public T Get<T>(string column)
        {
            var value = cells[column].Trim();
            if (typeof(T) == typeof(string))
                return (T)(object)value;
            if (value.Length == 0 && default(T) == null)
                return default!;
            try
            {
                if ((Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T)) == typeof(DateTime))
                {
                    if (DateTime.TryParseExact(value, ["yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"],
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                        return (T)(object)date;
                    throw new FormatException();
                }

                return (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(value)!;
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or NotSupportedException or OverflowException)
            {
                throw new FormatException($"Enter a valid {column}.");
            }
        }

        public string? Optional(string column) => string.IsNullOrWhiteSpace(cells[column]) ? null : cells[column].Trim();
        public string[] List(string column) => cells[column].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static async Task<IActionResult> ImportAsync(PageModel page, HrDbContext db, IFormFile? upload, string columns,
        Func<Row, Task<IActionResult>> save, Func<IActionResult>? redirect = null, int maxRecords = 500)
    {
        var expected = columns.Split(',');
        long line = 1;
        int count = 0;
        string message;
        try
        {
            if (upload == null || upload.Length is 0 or > 2 * 1024 * 1024)
                throw new FormatException("Choose a CSV file up to 2 MB.");
            using var parser = new TextFieldParser(upload.OpenReadStream(), new UTF8Encoding(false, true));
            parser.SetDelimiters(",");
            var headers = parser.ReadFields();
            if (headers == null || headers.Length != expected.Length ||
                !headers.ToHashSet(StringComparer.Ordinal).SetEquals(expected))
                throw new FormatException("Use the column names from the sample CSV.");

            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            while (!parser.EndOfData)
            {
                line = parser.LineNumber;
                var fields = parser.ReadFields();
                if (fields == null || fields.Length != headers.Length)
                    throw new FormatException("The number of values does not match the header.");
                if (++count > maxRecords)
                    throw new FormatException($"Upload at most {maxRecords} records per file.");

                page.ModelState.Clear();
                var row = new Row(headers.Zip(fields).ToDictionary(pair => pair.First, pair => pair.Second));
                var result = await save(row);
                if (result is not RedirectToPageResult || !page.ModelState.IsValid)
                {
                    var error = page.ModelState.Values.SelectMany(x => x.Errors)
                        .Select(e => e.ErrorMessage).FirstOrDefault(x => x.Length > 0);
                    throw new FormatException(error ?? "This record could not be saved. Check its values and your access.");
                }
            }
            if (count == 0)
                throw new FormatException("Add at least one record after the header.");
            db.AuditLogs.Add(new() { Actor = page.User.Identity!.Name!, Action = $"Imported {count} records on {page.PageContext.ActionDescriptor.ViewEnginePath}" });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            message = $"Imported {count} record(s).";
        }
        catch (Exception ex) when (ex is FormatException or MalformedLineException or DecoderFallbackException or DbUpdateException
            or MySqlConnector.MySqlException { Number: 1205 or 1213 })
        {
            db.ChangeTracker.Clear();
            var detail = ex is FormatException ? ex.Message :
                ex is MalformedLineException or DecoderFallbackException ? "Check the CSV formatting and UTF-8 encoding." :
                "Check for duplicates or concurrent changes and try again.";
            message = $"Import stopped at row {line}. No records were saved. {detail}";
        }

        page.TempData["RegistrationCsvDataset"] = page.Request.Query["dataset"].ToString() is { Length: > 0 } dataset ? dataset : null;
        page.TempData["RegistrationCsvResult"] = message;
        return redirect?.Invoke() ?? page.RedirectToPage();
    }
}
