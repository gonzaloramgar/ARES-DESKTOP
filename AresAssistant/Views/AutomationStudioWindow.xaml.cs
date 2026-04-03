using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using AresAssistant.Core;
using AresAssistant.Tools;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Views;

public partial class AutomationStudioWindow : Window
{
    private readonly ScheduledTaskStore _store;
    private readonly PermissionManager _permission;

    public AutomationStudioWindow(ScheduledTaskStore store, PermissionManager permission)
    {
        InitializeComponent();
        _store = store;
        _permission = permission;
        RefreshGrid();

        Loaded += (_, _) =>
        {
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240));
            RootBorder.BeginAnimation(OpacityProperty, fade);

            var slide = new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            RootTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slide);
        };
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        try
        {
            DragMove();
        }
        catch
        {
            // Ignore transient drag exceptions.
        }
    }

    private void RefreshGrid()
    {
        GridTasks.ItemsSource = null;
        GridTasks.ItemsSource = _store.GetAll();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var item = _store.Add(TxtTime.Text.Trim(), TxtCommand.Text.Trim(), TxtDescription.Text.Trim());
            RefreshGrid();
            AresMessageBox.Show($"Tarea añadida: {item.Id}", "ARES — Automation Studio");
        }
        catch (Exception ex)
        {
            AresMessageBox.Show($"No se pudo añadir la tarea:\n{ex.Message}", "ARES — Error");
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (GridTasks.SelectedItem is not ScheduledTaskItem selected)
        {
            AresMessageBox.Show("Selecciona una tarea primero.", "ARES — Automation Studio");
            return;
        }

        var ok = _store.Remove(selected.Id);
        RefreshGrid();
        AresMessageBox.Show(ok ? "Tarea eliminada." : "No se pudo eliminar.", "ARES — Automation Studio");
    }

    private async void Simulate_Click(object sender, RoutedEventArgs e)
    {
        var tool = new ScheduleSimulateTool(_store, _permission);
        var result = await tool.ExecuteAsync(new Dictionary<string, JToken>
        {
            ["hours_ahead"] = 24
        });

        AresMessageBox.Show(result.Message, "ARES — Simulación");
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
