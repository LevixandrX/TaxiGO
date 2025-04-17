using GMap.NET;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace TaxiGO.Services
{
    public class YandexGeocodingService : IGeocodingService
    {
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly AsyncPolicy<string> _retryPolicy; // Используем AsyncPolicy<string>

        public YandexGeocodingService(IConfiguration configuration, IMemoryCache cache)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));

            // Настройка политики повторных попыток с экспоненциальной задержкой
            _retryPolicy = Policy<string> // Указываем тип результата
                .Handle<HttpRequestException>()
                .OrResult(r => r.Contains("quota", StringComparison.OrdinalIgnoreCase))
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }

        public async Task<PointLatLng?> GeocodeAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;

            // Проверяем кэш
            string cacheKey = $"Geocode_{address}";
            if (_cache.TryGetValue(cacheKey, out PointLatLng? cachedResult))
            {
                return cachedResult;
            }

            try
            {
                var apiKey = _configuration["YandexGeocode:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                    throw new InvalidOperationException("Yandex Geocode API key not found in configuration.");

                // Добавляем "Санкт-Петербург", если не указано
                if (!address.ToLower().Contains("санкт-петербург"))
                {
                    address = $"{address}, Санкт-Петербург";
                }

                var url = $"https://geocode-maps.yandex.ru/1.x/?apikey={apiKey}&geocode={Uri.EscapeDataString(address)}&format=json&results=1";

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "TaxiGO/1.0");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                    await client.GetStringAsync(url));

                var json = JsonDocument.Parse(response);
                var point = json.RootElement
                    .GetProperty("response")
                    .GetProperty("GeoObjectCollection")
                    .GetProperty("featureMember");

                if (point.GetArrayLength() == 0)
                    return null;

                var posElement = point[0]
                    .GetProperty("GeoObject")
                    .GetProperty("Point")
                    .GetProperty("pos");

                if (posElement.ValueKind != JsonValueKind.String)
                    return null;

                var pos = posElement.GetString();
                if (string.IsNullOrEmpty(pos))
                    return null;

                var coords = pos.Split(' ');
                if (coords.Length != 2)
                    return null;

                double lon = double.Parse(coords[0], System.Globalization.CultureInfo.InvariantCulture);
                double lat = double.Parse(coords[1], System.Globalization.CultureInfo.InvariantCulture);
                var result = new PointLatLng(lat, lon);

                // Сохраняем в кэш на 24 часа
                _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
                return result;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                await Task.Delay(1000); // Задержка для соблюдения лимитов API
            }
        }

        public async Task<string?> ReverseGeocodeAsync(PointLatLng point)
        {
            // Проверяем кэш
            string cacheKey = $"ReverseGeocode_{point.Lat}_{point.Lng}";
            if (_cache.TryGetValue(cacheKey, out string? cachedResult))
            {
                return cachedResult;
            }

            try
            {
                var apiKey = _configuration["YandexGeocode:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                    throw new InvalidOperationException("Yandex Geocode API key not found in configuration.");

                var url = $"https://geocode-maps.yandex.ru/1.x/?apikey={apiKey}&geocode={point.Lng},{point.Lat}&format=json&results=1";

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "TaxiGO/1.0");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                    await client.GetStringAsync(url));

                var json = JsonDocument.Parse(response);
                var geoObject = json.RootElement
                    .GetProperty("response")
                    .GetProperty("GeoObjectCollection")
                    .GetProperty("featureMember");

                if (geoObject.GetArrayLength() == 0)
                    return "Неизвестный адрес";

                var addressDetails = geoObject[0]
                    .GetProperty("GeoObject")
                    .GetProperty("metaDataProperty")
                    .GetProperty("GeocoderMetaData")
                    .GetProperty("Address");

                var parts = new List<string>();

                if (addressDetails.TryGetProperty("Components", out var components))
                {
                    foreach (var component in components.EnumerateArray())
                    {
                        var kind = component.GetProperty("kind").GetString();
                        var name = component.GetProperty("name").GetString();
                        if (kind == "street" || kind == "house")
                        {
                            if (!string.IsNullOrEmpty(name))
                            {
                                parts.Add(name);
                            }
                        }
                    }
                }

                if (!parts.Any() && addressDetails.TryGetProperty("formatted", out var formatted))
                {
                    var formattedText = formatted.GetString();
                    if (!string.IsNullOrEmpty(formattedText))
                    {
                        formattedText = formattedText.Replace("Санкт-Петербург", "").Replace("Россия", "").Replace("  ", " ").Trim();
                        formattedText = string.Join(", ", formattedText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
                        parts.Add(formattedText);
                    }
                }

                string formattedAddress = string.Join(", ", parts);
                var result = string.IsNullOrEmpty(formattedAddress) ? "Неизвестный адрес" : formattedAddress;

                // Сохраняем в кэш на 24 часа
                _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
                return result;
            }
            catch (Exception)
            {
                return "Неизвестный адрес";
            }
            finally
            {
                await Task.Delay(1000); // Задержка для соблюдения лимитов API
            }
        }
    }
}