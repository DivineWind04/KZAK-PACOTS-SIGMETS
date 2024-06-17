using System;
using System.Globalization;
using System.Text;

namespace NATPlugin
{
    public class Fix
    {
        public string Name { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public Fix(string name, double lat = 0.0, double lon = 0.0)
        {
            this.Name = name;
            this.Latitude = lat;
            this.Longitude = lon;
        }

        public string Coordinate()
        {
            StringBuilder stringBuilder = new StringBuilder();
            NumberFormatInfo invariantInfo = NumberFormatInfo.InvariantInfo;
            GetDM(out var latDeg, out var latMin, out var north, out var lonDeg, out var lonMin, out var east);
            stringBuilder.AppendFormat(invariantInfo, "{0:0#}{1:0#}{2}", latDeg, latMin, north ? "N" : "S");
            stringBuilder.AppendFormat(invariantInfo, "{0:0##}{1:0#}{2}", lonDeg, lonMin, east ? "E" : "W");
            return stringBuilder.ToString();
        }

        public void GetDM(out float latDeg, out float latMin, out bool north, out float lonDeg, out float lonMin, out bool east)
        {
            double latitude = Latitude;
            north = latitude >= 0.0;
            double num = Math.Abs(latitude);
            latDeg = (float)Math.Truncate(num);
            num -= (double)latDeg;
            num *= 60.0;
            latMin = (float)num;
            double longitude = Longitude;
            east = longitude >= 0.0;
            double num2 = Math.Abs(longitude);
            lonDeg = (float)Math.Truncate(num2);
            num2 -= (double)lonDeg;
            num2 *= 60.0;
            lonMin = (float)num2;
        }
    }
}
