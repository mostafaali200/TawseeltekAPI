using System;
using System.Collections.Generic;

namespace TawseeltekAPI.Utils
{
    public static class PolylineDecoder
    {
        /// <summary>
        /// فك ترميز Google Encoded Polyline إلى List من الإحداثيات (Lat, Lng).
        /// </summary>
        public static List<(double Lat, double Lng)> DecodePolyline(string encodedPolyline)
        {
            var polylineChars = encodedPolyline.ToCharArray();
            var poly = new List<(double Lat, double Lng)>();

            int index = 0;
            int currentLat = 0;
            int currentLng = 0;

            while (index < polylineChars.Length)
            {
                // Latitude
                int shift = 0;
                int result = 0;
                int b;
                do
                {
                    b = polylineChars[index++] - 63;
                    result |= (b & 0x1f) << shift;
                    shift += 5;
                } while (b >= 0x20);
                int dlat = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
                currentLat += dlat;

                // Longitude
                shift = 0;
                result = 0;
                do
                {
                    b = polylineChars[index++] - 63;
                    result |= (b & 0x1f) << shift;
                    shift += 5;
                } while (b >= 0x20);
                int dlng = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
                currentLng += dlng;

                poly.Add((currentLat / 1E5, currentLng / 1E5));
            }

            return poly;
        }
    }
}
