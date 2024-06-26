using System;
using System.Collections.Generic;
using System.Linq;

namespace NATPlugin
{
    public class Track
    {
        public Track() { }

        public Track(string id, DateTime start, DateTime end, List<Fix> fixes)
        {
            Id = id;
            Start = start;
            End = end;
            Fixes = fixes;
        }

        public string Id { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string start { get; set; }
        public string end { get; set; }
        public List<Fix> Fixes { get; set; } = new List<Fix>();

        public string RouteDisplay => string.Join(" ", Fixes.Select(x => x.Name));
        public DateTime Start2 => DateTimeOffset.FromUnixTimeSeconds(long.Parse(start)).DateTime;

        public DateTime End2 => DateTimeOffset.FromUnixTimeSeconds(long.Parse(end)).DateTime;
        public string StartDisplay => Start.ToString("HHmm");
        public string EndDisplay => End.ToString("HHmm");

    }
}
