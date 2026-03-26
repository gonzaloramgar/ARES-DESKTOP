using System.Windows;

namespace AresAssistant.Views;

public partial class PurgeConfirmationDialog : Window
{
    public PurgeConfirmationDialog(Window owner)
    {
        InitializeComponent();
        Owner = owner;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
