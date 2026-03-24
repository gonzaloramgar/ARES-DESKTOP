// Resolve WPF vs WinForms ambiguous types globally
global using Application = System.Windows.Application;
global using UserControl = System.Windows.Controls.UserControl;
global using Color = System.Windows.Media.Color;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using MessageBox = System.Windows.MessageBox;
global using MessageBoxButton = System.Windows.MessageBoxButton;
global using MessageBoxImage = System.Windows.MessageBoxImage;
global using MessageBoxResult = System.Windows.MessageBoxResult;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using Clipboard = System.Windows.Clipboard;
global using ColorConverter = System.Windows.Media.ColorConverter;
global using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
// System.IO types not included in WinForms implicit usings
global using System.IO;
