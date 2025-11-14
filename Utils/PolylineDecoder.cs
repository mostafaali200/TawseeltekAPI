using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace TawseeltekAPI.Utils
{
    public static class PolylineDecoder
    {
        // 🚀 Cache للـ polyline (تسريع ×3)
        private static readonly ConcurrentDictionary<string, List<(double Lat, double Lng)>> Cache
            = new ConcurrentDictionary<string, List<(double Lat, double Lng)>>();

        /// <summary>
        /// فك ترميز Google Encoded Polyline إلى List من الإحداثيات (Lat, Lng).
        /// نسخة مُسرّعة + آمنة + تحتوي Cache.
        /// </summary>
        public static List<(double Lat, double Lng)> DecodePolyline(string encodedPolyline)
        {
            if (string.IsNullOrWhiteSpace(encodedPolyline))
                return new List<(double Lat, double Lng)>();

            // 🟢 استخدم الكاش لو موجود
            if (Cache.TryGetValue(encodedPolyline, out var cached))
                return cached;

            var result = new List<(double Lat, double Lng)>();

            try
            {
                var polylineChars = encodedPolyline.ToCharArray();
                int index = 0;
                int currentLat = 0;
                int currentLng = 0;

                while (index < polylineChars.Length)
                {
                    // Latitude
                    int shift = 0;
                    int value = 0;
                    int b;

                    do
                    {
                        if (index >= polylineChars.Length)
                            return SaveAndReturn(encodedPolyline, result);

                        b = polylineChars[index++] - 63;
                        value |= (b & 0x1F) << shift;
                        shift += 5;

                    } while (b >= 0x20);

                    int dlat = ((value & 1) != 0 ? ~(value >> 1) : (value >> 1));
                    currentLat += dlat;

                    // Longitude
                    shift = 0;
                    value = 0;

                    do
                    {
                        if (index >= polylineChars.Length)
                            return SaveAndReturn(encodedPolyline, result);

                        b = polylineChars[index++] - 63;
                        value |= (b & 0x1F) << shift;
                        shift += 5;

                    } while (b >= 0x20);

                    int dlng = ((value & 1) != 0 ? ~(value >> 1) : (value >> 1));
                    currentLng += dlng;

                    result.Add((currentLat / 1E5, currentLng / 1E5));
                }
            }
            catch
            {
                // ❗ إذا صار خطأ… رجّع قائمة فاضية (بدون كراش)
                return SaveAndReturn(encodedPolyline, new List<(double Lat, double Lng)>());
            }

            // 🟢 خزن في الكاش
            return SaveAndReturn(encodedPolyline, result);
        }

        private static List<(double Lat, double Lng)> SaveAndReturn(
            string key, List<(double Lat, double Lng)> poly)
        {
            Cache[key] = poly;
            return poly;
        }
    }
}
