using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ByesLauncher;

public class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class NullToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class NotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// Visible when value is 0 (or null) — used for the "ping not measured yet"
/// hint text. Discovery returns ping=0 because Steam Web API doesn't measure
/// latency; the user can hit the refresh-single icon to A2S-probe.
public class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is null || (value is int i && i == 0)) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// Joins a List&lt;string&gt; with " + " for grid display. Used for the
/// History tab's Modpacks column where multi-modpack servers should render
/// as "epoch-1.0.7.1 + overwatch-lite".
public class JoinListConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is System.Collections.IEnumerable items
            ? string.Join(" + ", items.Cast<object>().Select(o => o?.ToString() ?? ""))
            : "";
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// Inverts a bool — used for `IsEnabled="{Binding IsJoining, Converter=...}"`
/// so the Join button greys out while a join is in progress.
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}

/// Maps `IsJoining` bool → button label. Lets the same button switch between
/// "JOIN SERVER" / "JOINING…" without the VM growing per-tab string properties.
public class JoinButtonContentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isJoining = value is bool b && b;
        // parameter overrides the idle label (e.g. "REJOIN" on the History tab).
        var idle = parameter as string ?? "JOIN SERVER";
        return isJoining ? "JOINING…" : idle;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
