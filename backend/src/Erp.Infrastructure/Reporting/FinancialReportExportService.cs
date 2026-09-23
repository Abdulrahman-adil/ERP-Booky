using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Erp.Application.Accounting;

namespace Erp.Infrastructure.Reporting;

public sealed class FinancialReportExportService(IFinancialReportingService reporting) : IFinancialReportExportService
{
    public async Task<FinancialReportExport> ExportTrialBalanceAsync(Guid companyId, FinancialReportQuery query, FinancialReportExportFormat format, CancellationToken cancellationToken = default)
    {
        var report = await reporting.GetTrialBalanceAsync(companyId, query, cancellationToken);
        var document = new ExportDocument(
            "Trial Balance",
            DescribeRange(report.FromDate, report.ToDate),
            ["Account", "Opening debit", "Opening credit", "Period debit", "Period credit", "Closing debit", "Closing credit"],
            report.Rows.Select(row => new ExportRow(
                [$"{row.Code} — {row.Name}", row.OpeningDebit, row.OpeningCredit, row.PeriodDebit, row.PeriodCredit, row.ClosingDebit, row.ClosingCredit],
                !row.IsPosting,
                false)).Append(new ExportRow(["Totals", report.OpeningDebitTotal, report.OpeningCreditTotal, report.PeriodDebitTotal, report.PeriodCreditTotal, report.ClosingDebitTotal, report.ClosingCreditTotal], false, true)).ToArray());
        return Export(document, "trial-balance", report.ToDate, format);
    }

    public async Task<FinancialReportExport> ExportIncomeStatementAsync(Guid companyId, FinancialReportQuery query, FinancialReportExportFormat format, CancellationToken cancellationToken = default)
    {
        var report = await reporting.GetIncomeStatementAsync(companyId, query, cancellationToken);
        var rows = new List<ExportRow>();
        AddSection(rows, report.Revenue);
        rows.Add(new ExportRow(["Gross profit", report.GrossProfit], false, true));
        AddSection(rows, report.CostOfSales);
        AddSection(rows, report.OperatingExpenses);
        rows.Add(new ExportRow(["Operating profit", report.OperatingProfit], false, true));
        AddSection(rows, report.OtherIncome);
        AddSection(rows, report.OtherExpenses);
        rows.Add(new ExportRow(["Net profit", report.NetProfit], false, true));
        return Export(new ExportDocument("Profit and Loss", DescribeRange(report.FromDate, report.ToDate), ["Account", "Amount"], rows), "profit-and-loss", report.ToDate, format);
    }

    public async Task<FinancialReportExport> ExportBalanceSheetAsync(Guid companyId, FinancialReportQuery query, FinancialReportExportFormat format, CancellationToken cancellationToken = default)
    {
        var report = await reporting.GetBalanceSheetAsync(companyId, query, cancellationToken);
        var rows = new List<ExportRow>();
        AddSection(rows, report.Assets);
        rows.Add(new ExportRow(["Total assets", report.TotalAssets], false, true));
        AddSection(rows, report.Liabilities);
        AddSection(rows, report.Equity);
        rows.Add(new ExportRow(["Total liabilities and equity", report.TotalLiabilitiesAndEquity], false, true));
        rows.Add(new ExportRow(["Equation difference", report.Difference], false, true));
        return Export(new ExportDocument("Balance Sheet", $"As of {report.AsOfDate:dd MMM yyyy}", ["Account", "Amount"], rows), "balance-sheet", report.AsOfDate, format);
    }

    private static void AddSection(ICollection<ExportRow> rows, FinancialStatementSectionDto section)
    {
        rows.Add(new ExportRow([section.Name, section.Total], true, false));
        foreach (var row in section.Rows.Where(row => row.IsPosting))
        {
            rows.Add(new ExportRow([$"{new string(' ', Math.Max(0, row.HierarchyLevel - 1) * 2)}{row.Code} — {row.Name}{(row.IsDerived ? " (derived)" : string.Empty)}", row.Amount], false, false));
        }
    }

    private static FinancialReportExport Export(ExportDocument document, string filePrefix, DateOnly date, FinancialReportExportFormat format)
    {
        var extension = format == FinancialReportExportFormat.Xlsx ? "xlsx" : "pdf";
        var contentType = format == FinancialReportExportFormat.Xlsx
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "application/pdf";
        var content = format == FinancialReportExportFormat.Xlsx ? XlsxWriter.Write(document) : PdfWriter.Write(document);
        return new FinancialReportExport($"{filePrefix}-{date:yyyyMMdd}.{extension}", contentType, content);
    }

    private static string DescribeRange(DateOnly? fromDate, DateOnly toDate) => fromDate.HasValue
        ? $"{fromDate:dd MMM yyyy} to {toDate:dd MMM yyyy}"
        : $"Through {toDate:dd MMM yyyy}";

    private sealed record ExportDocument(string Title, string Subtitle, IReadOnlyList<string> Headers, IReadOnlyCollection<ExportRow> Rows);
    private sealed record ExportRow(IReadOnlyList<object> Cells, bool IsHeader, bool IsTotal);

