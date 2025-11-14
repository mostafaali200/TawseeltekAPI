namespace WebApplication1.Dto
{
    public class FindDriversDTO
    {
        public double FromLat { get; set; }
        public double FromLng { get; set; }
        public double ToLat { get; set; }
        public double ToLng { get; set; }
        public DateTime DesiredTime { get; set; }
    }
}
