using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using vatsys;
using vatsys.Plugin;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using static vatsys.DefaultJurisdiction;
using static vatsys.DisplayMaps.Map.Label;
using Timer = System.Timers.Timer;

namespace NATPlugin
{
    [Export(typeof(IPlugin))]
    public class Plugin : IPlugin
    {
        public string Name => "PACOTS Tracks";

        private static readonly string TrackUrl = "https://www.notams.faa.gov/dinsQueryWeb/queryRetrievalMapAction.do?retrieveLocId=KZAK%20RJJJ&actionType=notamRetrievalByICAOs&submit=NOTAMs";
        private static readonly string SigmetUrl = "https://aviationweather.gov/api/data/isigmet?format=json"; ///https://www.aviationweather.gov/cgi-bin/json/IsigmetJSON.php
        public static HttpClient _httpClient = new HttpClient();
        private static readonly string[] _months = { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
        private static readonly int UpdateMinutes = 15;

        public static List<Track> Tracks { get; set; } = new List<Track>();

        public static List<Sigmet> Sigmets { get; set; } = new List<Sigmet>();
        public static DateTime? LastUpdated { get; set; }
        private static Timer UpdateTimer { get; set; } = new Timer();

        public static event EventHandler TracksUpdated;

        public Plugin()
        {
            Go();

            _ = GetSigmets();

            UpdateTimer.Elapsed += DataTimer_Elapsed;
            UpdateTimer.Interval = TimeSpan.FromMinutes(UpdateMinutes).TotalMilliseconds;
            UpdateTimer.AutoReset = true;
            UpdateTimer.Start();
        }

        private void DataTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Go();
            _ = GetSigmets();
        }

