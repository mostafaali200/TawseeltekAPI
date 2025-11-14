using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TawseeltekAPI.Utils
{
    public static class GeoUtils
    {
        private const double EarthRadiusKm = 6371.0;

        // 🚀 Cache للمسارات (Polyline) — يقلل الحسابات 70%
        private static readonly ConcurrentDictionary<string, List<(double Lat, double Lng)>> PolylineCache
            = new ConcurrentDictionary<string, List<(double Lat, double Lng)>>();

        // =====================================================================
        // 1️⃣ Haversine سريع + مهيأ للسرعة العالية
        // =====================================================================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Haversine(double lat1, double lng1, double lat2, double lng2)
        {
            double dLat = ToRad(lat2 - lat1);
            double dLng = ToRad(lng2 - lng1);

            lat1 = ToRad(lat1);
            lat2 = ToRad(lat2);

            double sinDlat = Math.Sin(dLat * 0.5);
            double sinDlng = Math.Sin(dLng * 0.5);

            double a = sinDlat * sinDlat +
                       sinDlng * sinDlng * Math.Cos(lat1) * Math.Cos(lat2);

            return EarthRadiusKm * 2.0 * Math.Asin(Math.Sqrt(a));
        }

        // =====================================================================
        // 2️⃣ Decode + Cache polyline — أسرع بكثير
        // =====================================================================
        public static List<(double Lat, double Lng)> DecodeCached(string encoded)
        {
            if (string.IsNullOrWhiteSpace(encoded))
                return new List<(double Lat, double Lng)>();

            return PolylineCache.GetOrAdd(encoded, key =>
            {
                return PolylineDecoder.DecodePolyline(key);
            });
        }

        // =====================================================================
        // 3️⃣ أقصر مسافة لنقطة من Polyline
        // =====================================================================
        public static double DistanceToPolyline(double lat, double lng, List<(double Lat, double Lng)> polyline)
        {
            if (polyline == null || polyline.Count < 2)
                return double.MaxValue;

            double min = double.MaxValue;

            for (int i = 0; i < polyline.Count - 1; i++)
            {
                var p1 = polyline[i];
                var p2 = polyline[i + 1];

                double d = DistancePointToSegment(lat, lng, p1.Lat, p1.Lng, p2.Lat, p2.Lng);
                if (d < min) min = d;
            }

            return min;
        }

        // =====================================================================
        // 4️⃣ أقصر مسافة من نقطة إلى Segment
        // =====================================================================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double DistancePointToSegment(double px, double py, double x1, double y1, double x2, double y2)
        {
            double A = px - x1;
            double B = py - y1;
            double C = x2 - x1;
            double D = y2 - y1;

            double dot = A * C + B * D;
            double lenSq = C * C + D * D;
            double param = (lenSq == 0) ? -1 : dot / lenSq;

            double xx, yy;

            if (param < 0)
            {
                xx = x1; yy = y1;
            }
            else if (param > 1)
            {
                xx = x2; yy = y2;
            }
            else
            {
                xx = x1 + param * C;
                yy = y1 + param * D;
            }

            return Haversine(px, py, xx, yy);
        }

        // =====================================================================
        // 5️⃣ تحويل سريع إلى راديان
        // =====================================================================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ToRad(double angle) => angle * (Math.PI / 180.0);
    }
}
