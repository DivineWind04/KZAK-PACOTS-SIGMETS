using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using vatsys;
using vatsys.Plugin;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using static vatsys.Airspace2;
using static vatsys.DefaultJurisdiction;
using static vatsys.DisplayMaps.Map.Label;
using static vatsys.SectorsVolumes;
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

        private static CustomToolStripMenuItem _TDMMenu;
        private static TDMWindow _tdmWindow;
        public Plugin()
        {
            _TDMMenu = new CustomToolStripMenuItem(CustomToolStripMenuItemWindowType.Main, CustomToolStripMenuItemCategory.Windows, new ToolStripMenuItem("TDM Tracks"));
            _TDMMenu.Item.Click += TDMMenu_Click;
            MMI.AddCustomMenuItem(_TDMMenu);

            Go();
            _ = GetSigmets();


            UpdateTimer.Elapsed += DataTimer_Elapsed;
            UpdateTimer.Interval = TimeSpan.FromMinutes(UpdateMinutes).TotalMilliseconds;
            UpdateTimer.AutoReset = true;
            UpdateTimer.Start();
        }

        private async void DataTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Go();
            await GetSigmets();

        }

        private void TDMMenu_Click(object sender, EventArgs e)
        {
            ShowtdmWindow();
        }

        private static void ShowtdmWindow()
        {
            MMI.InvokeOnGUI((MethodInvoker)delegate ()
            {
                if (_tdmWindow == null || _tdmWindow.IsDisposed)
                {
                    _tdmWindow = new TDMWindow();
                }
                else if (_tdmWindow.Visible) return;

                _tdmWindow.Show();
            });
        }
        public static async void Go()
        {
            RemoveTracks();

            try
            {
                await GetSigmets();
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

            //KZAK PACOTS Formatting
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
                        var Intersections = new List<Airspace2.Intersection>();

                        if (fix != Intersections.Distinct())
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
            //RJJJ ATMC PACOTS Formatting
           foreach (var tdElement in tdElements)
           {
                if (!tdElement.InnerHtml.Contains("EASTBOUND PACOTS")) continue;

                var words = (IList<string>) new ArraySegment<string>(tdElement.InnerHtml.Replace('\n', ' ').Split(' '));

                var untilIdx = words.IndexOf("UNTIL");
                var startYear = int.Parse(words[untilIdx-1]);
                var startMonth = Array.IndexOf(_months, words[untilIdx-3]);

                var startDay = int.Parse(words[untilIdx - 4]);
                var startTimeSplit = words[untilIdx - 2].Split(':');
                var startHour = int.Parse(startTimeSplit[0]);
                var startMin = int.Parse(startTimeSplit[1]);
                var endYear = int.Parse(words[untilIdx + 4].Remove(1));
                var endMonth = Array.IndexOf(_months, words[untilIdx + 2]);
                var endDay = int.Parse(words[untilIdx + 1]);
                var endTimeSplit = words[untilIdx + 3].Split(':');
                var endHour = int.Parse(endTimeSplit[0]);
                var endMin = int.Parse(endTimeSplit[1]);

                var start = new DateTime(startYear, startMonth, startDay, startHour, startMin, 0);
                var end = new DateTime(endYear, endMonth, endDay, endHour, endMin, 0);

                // Some entries may contain multiple tracks
                while (true)
                {
                    var trackIdx = words.IndexOf("TRACK");

                    // If words doesn't contain the word TRACK, we are done processing this entry
                    if (trackIdx == -1) {
                        break;
                    }

                    var routeIdx = words.IndexOf("FLEX");
                    var trackIdTemp = words[trackIdx + 1];
                    // Remove period after the track id
                    var trackId = trackIdTemp.Substring(0, trackIdTemp.Length - 1);
                    var fixes = new List<Fix>();
                    int cutoffIdx = 0;

                    for (int rt_i = routeIdx + 3; rt_i < words.Count; rt_i++)
                    {
                        // If the current word is RMK or the word after is ROUTE, assume that
                        // this is no longer part of the flex route so break out of for-loop
                        if (words[rt_i + 1] == "ROUTE" || words[rt_i] == "RMK")
                        {
                            cutoffIdx = rt_i;
                            break;
                        }
                        var point = words[rt_i];

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

                    tracks.Add(new Track(trackId, start, end, fixes));
                    // Grab slice after the flex route to see if there are any more tracks to process
                    words = words.Skip(cutoffIdx).ToArray<string>();
                }
            }

            return tracks;
        }

        private static void RemoveTracks()
        {
            foreach (var track in Tracks)
            {
                var ra = RestrictedAreas.Instance.Areas.FirstOrDefault(x => x.Name == $"TDM {track.Id}");

                var currentTdm = ra.IsActive();

                if (ra == null || currentTdm) continue;

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

            //var coordinates = JsonSerializer.Deserialize<Coord>(content);


            var from = DateTimeOffset.FromUnixTimeSeconds(long.Parse(sigmets.ValidTimeFrom.ToString("HHmm"))).DateTime;
            var to = DateTimeOffset.FromUnixTimeSeconds(long.Parse(sigmets.ValidTimeTo.ToString("HHmm"))).DateTime;

            foreach (var sig in Sigmets)
            {
                if (sig.FirId != "KZAK") continue;

                for (int c = 0; c < sig.Coords.Count; c++)
                {
                    double latitude = sigmets.Coords[c].Lat;
                    double longitude = sigmets.Coords[c].Lon;
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
