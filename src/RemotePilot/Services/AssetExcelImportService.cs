using System.Globalization;
using System.IO;
using System.Text;
using ExcelDataReader;

namespace RemotePilot.Services;

/// <summary>读取 ISS7 等面板导出的 .xls/.xlsx 表格，转成标准 CSV 后复用 AssetCsvService 解析。</summary>
public static class AssetExcelImportService
{
    public static CsvImportResult Import(Stream stream)
    {
        // .xls（BIFF）里常含 GBK 等代码页文本，必须先注册代码页提供器
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var sb = new StringBuilder();
        try
        {
            using var reader = ExcelReaderFactory.CreateReader(stream);
            do
            {
                while (reader.Read())
                {
                    var fields = new string[reader.FieldCount];
                    bool hasValue = false;
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var text = FormatCell(reader.GetValue(i));
                        if (!string.IsNullOrWhiteSpace(text)) hasValue = true;
                        fields[i] = Escape(text);
                    }

                    // 跳过只有格式、没有内容的空行
                    if (!hasValue) continue;
                    sb.AppendLine(string.Join(",", fields));
                }

                // ISS7 导出只有一个工作表，读到第一个非空表即可
                if (sb.Length > 0) break;
            }
            while (reader.NextResult());
        }
        catch (Exception ex)
        {
            return new CsvImportResult
            {
                Errors = [new CsvImportError { LineNumber = 0, LineContent = "", Message = ex.Message }]
            };
        }

        return AssetCsvService.ImportCsv(sb.ToString());
    }

    private static string FormatCell(object? value) => value switch
    {
        null => "",
        DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        double d => d == Math.Floor(d) ? ((long)d).ToString(CultureInfo.InvariantCulture) : d.ToString(CultureInfo.InvariantCulture),
        bool b => b ? "1" : "0",
        _ => value.ToString() ?? ""
    };

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}
