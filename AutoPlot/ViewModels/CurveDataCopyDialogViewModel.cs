using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;


public partial class CurveDataCopyDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _curveText;

    [ObservableProperty]
    private string _headerText;

    private readonly string _clipboardText;

    public CurveDataCopyDialogViewModel(string curveText)
    {
        (_headerText, _clipboardText) = SeparateHeader(curveText);
        CurveText = FormatForDisplay(_clipboardText);
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        Clipboard.SetText(_clipboardText ?? "");
    }

    private static string FormatForDisplay(string tabSeparatedText)
    {
        const int columnWidth = 16;
        string[] lines = tabSeparatedText.Replace("\r\n", "\n").Split('\n');
        var formatted = new System.Text.StringBuilder();

        foreach (string line in lines)
        {
            if (line.Length == 0)
                continue;

            string[] columns = line.Split('\t');
            for (int i = 0; i < columns.Length; i++)
            {
                string value = columns[i].Trim();
                formatted.Append(i == columns.Length - 1
                    ? value
                    : value.PadRight(columnWidth));
            }
            formatted.AppendLine();
        }

        return formatted.ToString();
    }

    private static (string Header, string Data) SeparateHeader(string text)
    {
        const int columnWidth = 16;
        string normalized = text.Replace("\r\n", "\n").TrimEnd('\n');
        string[] lines = normalized.Split('\n');
        if (lines.Length == 0 || lines[0].Length == 0)
            return (string.Empty, string.Empty);

        string[] firstColumns = lines[0].Split('\t');
        bool hasHeader = firstColumns.Length > 0 &&
                         firstColumns[0].Trim().Equals("X", StringComparison.OrdinalIgnoreCase);

        string[] headerColumns = hasHeader
            ? firstColumns.Select(column => column.Trim()).ToArray()
            : Enumerable.Range(0, firstColumns.Length)
                .Select(index => index == 0 ? "X" : index == 1 ? "Y" : $"Y{index}")
                .ToArray();

        string header = string.Concat(headerColumns.Select((column, index) =>
            index == headerColumns.Length - 1 ? column : column.PadRight(columnWidth)));
        string data = hasHeader
            ? string.Join(Environment.NewLine, lines.Skip(1))
            : string.Join(Environment.NewLine, lines);

        return (header, data);
    }
}
