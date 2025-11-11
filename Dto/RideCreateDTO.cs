namespace TawseeltekAPI.Dto
{
    public class RideCreateDTO
    {
        public int DriverID { get; set; }
        public int FromCityID { get; set; }
        public int ToCityID { get; set; }
        public DateTime DepartureTime { get; set; }

        // جديد:
        public string RoutePolyline { get; set; } // Encoded polyline من Google Directions
        public int Capacity { get; set; } = 4;
        public decimal PricePerSeat { get; set; } = 0;
    }

    public class RidePassengerDTO
    {
        public int RideID { get; set; }
        public int PassengerID { get; set; }
        public decimal Fare { get; set; }
        public int SeatCount { get; set; } = 1;

    }

    // طلب بحث ذكي:
    public class RideSearchSmartDTO
    {
        public double FromLat { get; set; }
        public double FromLng { get; set; }
        public double ToLat { get; set; }
        public double ToLng { get; set; }
        public DateTime DesiredTime { get; set; }

        public double MaxPickupDetourKm { get; set; } = 5; // أقصى التفاف للسائق ليأخذ الراكب
        public double MaxRouteOffsetKm { get; set; } = 3;  // “على الطريق” ضمن 3 كم
        public int TimeFlexMinutes { get; set; } = 120;    // ± ساعتين
    }

    public class BestDriverResultDTO
    {
        public int RideID { get; set; }
        public int DriverID { get; set; }
        public string DriverName { get; set; }
        public string VehicleType { get; set; }
        public string PlateNumber { get; set; }
        public DateTime DepartureTime { get; set; }

        public double PickupDistanceKm { get; set; } // من موقع الالتقاط إلى أقرب نقطة على مسار السائق
        public double RouteOffsetKm { get; set; }    // مدى انحراف وجهة الراكب عن المسار
        public double TimeDiffMinutes { get; set; }  // فرق التوقيت
        public double Score { get; set; }            // كلما أكبر أفضل
        public double DriverLat { get; set; }
        public double DriverLng { get; set; }

    }
}
