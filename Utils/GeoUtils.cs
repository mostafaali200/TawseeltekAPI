using System;
using System.Collections.Generic;

namespace TawseeltekAPI.Utils
{
    public static class GeoUtils
    {
        private const double EarthRadiusKm = 6371.0;

        /// <summary>
        /// احسب المسافة بين نقطتين (Haversine).
        /// </summary>
        public static double Haversine(double lat1, double lng1, double lat2, double lng2)
        {
            double dLat = ToRadians(lat2 - lat1);
            double dLng = ToRadians(lng2 - lng1);

            lat1 = ToRadians(lat1);
            lat2 = ToRadians(lat2);

            double a = Math.Pow(Math.Sin(dLat / 2), 2) +
                       Math.Pow(Math.Sin(dLng / 2), 2) *
                       Math.Cos(lat1) * Math.Cos(lat2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadiusKm * c;
        }

        /// <summary>
        /// احسب أقصر مسافة من نقطة إلى مسار (Polyline).
        /// </summary>
        public static double DistanceToPolyline(double lat, double lng, List<(double Lat, double Lng)> polyline)
        {
            if (polyline == null || polyline.Count < 2) return double.MaxValue;

            double minDistance = double.MaxValue;

            for (int i = 0; i < polyline.Count - 1; i++)
            {
                var p1 = polyline[i];
                var p2 = polyline[i + 1];

                double dist = DistancePointToSegment(lat, lng, p1.Lat, p1.Lng, p2.Lat, p2.Lng);
                if (dist < minDistance)
                    minDistance = dist;
            }

            return minDistance;
        }

        /// <summary>
        /// المسافة من نقطة إلى خط مستقيم (جزء من Polyline).
        /// </summary>
        private static double DistancePointToSegment(double px, double py, double x1, double y1, double x2, double y2)
        {
            double A = px - x1;
            double B = py - y1;
            double C = x2 - x1;
            double D = y2 - y1;

            double dot = A * C + B * D;
            double lenSq = C * C + D * D;
            double param = lenSq != 0 ? dot / lenSq : -1;

            double xx, yy;

            if (param < 0)
            {
                xx = x1;
                yy = y1;
            }
            else if (param > 1)
            {
                xx = x2;
                yy = y2;
            }
            else
            {
                xx = x1 + param * C;
                yy = y1 + param * D;
            }

            return Haversine(px, py, xx, yy);
        }

        private static double ToRadians(double angle) => Math.PI * angle / 180.0;
    }
}