    private static class XlsxWriter
    {
        public static byte[] Write(ExportDocument document)
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                WriteEntry(archive, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
                WriteEntry(archive, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                WriteEntry(archive, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Report\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                WriteEntry(archive, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
                WriteEntry(archive, "xl/styles.xml", Styles);
                WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildSheet(document));
            }
            return stream.ToArray();
        }

        private static string BuildSheet(ExportDocument document)
        {
            var builder = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><cols><col min=\"1\" max=\"1\" width=\"48\" customWidth=\"1\"/>");
            for (var index = 2; index <= document.Headers.Count; index++) builder.Append($"<col min=\"{index}\" max=\"{index}\" width=\"18\" customWidth=\"1\"/>");
            builder.Append("</cols><sheetData>");
            var rowIndex = 1;
            AddTextRow(builder, rowIndex++, [document.Title], 4);
            AddTextRow(builder, rowIndex++, [document.Subtitle], 5);
            AddTextRow(builder, rowIndex++, document.Headers, 1);
            foreach (var row in document.Rows)
            {
                builder.Append($"<row r=\"{rowIndex}\">");
                for (var columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++)
                {
                    var cell = row.Cells[columnIndex];
                    var reference = $"{ColumnName(columnIndex + 1)}{rowIndex}";
                    var style = row.IsTotal ? 3 : row.IsHeader ? 2 : cell is decimal ? 6 : 0;
                    if (cell is decimal amount)
                    {
                        builder.Append($"<c r=\"{reference}\" s=\"{style}\"><v>{amount.ToString(CultureInfo.InvariantCulture)}</v></c>");
                    }
                    else
                    {
                        builder.Append($"<c r=\"{reference}\" s=\"{style}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{EscapeXml(Convert.ToString(cell, CultureInfo.InvariantCulture) ?? string.Empty)}</t></is></c>");
                    }
                }
                builder.Append("</row>");
                rowIndex++;
            }
            builder.Append("</sheetData><autoFilter ref=\"A3:").Append(ColumnName(document.Headers.Count)).Append(rowIndex - 1).Append("\"/></worksheet>");
            return builder.ToString();
        }

        private static void AddTextRow(StringBuilder builder, int rowIndex, IReadOnlyList<string> values, int style)
        {
            builder.Append($"<row r=\"{rowIndex}\">");
            for (var index = 0; index < values.Count; index++) builder.Append($"<c r=\"{ColumnName(index + 1)}{rowIndex}\" s=\"{style}\" t=\"inlineStr\"><is><t>{EscapeXml(values[index])}</t></is></c>");
            builder.Append("</row>");
        }

        private static void WriteEntry(ZipArchive archive, string name, string content)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name, CompressionLevel.Fastest).Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        private static string ColumnName(int index)
        {
            var name = string.Empty;
            while (index > 0) { index--; name = (char)('A' + index % 26) + name; index /= 26; }
            return name;
        }

        private static string EscapeXml(string value) => System.Security.SecurityElement.Escape(value) ?? string.Empty;

        private const string Styles = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"2\"><font><sz val=\"10\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"10\"/><name val=\"Calibri\"/></font></fonts><fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF1F4E78\"/><bgColor indexed=\"64\"/></patternFill></fill></fills><borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"7\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/><xf numFmtId=\"4\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyNumberFormat=\"1\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"4\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/></cellXfs></styleSheet>";
    }

    private static class PdfWriter
    {
        public static byte[] Write(ExportDocument document)
        {
            var lines = new List<string> { document.Title, document.Subtitle, string.Empty, string.Join(" | ", document.Headers) };
            lines.AddRange(document.Rows.Select(row => string.Join(" | ", row.Cells.Select(cell => cell is decimal amount ? amount.ToString("N2", CultureInfo.InvariantCulture) : Convert.ToString(cell, CultureInfo.InvariantCulture)))));
            var pages = lines.Chunk(44).ToArray();
            var objects = new List<string> { "<< /Type /Catalog /Pages 2 0 R >>", $"<< /Type /Pages /Count {pages.Length} /Kids [{string.Join(' ', Enumerable.Range(0, pages.Length).Select(index => $"{4 + index * 2} 0 R"))}] >>", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>" };
            for (var pageIndex = 0; pageIndex < pages.Length; pageIndex++)
            {
                var pageId = 4 + pageIndex * 2;
                var contentId = pageId + 1;
                var content = BuildPage(pages[pageIndex], pageIndex + 1, pages.Length);
                objects.Add($"<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 3 0 R >> >> /MediaBox [0 0 612 792] /Contents {contentId} 0 R >>");
                objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
            }
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new ASCIIEncoding(), 1024, true);
            writer.Write("%PDF-1.4\n");
            var offsets = new List<long> { 0 };
            for (var index = 0; index < objects.Count; index++)
            {
                writer.Flush();
                offsets.Add(stream.Position);
                writer.Write($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
            }
            writer.Flush();
            var xref = stream.Position;
            writer.Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
            foreach (var offset in offsets.Skip(1)) writer.Write($"{offset:D10} 00000 n \n");
            writer.Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
            writer.Flush();
            return stream.ToArray();
        }

        private static string BuildPage(IEnumerable<string> lines, int pageNumber, int pageCount)
        {
            var builder = new StringBuilder("BT\n/F1 10 Tf\n50 750 Td\n");
            foreach (var line in lines) builder.Append('(').Append(EscapePdf(line)).Append(") Tj\n0 -16 Td\n");
            builder.Append($"0 -12 Td\n(Page {pageNumber} of {pageCount}) Tj\nET");
            return builder.ToString();
        }

        private static string EscapePdf(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }
}
