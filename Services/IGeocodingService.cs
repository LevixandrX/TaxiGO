using GMap.NET;

namespace TaxiGO.Services
{
    public interface IGeocodingService
    {
        Task<PointLatLng?> GeocodeAddressAsync(string address);
        Task<string?> ReverseGeocodeAsync(PointLatLng point);
    }
}