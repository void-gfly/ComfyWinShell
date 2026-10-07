using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfDesktop.ViewModels;

namespace WpfDesktop.Views;

public partial class ConfigurationView : UserControl
{
    public ConfigurationView()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, new RoutedEventHandler(OnButtonClick), true);
    }
    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ConfigurationViewModel viewModel
            && e.OriginalSource is Button button && ReferenceEquals(button.Command, viewModel.SaveDefaultCommand))
            viewModel.HasInputErrors = HasValidationError(this);
    }

    private static bool HasValidationError(DependencyObject element)
    {
        if (Validation.GetHasError(element)) return true;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            if (HasValidationError(VisualTreeHelper.GetChild(element, i))) return true;
        return false;
    }
}
