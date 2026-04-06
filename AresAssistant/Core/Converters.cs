using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Text.RegularExpressions;

namespace AresAssistant.Core;

/// <summary>
/// Convierte un booleano en alineación horizontal:
/// true → Right (mensajes del usuario), false → Left (mensajes del asistente).
/// </summary>
public class BoolToAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Convierte un booleano en Visibility: true → Visible, false → Collapsed.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Invierte un valor booleano: true → false, false → true.
/// </summary>
public class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;
}

/// <summary>
/// Convierte un booleano invertido en Visibility: true → Collapsed, false → Visible.
/// </summary>
public class InvertBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Convierte una cadena hexadecimal de color (ej: "#FF5722") en un SolidColorBrush.
/// Si el valor es inválido o vacío devuelve Transparent como fallback seguro.
/// </summary>
public class HexToColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try { return new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)); }
            catch { /* invalid hex → transparent fallback */ }
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Convierte cadena vacía/null en Visibility.Visible (para mostrar placeholders)
/// y cadena con contenido en Collapsed.
/// </summary>
public class StringEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Convierte texto markdown básico a FlowDocument para renderizar mensajes de chat.
/// Soporta negrita, código inline, listas simples y bloques ```.
/// </summary>
public class MarkdownToFlowDocumentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value?.ToString() ?? string.Empty;
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 13,
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextBrush"]
        };

        if (string.IsNullOrWhiteSpace(text))
            return doc;

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var list = default(System.Windows.Documents.List);
        var inCode = false;
        var codeBuffer = new List<string>();

        foreach (var raw in lines)
        {
            var line = raw ?? string.Empty;
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                if (inCode)
                {
                    var codePara = new Paragraph(new Run(string.Join(Environment.NewLine, codeBuffer)))
                    {
                        Margin = new Thickness(0, 4, 0, 8),
                        Padding = new Thickness(8, 6, 8, 6),
                        Background = new SolidColorBrush(Color.FromArgb(45, 16, 23, 36)),
                        FontFamily = new System.Windows.Media.FontFamily("Consolas")
                    };
                    doc.Blocks.Add(codePara);
                    codeBuffer.Clear();
                    inCode = false;
                }
                else
                {
                    if (list != null)
                    {
                        doc.Blocks.Add(list);
                        list = null;
                    }
                    inCode = true;
                }
                continue;
            }

            if (inCode)
            {
                codeBuffer.Add(line);
                continue;
            }

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
            {
                list ??= new System.Windows.Documents.List { Margin = new Thickness(12, 2, 0, 6) };
                var liPara = new Paragraph { Margin = new Thickness(0) };
                AddInlineMarkdown(liPara.Inlines, trimmed[2..]);
                list.ListItems.Add(new ListItem(liPara));
                continue;
            }

            if (list != null)
            {
                doc.Blocks.Add(list);
                list = null;
            }

            var para = new Paragraph { Margin = new Thickness(0, 0, 0, 5) };
            AddInlineMarkdown(para.Inlines, line);
            doc.Blocks.Add(para);
        }

        if (list != null)
            doc.Blocks.Add(list);

        if (inCode && codeBuffer.Count > 0)
        {
            var codePara = new Paragraph(new Run(string.Join(Environment.NewLine, codeBuffer)))
            {
                Margin = new Thickness(0, 4, 0, 8),
                Padding = new Thickness(8, 6, 8, 6),
                Background = new SolidColorBrush(Color.FromArgb(45, 16, 23, 36)),
                FontFamily = new System.Windows.Media.FontFamily("Consolas")
            };
            doc.Blocks.Add(codePara);
        }

        return doc;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static readonly Regex InlineRegex = new(@"(\*\*[^*]+\*\*|`[^`]+`)", RegexOptions.Compiled);

    private static void AddInlineMarkdown(InlineCollection inlines, string text)
    {
        var pos = 0;
        foreach (Match m in InlineRegex.Matches(text))
        {
            if (m.Index > pos)
                inlines.Add(new Run(text[pos..m.Index]));

            var token = m.Value;
            if (token.StartsWith("**") && token.EndsWith("**") && token.Length > 4)
            {
                inlines.Add(new Bold(new Run(token[2..^2])));
            }
            else if (token.StartsWith("`") && token.EndsWith("`") && token.Length > 2)
            {
                inlines.Add(new Run(token[1..^1])
                {
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    Background = new SolidColorBrush(Color.FromArgb(45, 20, 28, 43))
                });
            }
            else
            {
                inlines.Add(new Run(token));
            }

            pos = m.Index + m.Length;
        }

        if (pos < text.Length)
            inlines.Add(new Run(text[pos..]));
    }
}
