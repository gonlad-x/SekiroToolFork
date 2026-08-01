using System.Windows;
using System.Windows.Input;

namespace SekiroTool.Views.Windows;

public partial class TextInputWindow : Window
{
    public string Value => InputBox.Text.Trim();

    private TextInputWindow(string title, string prompt, string initialValue)
    {
        InitializeComponent();

        Title = title;
        TitleText.Text = title;
        PromptText.Text = prompt;
        InputBox.Text = initialValue;

        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    /// <summary>Prompts for a single line of text. Returns null if cancelled or left blank.</summary>
    public static string? Prompt(string title, string prompt, string initialValue = "")
    {
        var window = new TextInputWindow(title, prompt, initialValue)
        {
            Owner = Application.Current.MainWindow
        };

        return window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.Value) ? window.Value : null;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) DialogResult = true;
        else if (e.Key == Key.Escape) DialogResult = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
}
