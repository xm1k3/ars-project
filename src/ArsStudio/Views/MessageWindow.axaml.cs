using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ArsStudio.Views;

public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    public MessageWindow(string message)
        : this()
    {
        MessageText.Text = message;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}
