using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;


public partial class CurveDataCopyDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _curveText;

    public CurveDataCopyDialogViewModel(string curveText)
    {
        CurveText = curveText;
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        Clipboard.SetText(CurveText ?? "");
    }
}
