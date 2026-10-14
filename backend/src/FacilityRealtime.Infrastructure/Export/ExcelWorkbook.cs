using ClosedXML.Excel;

namespace FacilityRealtime.Infrastructure.Export;

/// <summary>One worksheet: a bold, frozen header row, then rows of text.</summary>
public sealed record ExcelSheet(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>Writes the .xlsx of wireframe D8.</summary>
public static class ExcelWorkbook
{
    private const double ColumnWidth = 16;

    /// <summary>
    /// Every cell is set as text, so a name or tag someone typed (e.g. "=1+1") can never run as a formula. Columns get a fixed
    /// width, because auto-fit measures text with system fonts the server may not have.
    /// </summary>
    public static byte[] Write(IReadOnlyList<ExcelSheet> sheets)
    {
        using var workbook = new XLWorkbook();
        foreach (var sheet in sheets)
        {
            var worksheet = workbook.Worksheets.Add(sheet.Name);
            for (var column = 0; column < sheet.Headers.Count; column++)
            {
                worksheet.Cell(1, column + 1).SetValue(sheet.Headers[column]);
            }

            worksheet.Row(1).Style.Font.Bold = true;
            worksheet.SheetView.FreezeRows(1);
            for (var row = 0; row < sheet.Rows.Count; row++)
            {
                for (var column = 0; column < sheet.Rows[row].Count; column++)
                {
                    worksheet.Cell(row + 2, column + 1).SetValue(sheet.Rows[row][column]);
                }
            }

            worksheet.Columns(1, sheet.Headers.Count).Width = ColumnWidth;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