        public static void Go()
        {
            RemoveTracks();

            try
            {
                
                Tracks = GetTracks();               

                foreach (var track in Tracks.OrderBy(x => x.Id))
                {
                    var area = new RestrictedAreas.RestrictedArea.Boundary();

                    foreach (var fix in track.Fixes)
                    {
                        area.List.Add(new Coordinate(fix.Latitude, fix.Longitude));
                    }

                    var activiations = new List<RestrictedAreas.RestrictedArea.Activation>
                    {
                        new RestrictedAreas.RestrictedArea.Activation(track.StartDisplay, track.EndDisplay)
                    };

                    var ra = new RestrictedAreas.RestrictedArea($"TDM {track.Id}", RestrictedAreas.AreaTypes.Danger, 0, 100)
                    {
                        Area = area,
                        LinePattern = DisplayMaps.Map.Patterns.Solid,       
                        DAIWEnabled = false,
                        Activations = activiations
                    };

                    RestrictedAreas.Instance.Areas.Add(ra);
                }

                LastUpdated = DateTime.UtcNow;

                TracksUpdated?.Invoke(null, new EventArgs());
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"Error loading tracks: {ex.Message}"), "PACOTS Plugin");
            }

        }

        public static List<Track> GetTracks()
        {

            var tracks = new List<Track>();

            var web = new HtmlWeb();

            var htmlDoc = web.Load(TrackUrl);

            var tdElements = htmlDoc.DocumentNode.SelectNodes("//td[@class='textBlack12']");

            //string start;
            //string end;

            foreach (var tdElement in tdElements)
            {
                if (!tdElement.InnerHtml.Contains("TDM TRK")) continue;

                var lines = tdElement.InnerHtml.Split('\n');

                var info = lines[0].Split(' ');

                var validity = lines[1].Split(' ');

                var start = ToDateTime(validity[0]);

                var end = ToDateTime(validity[1]);

                var route = lines[2];

                if (!lines[3].StartsWith("RTS/")) route += lines[3].Trim();

                var fixes = new List<Fix>();



                foreach (var point in route.Split(' '))
                {
                    if (string.IsNullOrWhiteSpace(point)) continue;

                    var isMatch = Regex.Match(point, "[0-9]{2}N[0-9]{3}[E,W]");

                    if (isMatch.Success)
                    {
                        // Lat Long

                        double latitude = double.Parse(point.Substring(0, 2));

                        double longitude;

                        if (point.Substring(6, 1) == "E")
                        {
                            longitude = double.Parse(point.Substring(3, 3));
                        }
                        else
                        {
                            longitude = -double.Parse(point.Substring(3, 3));
                        }

                        fixes.Add(new Fix(point, latitude, longitude));
                    }
                    else
                    {
                        // Waypoint. Need to check for duplicate  fixes
                        var fix = Airspace2.GetIntersection(point);

                        if (fix != null)
                        {
                            fixes.Add(new Fix(point, fix.LatLong.Latitude, fix.LatLong.Longitude));
                        }
                        else
                        {
                            Errors.Add(new Exception($"Could not find fix: {point}"), "PACOTS Plugin");
                        }
                    }
                }

                tracks.Add(new Track(info[4], start, end, fixes));
            }

           //foreach (var tdElement in tdElements)
           //{
           //    if (!tdElement.InnerHtml.Contains("EASTBOUND PACOTS")) continue;
           //
           //    var lines = tdElement.InnerHtml.Split('\n');
           //
           //    var info = lines[1].Split(' ');
           //
           //
           //
           //        var untilSplit = tdElement.InnerHtml.Split(' ');
           //        bool reached = false;
           //        var startYear = untilSplit[3];
           //        var startMonth = untilSplit[1];
           //        var startDay = untilSplit[0];
           //        var startHour = untilSplit[2];
           //        var startMin = untilSplit[2];
           //        var endYear = untilSplit[1];
           //        var endMonth = untilSplit[1];
           //        var endDay = untilSplit[2];
           //        var endHour = untilSplit[2];
           //        var endMin = untilSplit[2];
           //        var start = startYear + startMonth + startDay + startHour + startMin;
           //        var end = endYear + endMonth + endDay + endHour + endMin;
           //        List<string> validities = new List<string>();
           //
           //    for (int j = 0; j < _months.Length; j++)
           //    {
           //
           //        if (untilSplit.Contains(_months[j]))
           //        {
           //            // Parse the time
           //            DateTime time = new DateTime(DateTime.UtcNow.Year, j + 1, Convert.ToInt32(start.Split(' ')[0]), Convert.ToInt32(start.Split(' ')[1].Substring(0, 2)), Convert.ToInt32(start.Split(' ')[1].Substring(2, 2)), 0);
           //            start = Convert.ToString(time.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
           //            time = new DateTime(DateTime.UtcNow.Year, j + 1, Convert.ToInt32(end.Split(' ')[0]), Convert.ToInt32(end.Split(' ')[1].Substring(0, 2)), Convert.ToInt32(end.Split(' ')[1].Substring(2, 2)), 0);
           //            end = Convert.ToString(time.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
           //            validities.Add(start + "?" + end + "?");
           //            reached = true;
           //        }
           //        if (reached) // For performance
           //        {
           //            break;
           //        }
           //
           //    }





                   // if (reached) // For performance
                   // {
                   //     break;
                   // }
                   //
                   //
                   //
                   //
                   // var route = lines[2];
                   // if (!lines[3].StartsWith("JAPAN ROUTE") || !lines[3].StartsWith("RCTP / VHHH ROUTE")) route += lines[3].Trim();
                   //
                   // var fixes = new List<Fix>();
                   //
                   //
                   //
                   // foreach (var point in route.Split(' '))
                   // {
                   //     if (string.IsNullOrWhiteSpace(point)) continue;
                   //
                   //     var isMatch = Regex.Match(point, "[0-9]{2}N[0-9]{3}[E,W]");
                   //
                   //     if (isMatch.Success)
                   //     {
                   //         // Lat Long
                   //
                   //         double latitude = double.Parse(point.Substring(0, 2));
                   //
                   //         double longitude;
                   //
                   //         if (point.Substring(6, 1) == "E")
                   //         {
                   //             longitude = double.Parse(point.Substring(3, 3));
                   //         }
                   //         else
                   //         {
                   //             longitude = -double.Parse(point.Substring(3, 3));
                   //         }
                   //
                   //         fixes.Add(new Fix(point, latitude, longitude));
                   //     }
                   //     else
                   //     {
                   //         // Waypoint. Need to check for duplicate  fixes
                   //         var fix = Airspace2.GetIntersection(point);
                   //
                   //         if (fix != null)
                   //         {
                   //             fixes.Add(new Fix(point, fix.LatLong.Latitude, fix.LatLong.Longitude));
                   //         }
                   //         else
                   //         {
                   //             Errors.Add(new Exception($"Could not find fix: {point}"), "PACOTS Plugin");
                   //         }
                   //     }
                   // }
                   //
                   // tracks.Add(new Track(info[1], DateTimeOffset.FromUnixTimeSeconds(long.Parse(start)).DateTime, DateTimeOffset.FromUnixTimeSeconds(long.Parse(end)).DateTime, fixes));


            //}

                return tracks;

            
        }
            
        

        private static void RemoveTracks()
        {
            foreach (var track in Tracks)
            {
                var ra = RestrictedAreas.Instance.Areas.FirstOrDefault(x => x.Name == $"TDM {track.Id}");

                if (ra == null) continue;

                RestrictedAreas.Instance.Areas.Remove(ra);
            }

            Tracks.Clear();
        }

        public static async Task GetSigmets()
        {
            var getSigmet = await _httpClient.GetAsync(SigmetUrl);

            var points = new List<Fix>();

            var poly = new List<Track>();

            if (!getSigmet.IsSuccessStatusCode)
            {
                return;
            }

            var content = await getSigmet.Content.ReadAsStringAsync();

            var sigmets = JsonSerializer.Deserialize<Sigmet>(content);

            var coordinates = JsonSerializer.Deserialize<Coord>(content);

            double latitude = coordinates.Lat;
            double longitude = coordinates.Lon;

            var from = DateTimeOffset.FromUnixTimeSeconds(long.Parse(sigmets.ValidTimeFrom.ToString())).DateTime;
            var to = DateTimeOffset.FromUnixTimeSeconds(long.Parse(sigmets.ValidTimeTo.ToString())).DateTime;

            foreach (var sig in Sigmets)
            {
                if (sig.FirId != "KZAK") continue;

                for (int c = 0; c < sig.Coords.Count; c++)
                {

                    points.Add(new Fix(sig.IsigmetId.ToString(), latitude, longitude));

                    poly.Add(new Track(sig.IsigmetId.ToString(), from, to, points));
                }

            }

            try
            {

                foreach (var sigmet in Sigmets.OrderBy(x => x.SeriesId))
                {
                    var area = new RestrictedAreas.RestrictedArea.Boundary();

                    foreach (var point in sigmet.Coords)
                    {
                        area.List.Add(new Coordinate(coordinates.Lat, coordinates.Lon));
                    }

                    var activiations = new List<RestrictedAreas.RestrictedArea.Activation>
                    {
                        new RestrictedAreas.RestrictedArea.Activation(from.ToString(), to.ToString())
                    };

                    var ra = new RestrictedAreas.RestrictedArea($"SIG {sigmet.SeriesId}", RestrictedAreas.AreaTypes.Danger, 0, sigmet.Top.Value)
                    {
                        Area = area,
                        LinePattern = DisplayMaps.Map.Patterns.Solid,
                        DAIWEnabled = true,
                        Activations = activiations
                    };

                    RestrictedAreas.Instance.Areas.Add(ra);
                }

                LastUpdated = DateTime.UtcNow;

                TracksUpdated?.Invoke(null, new EventArgs());
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"Error loading SIGMET: {ex.Message}"), "PACOTS Plugin");
            }
        }

        public static DateTime ToDateTime(string input) => new DateTime(int.Parse(input.Substring(0, 2)), int.Parse(input.Substring(2, 2)), int.Parse(input.Substring(4, 2)), int.Parse(input.Substring(6, 2)), int.Parse(input.Substring(8, 2)), 0, DateTimeKind.Utc);

        public void OnFDRUpdate(FDP2.FDR updated)
        {
            return;
        }

        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
            return;
        }
    }
}
