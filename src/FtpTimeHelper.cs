using FluentFTP;

namespace OwlCore.Storage.FluentFTP;

/// <summary>
/// Helper for converting FTP timestamps to/from local time based on FluentFTP configuration.
/// </summary>
internal static class FtpTimeHelper
{
    /// <summary>
    /// Converts a timestamp from FluentFTP to local time based on the client's TimeConversion setting.
    /// </summary>
    /// <param name="ftpTime">The timestamp from FluentFTP (already processed per TimeConversion).</param>
    /// <param name="config">The FTP client configuration.</param>
    /// <returns>The timestamp in local time (Kind=Local).</returns>
    public static DateTime ToLocalTime(DateTime ftpTime, FtpConfig config)
    {
        var result = config.TimeConversion switch
        {
            // FluentFTP already converted to local time
            FtpDate.LocalTime => DateTime.SpecifyKind(ftpTime, DateTimeKind.Local),
            
            // FluentFTP converted to UTC; convert to local
            FtpDate.UTC => ftpTime.Kind == DateTimeKind.Utc 
                ? ftpTime.ToLocalTime() 
                : DateTime.SpecifyKind(ftpTime, DateTimeKind.Utc).ToLocalTime(),
            
            // ServerTime: no conversion done by FluentFTP; use TimeZone offset to convert
            FtpDate.ServerTime => TimeZoneInfo.ConvertTime(
                DateTime.SpecifyKind(ftpTime, DateTimeKind.Unspecified),
                config.ServerTimeZone,
                config.ClientTimeZone
            ),
            
            _ => DateTime.SpecifyKind(ftpTime, DateTimeKind.Local)
        };

        // Truncate to second precision (FTP servers don't preserve milliseconds)
        return new DateTime(result.Ticks - (result.Ticks % TimeSpan.TicksPerSecond), result.Kind);
    }

    /// <summary>
    /// Converts a local timestamp to what FluentFTP expects based on the client's TimeConversion setting.
    /// </summary>
    /// <param name="localTime">The local timestamp.</param>
    /// <param name="config">The FTP client configuration.</param>
    /// <returns>The timestamp in the format FluentFTP expects.</returns>
    public static DateTime FromLocalTime(DateTime localTime, FtpConfig config)
    {
        // Ensure we're working with local time
        var local = localTime.Kind == DateTimeKind.Local 
            ? localTime 
            : localTime.ToLocalTime();

        var result = config.TimeConversion switch
        {
            // FluentFTP expects local time
            FtpDate.LocalTime => local,
            
            // FluentFTP expects UTC
            FtpDate.UTC => local.ToUniversalTime(),

            // FluentFTP expects server time; convert from local to server timezone
            FtpDate.ServerTime => TimeZoneInfo.ConvertTime(
                DateTime.SpecifyKind(local, DateTimeKind.Unspecified),
                config.ClientTimeZone,
                config.ServerTimeZone
            ),

            _ => local
        };

        // Truncate to second precision (FTP servers don't preserve milliseconds)
        return new DateTime(result.Ticks - (result.Ticks % TimeSpan.TicksPerSecond), result.Kind);
    }
}
