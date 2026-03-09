using OwlCore.Extensions;

namespace OwlCore.Storage.FluentFTP;

/// <summary>
/// A timer-based property watcher for FTP storage properties.
/// </summary>
/// <typeparam name="T">The type of the property value.</typeparam>
public class FtpTimerBasedPropertyWatcher<T> : IStoragePropertyWatcher<T>
{
    private readonly Timer _timer;
    private T _lastValue;

    /// <summary>
    /// Creates a new instance of <see cref="FtpTimerBasedPropertyWatcher{T}"/>.
    /// </summary>
    /// <param name="property">The property being watched.</param>
    /// <param name="interval">How often checks for updates should be made.</param>
    /// <param name="initialValue">The initial value to poll against and fire changed events if different.</param>
    public FtpTimerBasedPropertyWatcher(IStorageProperty<T> property, TimeSpan interval, T initialValue)
    {
        Property = property;

        // Capture initial value synchronously to establish baseline before returning
        // This ensures we have a baseline before any event handlers can trigger updates
        _lastValue = initialValue;

        _timer = new Timer(_ => ExecuteAsync().Forget());
        _timer.Change(interval, interval);
    }

    /// <inheritdoc/>
    public IStorageProperty<T> Property { get; }

    /// <inheritdoc/>
    public event EventHandler<T>? ValueUpdated;

    /// <summary>
    /// Executes the property check.
    /// </summary>
    private async Task ExecuteAsync()
    {
        try
        {
            var currentValue = await Property.GetValueAsync(CancellationToken.None);

            if (!EqualityComparer<T>.Default.Equals(currentValue, _lastValue))
            {
                _lastValue = currentValue;
                ValueUpdated?.Invoke(this, currentValue);
            }
        }
        catch
        {
            // Ignore errors during polling - connection may be temporarily unavailable
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
#pragma warning disable CA1816
    public ValueTask DisposeAsync()
#pragma warning restore CA1816
    {
        Dispose();
        return default;
    }
}
