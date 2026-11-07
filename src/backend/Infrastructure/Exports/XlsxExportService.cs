using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using MySciClub.Application.Features.Members;

namespace MySciClub.Infrastructure.Exports;

internal sealed class XlsxExportService : IExcelExportService
{
    private static readonly string[] Headers =
    [
        "Numero socio", "Nome", "Cognome", "Data di nascita", "Luogo di nascita", "Sesso",
        "Codice fiscale", "Cittadinanza", "Indirizzo", "CAP", "Comune", "Provincia",
        "Paese di residenza", "Email", "Cellulare", "Stato tesseramento"
    ];

    public Task<byte[]> CreateAsync(IReadOnlyList<SocioExportRow> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            WriteText(archive, "[Content_Types].xml", ContentTypes);
            WriteText(archive, "_rels/.rels", RootRelationships);
            WriteText(archive, "xl/workbook.xml", Workbook);
            WriteText(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships);
            WriteSheet(archive, rows, cancellationToken);
        }
        return Task.FromResult(output.ToArray());
    }

    private static void WriteSheet(ZipArchive archive, IReadOnlyList<SocioExportRow> rows, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false });
        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteAttributeString("xmlns", "r", null, RelationshipsNamespace);
        writer.WriteStartElement("sheetViews", SpreadsheetNamespace);
        writer.WriteStartElement("sheetView", SpreadsheetNamespace); writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane", SpreadsheetNamespace); writer.WriteAttributeString("ySplit", "1");
        writer.WriteAttributeString("topLeftCell", "A2"); writer.WriteAttributeString("activePane", "bottomLeft"); writer.WriteAttributeString("state", "frozen"); writer.WriteEndElement();
        writer.WriteStartElement("selection", SpreadsheetNamespace); writer.WriteAttributeString("pane", "bottomLeft"); writer.WriteAttributeString("activeCell", "A2"); writer.WriteAttributeString("sqref", "A2"); writer.WriteEndElement();
        writer.WriteEndElement(); writer.WriteEndElement();
        WriteColumns(writer, rows);
        writer.WriteStartElement("sheetData", SpreadsheetNamespace);
        writer.WriteStartElement("row", SpreadsheetNamespace); writer.WriteAttributeString("r", "1");
        for (var column = 0; column < Headers.Length; column++) WriteCell(writer, column, 1, Headers[column]);
        writer.WriteEndElement();
        for (var index = 0; index < rows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var r = rows[index];
            string[] values =
            [
                r.NumeroSocio.ToString(CultureInfo.InvariantCulture), r.Nome, r.Cognome,
                r.DataNascita.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), r.LuogoNascita,
                r.Sesso.ToString(), r.CodiceFiscale, r.Cittadinanza, r.Indirizzo, r.Cap ?? string.Empty,
                r.Comune ?? string.Empty, r.Provincia ?? string.Empty, r.PaeseResidenza, r.Email,
                r.Cellulare, r.Tesserato ? "Tesserato" : "Non tesserato"
            ];
            var rowNumber = index + 2;
            writer.WriteStartElement("row", SpreadsheetNamespace); writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
            for (var column = 0; column < values.Length; column++) WriteCell(writer, column, rowNumber, values[column]);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteStartElement("autoFilter", SpreadsheetNamespace);
        writer.WriteAttributeString("ref", $"A1:P{Math.Max(1, rows.Count + 1)}");
        writer.WriteEndElement();
        writer.WriteEndElement(); writer.WriteEndDocument();
    }

    private static void WriteColumns(XmlWriter writer, IReadOnlyList<SocioExportRow> rows)
    {
        var maxLengths = Headers.Select(x => x.Length).ToArray();
        foreach (var row in rows)
        {
            var values = new[] { row.NumeroSocio.ToString(CultureInfo.InvariantCulture), row.Nome, row.Cognome,
                row.DataNascita.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), row.LuogoNascita, row.Sesso.ToString(),
                row.CodiceFiscale, row.Cittadinanza, row.Indirizzo, row.Cap ?? string.Empty, row.Comune ?? string.Empty,
                row.Provincia ?? string.Empty, row.PaeseResidenza, row.Email, row.Cellulare, row.Tesserato ? "Tesserato" : "Non tesserato" };
            for (var i = 0; i < values.Length; i++) maxLengths[i] = Math.Min(38, Math.Max(maxLengths[i], values[i]?.Length ?? 0));
        }
        writer.WriteStartElement("cols", SpreadsheetNamespace);
        for (var i = 0; i < maxLengths.Length; i++)
        {
            writer.WriteStartElement("col", SpreadsheetNamespace); writer.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("width", Math.Clamp(maxLengths[i] + 2, 12, 40).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customWidth", "1"); writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void WriteCell(XmlWriter writer, int zeroBasedColumn, int row, string value)
    {
        writer.WriteStartElement("c", SpreadsheetNamespace);
        writer.WriteAttributeString("r", ColumnName(zeroBasedColumn) + row.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("t", "inlineStr");
        writer.WriteStartElement("is", SpreadsheetNamespace); writer.WriteStartElement("t", SpreadsheetNamespace);
        if (value.Length != value.Trim().Length) writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
        writer.WriteString(value); writer.WriteEndElement(); writer.WriteEndElement(); writer.WriteEndElement();
    }

    private static string ColumnName(int index)
    {
        var result = string.Empty;
        for (index++; index > 0; index = (index - 1) / 26) result = (char)('A' + (index - 1) % 26) + result;
        return result;
    }

    private static void WriteText(ZipArchive archive, string name, string xml)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(xml);
    }

    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string ContentTypes = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>";
    private const string RootRelationships = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>";
    private const string Workbook = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Soci\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";
    private const string WorkbookRelationships = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>";
}
