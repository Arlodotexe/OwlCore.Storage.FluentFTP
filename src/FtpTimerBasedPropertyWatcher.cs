using OwlCore.Extensions;

namespace OwlCore.Storage.FluentFTP;

/// <summary>
/// A timer-based property watcher for FTP storage properties.
/// </summary>
/// <typeparam name="T">The type of the property value.</typeparam>
public class FtpTimerBasedPropertyWatcher<T> : IStoragePropertyWatcher<T>
{
    private readonly Timer _timer;
    private T? _lastValue;
    private bool _hasLastValue;

    /// <summary>
    /// Creates a new instance of <see cref="FtpTimerBasedPropertyWatcher{T}"/>.
    /// </summary>
    /// <param name="property">The property being watched.</param>
    /// <param name="interval">How often checks for updates should be made.</param>
    public FtpTimerBasedPropertyWatcher(IStorageProperty<T> property, TimeSpan interval)
    {
        Property = property;

        // Capture initial value synchronously to establish baseline before returning
        // This ensures we have a baseline before any event handlers can trigger updates
        try
        {
            _lastValue = property.GetValueAsync(CancellationToken.None).GetAwaiter().GetResult();
            _hasLastValue = true;
        }
        catch
        {
            // If we can't get initial value, we'll capture it on first poll
        }

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

            if (!_hasLastValue)
            {
                _lastValue = currentValue;
                _hasLastValue = true;
                return;
            }

            if (!EqualityComparer<T>.Default.Equals(currentValue, _lastValue!))
            {
                _lastValue = currentValue;
                ValueUpdated?.Invoke(this, currentValue!);
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
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
}
