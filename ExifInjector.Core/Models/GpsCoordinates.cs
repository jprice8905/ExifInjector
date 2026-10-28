using System.Globalization;

namespace ExifInjector.Core.Models;

/// <summary>A WGS-84 coordinate pair in signed decimal degrees.</summary>
public sealed record GpsCoordinates(double Latitude, double Longitude)
{
    public string LatitudeRef => Latitude >= 0 ? "N" : "S";

    public string LongitudeRef => Longitude >= 0 ? "E" : "W";

    public string LatitudeValue => Math.Abs(Latitude).ToString("0.########", CultureInfo.InvariantCulture);

    public string LongitudeValue => Math.Abs(Longitude).ToString("0.########", CultureInfo.InvariantCulture);

    /// <summary>
    /// Parses signed decimal degrees from two strings. Returns false (with a reason) when the values are unusable.
    /// </summary>
    public static bool TryParse(string? latitude, string? longitude, out GpsCoordinates? coordinates, out string? error)
    {
        coordinates = null;
        error = null;

        var hasLatitude = !string.IsNullOrWhiteSpace(latitude);
        var hasLongitude = !string.IsNullOrWhiteSpace(longitude);

        if (!hasLatitude && !hasLongitude)
        {
            return true;
        }

        if (hasLatitude != hasLongitude)
        {
            error = "Both latitude and longitude are required to write a GPS location.";
            return false;
        }

        if (!double.TryParse(latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLatitude) ||
            !double.TryParse(longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLongitude))
        {
            error = "Latitude and longitude must be decimal degrees, for example 47.6062 and -122.3321.";
            return false;
        }

        if (parsedLatitude is < -90 or > 90)
        {
            error = "Latitude must be between -90 and 90.";
            return false;
        }

        if (parsedLongitude is < -180 or > 180)
        {
            error = "Longitude must be between -180 and 180.";
            return false;
        }

        coordinates = new GpsCoordinates(parsedLatitude, parsedLongitude);
        return true;
    }
}
