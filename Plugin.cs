using PACOTSPlugin;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using System.Xml;
using vatsys;
using vatsys.Properties;
using vatsys.Plugin;
using Timer = System.Timers.Timer;
using Track = PACOTSPlugin.Track;

namespace NATPlugin
{
    [Export(typeof(IPlugin))]
    public class Plugin : IPlugin
    {
        public string Name => "PACOTS Tracks";

        private const string CategoryName = "ATOP";
        private static readonly string SigmetUrl = "https://aviationweather.gov/api/data/isigmet?format=json";
        private const string SigmetMapName = "SIGMETS";
        private const string SigmetMapFileName = "SIGMET.XML";
        private static readonly TDMWindow TDMWindow = new TDMWindow();
        private static readonly object SigmetMapLock = new object();
        
        public static HttpClient _httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) };
        private static readonly int UpdateMinutes = 15;

        public static List<Track> Tracks { get; set; } = new List<Track>();
        public static List<Sigmets> SigmetAreas { get; set; } = new List<Sigmets>();
        public static List<Sigmet> Sigmets { get; set; } = new List<Sigmet>();
        public static List<Notam> Notams { get; set; } = new List<Notam>();
        public static DateTime? LastUpdated { get; set; }
        private static Timer UpdateTimer { get; set; } = new Timer();
        private static NotamService _notamService;

        public static event EventHandler TracksUpdated;
        public static event EventHandler NotamsUpdated;

        public Plugin()
        {
            // Set User-Agent for API requests
            try
            {
                if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                {
                    _httpClient.DefaultRequestHeaders.Add("User-Agent", "VATSYS-PACOTS-Plugin/1.0");
                }
            }
            catch { /* Ignore if header already exists */ }
            
            // Initialize NOTAM service with hardcoded credentials
            _notamService = new NotamService(_httpClient);
            
            // Initialize TDM menu item
            InitializeTDMMenu();
            
            // Run initialization on a background thread to avoid blocking plugin load
            Task.Run(async () =>
            {
                try
                {
                    // Fetch PACOTS tracks from NMS-API NOTAMs
                    await RefreshTracksFromNotamApiAsync();
                    
                    // Fetch and display SIGMETs
                    await RefreshSigmetsAsync();
                }
                catch (Exception ex)
                {
                    Errors.Add(new Exception($"Error during plugin initialization: {ex.Message}"), "PACOTS Plugin");
                }
            });

            UpdateTimer.Elapsed += DataTimer_Elapsed;
            UpdateTimer.Interval = TimeSpan.FromMinutes(UpdateMinutes).TotalMilliseconds;
            UpdateTimer.AutoReset = true;
            UpdateTimer.Start();
        }

        private void DataTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Task.Run(async () =>
            {
                try
                {
                    // Refresh PACOTS tracks from NMS-API
                    await RefreshTracksFromNotamApiAsync();
                    
                    // Refresh SIGMETs
                    await RefreshSigmetsAsync();
                }
                catch (Exception ex)
                {
                    Errors.Add(new Exception($"Error during timer update: {ex.Message}"), "PACOTS Plugin");
                }
            });
        }
        
        /// <summary>
        /// Refreshes PACOTS tracks by fetching NOTAMs from the FAA NMS-API and parsing them
        /// </summary>
        public static async Task RefreshTracksFromNotamApiAsync()
        {
            if (_notamService == null)
            {
                Errors.Add(new Exception("NOTAM service not initialized."), "PACOTS Plugin");
                return;
            }
            
            try
            {
                // Search for PACOTS NOTAMs using free text search
                var response = await _notamService.GetPacotsNotamsAsync();
                
                // Convert to simplified Notam objects
                var notams = Notam.FromResponse(response);
                
                // Store all NOTAMs
                Notams = notams;
                NotamsUpdated?.Invoke(null, EventArgs.Empty);
                
                // Parse PACOTS tracks from NOTAMs
                var tracks = ParsePacotsFromNotams(notams);
                
                // Update display
                RemoveTracks();
                Tracks = tracks;
                
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
            catch (NotamApiException ex)
            {
                Errors.Add(new Exception($"Error fetching NOTAMs from API: {ex.Message}"), "PACOTS Plugin");
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"Unexpected error refreshing tracks: {ex.Message}"), "PACOTS Plugin");
            }
        }
        /// <summary>
        /// Refreshes SIGMETs from the aviation weather API and updates dynamic XML/runtime map output.
        /// </summary>
        public static async Task RefreshSigmetsAsync()
        {
            try
            {
                var sigmets = await GetSigmets();

                // Keep XML map output and in-memory map in sync with current API data.
                UpdateSigmetXmlMapAndRuntimeMap(sigmets);
                
                // Store the new sigmets
                SigmetAreas = sigmets;
                
                // Also update the Sigmets list for backward compatibility
                Sigmets = sigmets.Select(s => new Sigmet
                {
                    SeriesId = s.Id,
                    ValidTimeFrom = (int)new DateTimeOffset(s.Start).ToUnixTimeSeconds(),
                    ValidTimeTo = (int)new DateTimeOffset(s.End).ToUnixTimeSeconds()
                }).ToList();

                // Ensure ASD redraws after SIGMET map/area update.
                MMI.RequestRedraw(true);
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"Error refreshing SIGMETs: {ex.Message}"), "PACOTS Plugin");
            }
        }

        private static void UpdateSigmetXmlMapAndRuntimeMap(List<Sigmets> sigmets)
        {
            lock (SigmetMapLock)
            {
                try
                {
                    var mapPath = EnsureSigmetMapPath();
                    WriteSigmetXmlMap(mapPath, sigmets);
                    UpsertRuntimeSigmetMap(sigmets);
                }
                catch (Exception ex)
                {
                    Errors.Add(new Exception($"Error updating SIGMET XML/runtime map: {ex.Message}"), "PACOTS Plugin");
                }
            }
        }

        private static string EnsureSigmetMapPath()
        {
            var mapsRoot = Path.Combine(Settings.Default.DatasetPath, "Maps");
            Directory.CreateDirectory(mapsRoot);
            return Path.Combine(mapsRoot, SigmetMapFileName);
        }

        private static void WriteSigmetXmlMap(string filePath, List<Sigmets> sigmets)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = false
            };

            using (var writer = XmlWriter.Create(filePath, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Maps");

                writer.WriteStartElement("Map");
                writer.WriteAttributeString("Type", "REST_NTZ_DAIW");
                writer.WriteAttributeString("Name", SigmetMapName);
                writer.WriteAttributeString("Priority", "1");
                writer.WriteAttributeString("CustomColourName", "PRDArea");

                foreach (var sigmet in sigmets.Where(s => s.Fixes != null && s.Fixes.Count >= 3))
                {
                    writer.WriteStartElement("Line");
                    writer.WriteAttributeString("Name", $"SIGMET {sigmet.Id}");
                    writer.WriteAttributeString("Pattern", "Dashed");
                    writer.WriteString(ToMapPointString(sigmet.Fixes));
                    writer.WriteEndElement();

                    var centroid = GetCentroid(sigmet.Fixes);
                    writer.WriteStartElement("Label");
                    writer.WriteAttributeString("HasLeader", "false");
                    writer.WriteStartElement("Point");
                    writer.WriteAttributeString("Name", $"SIG-{sigmet.Id}");
                    writer.WriteString(ToIsoPoint(centroid.Latitude, centroid.Longitude));
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void UpsertRuntimeSigmetMap(List<Sigmets> sigmets)
        {
            var map = DisplayMaps.Maps.FirstOrDefault(m =>
                string.Equals(m.Name, SigmetMapName, StringComparison.OrdinalIgnoreCase));

            if (map == null)
            {
                map = new DisplayMaps.Map
                {
                    Name = SigmetMapName,
                    Type = DisplayMaps.MapTypes.REST_NTZ_DAIW,
                    Category = DisplayMaps.MapCategories.ASD,
                    Pattern = DisplayMaps.Map.Patterns.Solid,
                    Priority = 1,
                    CustomColourName = "PRDArea"
                };
                DisplayMaps.Maps.Add(map);
            }

            map.Type = DisplayMaps.MapTypes.REST_NTZ_DAIW;
            map.Category = DisplayMaps.MapCategories.ASD;
            map.Priority = 1;
            map.CustomColourName = "PRDArea";
            map.Lines.Clear();
            map.Infills.Clear();
            map.Symbols.Clear();
            map.Labels.Clear();
            map.Runways.Clear();

            foreach (var sigmet in sigmets.Where(s => s.Fixes != null && s.Fixes.Count >= 3))
            {
                var line = new DisplayMaps.Map.Line
                {
                    Name = $"SIGMET {sigmet.Id}",
                    Pattern = DisplayMaps.Map.Patterns.Dashed
                };

                foreach (var fix in sigmet.Fixes)
                    line.Points.Add(new Coordinate(fix.Latitude, fix.Longitude));

                var first = sigmet.Fixes[0];
                var last = sigmet.Fixes[sigmet.Fixes.Count - 1];
                if (Math.Abs(first.Latitude - last.Latitude) > 0.001 || Math.Abs(first.Longitude - last.Longitude) > 0.001)
                    line.Points.Add(new Coordinate(first.Latitude, first.Longitude));

                if (line.Points.Count > 1)
                    map.Lines.Add(line);

                var centroid = GetCentroid(sigmet.Fixes);
                map.Labels.Add(new DisplayMaps.Map.Label
                {
                    Name = $"SIG-{sigmet.Id}",
                    Location = new Coordinate(centroid.Latitude, centroid.Longitude),
                    Leader = false
                });
            }

            DisplayMaps.Maps.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        private static string ToMapPointString(List<Fix> fixes)
        {
            var points = fixes.Select(f => ToIsoPoint(f.Latitude, f.Longitude)).ToList();
            var first = fixes[0];
            var last = fixes[fixes.Count - 1];
            if (Math.Abs(first.Latitude - last.Latitude) > 0.001 || Math.Abs(first.Longitude - last.Longitude) > 0.001)
                points.Add(ToIsoPoint(first.Latitude, first.Longitude));
            return string.Join("/", points);
        }

        private static string ToIsoPoint(double latitude, double longitude)
        {
            var lat = latitude.ToString("+00.000;-00.000", CultureInfo.InvariantCulture);
            var lon = longitude.ToString("+000.000;-000.000", CultureInfo.InvariantCulture);
            return lat + lon;
        }

        private static Fix GetCentroid(List<Fix> fixes)
        {
            var lat = fixes.Average(f => f.Latitude);
            var lon = fixes.Average(f => f.Longitude);
            return new Fix("C", lat, lon);
        }
        
        /// <summary>
        /// Parses PACOTS tracks from a list of NOTAMs
        /// </summary>
        private static List<Track> ParsePacotsFromNotams(List<Notam> notams)
        {
            var tracks = new List<Track>();
            
            foreach (var notam in notams)
            {
                // Get the NOTAM text content
                var notamText = notam.DisplayText ?? notam.Text ?? notam.IcaoMessage ?? notam.TraditionalMessage;
                
                if (string.IsNullOrEmpty(notamText))
                    continue;
                
                // Check if this NOTAM is PACOTS related
                if (!notamText.Contains("PACOTS") && !notamText.Contains("PACOT") && !notamText.Contains("TDM TRK"))
                    continue;
                
                try
                {
                    // Parse all tracks from this NOTAM (may contain multiple tracks)
                    var parsedTracks = ParseMultipleTracks(notamText, notam);
                    tracks.AddRange(parsedTracks);
                }
                catch (Exception ex)
                {
                    Errors.Add(new Exception($"Error parsing PACOTS track from NOTAM {notam.Number}: {ex.Message}"), "PACOTS Plugin");
                }
            }
            
            return tracks;
        }
        
        /// <summary>
        /// Parses multiple PACOTS tracks from a single NOTAM text
        /// Supports two formats:
        /// 1. RJJJ eastbound: "TRACK 1." followed by "FLEX ROUTE :" line
        /// 2. KZAK westbound: "(TDM TRK E 260218190001" with waypoints on following lines
        /// </summary>
        private static List<Track> ParseMultipleTracks(string notamText, Notam notam)
        {
            var tracks = new List<Track>();
            var lines = notamText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            DateTime start = notam.EffectiveStart ?? DateTime.UtcNow;
            DateTime end = notam.EffectiveEnd ?? DateTime.UtcNow.AddHours(24);
            
            string currentTrackId = null;
            var currentFixes = new List<Fix>();
            var currentWaypointText = new List<string>(); // Collect waypoint lines for TDM tracks
            bool isFlexRoute = false;
            bool isTdmTrack = false;
            bool isDoneCollectingWaypoints = false; // Flag to stop collecting after RTS/

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Format 1: RJJJ eastbound - "TRACK 1." or "TRACK 2." etc.
                var trackNumMatch = Regex.Match(trimmedLine, @"^TRACK\s+(\d+)\s*\.", RegexOptions.IgnoreCase);
                if (trackNumMatch.Success)
                {
                    // Save previous track if we have one
                    if (currentTrackId != null)
                    {
                        // For TDM tracks, parse all collected waypoint text at once
                        if (currentWaypointText.Count > 0)
                        {
                            var combinedText = string.Join(" ", currentWaypointText);
                            ParseWaypointsFromLine(combinedText, currentFixes);
                            currentWaypointText.Clear();
                        }
                        if (currentFixes.Count > 1)
                        {
                            tracks.Add(new Track(currentTrackId, start, end, new List<Fix>(currentFixes)));
                        }
                    }
                    
                    currentTrackId = trackNumMatch.Groups[1].Value;
                    currentFixes.Clear();
                    isFlexRoute = false;
                    isTdmTrack = false;
                    continue;
                }
                
                // Format 2: KZAK westbound - "(TDM TRK E 260218190001"
                var tdmMatch = Regex.Match(trimmedLine, @"^\(?\s*TDM\s+TRK\s+([A-Z])\s+(\d+)", RegexOptions.IgnoreCase);
                if (tdmMatch.Success)
                {
                    // Save previous track if we have one
                    if (currentTrackId != null)
                    {
                        // For TDM tracks, parse all collected waypoint text at once
                        if (currentWaypointText.Count > 0)
                        {
                            var combinedText = string.Join(" ", currentWaypointText);
                            ParseWaypointsFromLine(combinedText, currentFixes);
                            currentWaypointText.Clear();
                        }
                        if (currentFixes.Count > 1)
                        {
                            tracks.Add(new Track(currentTrackId, start, end, new List<Fix>(currentFixes)));
                        }
                    }
                    
                    currentTrackId = tdmMatch.Groups[1].Value;
                    currentFixes.Clear();
                    currentWaypointText.Clear();
                    isFlexRoute = false;
                    isTdmTrack = true;
                    isDoneCollectingWaypoints = false; // Reset for new track
                    continue;
                }
                
                // Parse validity period for TDM tracks: "2602181900 2602190800"
                if (isTdmTrack)
                {
                    var validityMatch = Regex.Match(trimmedLine, @"^(\d{10})\s+(\d{10})$");
                    if (validityMatch.Success)
                    {
                        try
                        {
                            start = ToDateTime(validityMatch.Groups[1].Value);
                            end = ToDateTime(validityMatch.Groups[2].Value);
                        }
                        catch { /* Use NOTAM effective dates */ }
                        continue;
                    }
                    
                    // For TDM tracks, collect waypoint lines until we hit RTS/ or RMK/
                    if (!isDoneCollectingWaypoints && !trimmedLine.StartsWith("RTS/") && !trimmedLine.StartsWith("RMK/") && !trimmedLine.StartsWith(")"))
                    {
                        // Collect the line instead of parsing immediately
                        currentWaypointText.Add(trimmedLine);
                        continue;
                    }
                    
                    // When we hit RTS/ or RMK/, parse all collected waypoints at once and stop collecting
                    if (trimmedLine.StartsWith("RTS/") || trimmedLine.StartsWith("RMK/"))
                    {
                        if (currentWaypointText.Count > 0)
                        {
                            var combinedText = string.Join(" ", currentWaypointText);
                            ParseWaypointsFromLine(combinedText, currentFixes);
                            currentWaypointText.Clear();
                        }
                        isDoneCollectingWaypoints = true; // Stop collecting after RTS/RMK
                        continue;
                    }
                }
                
                // Look for FLEX ROUTE line (RJJJ format) - this contains the actual waypoints
                if (trimmedLine.StartsWith("FLEX ROUTE", StringComparison.OrdinalIgnoreCase))
                {
                    isFlexRoute = true;
                    // Parse waypoints from this line (after the colon)
                    var colonIndex = trimmedLine.IndexOf(':');
                    if (colonIndex >= 0)
                    {
                        var routePart = trimmedLine.Substring(colonIndex + 1);
                        ParseWaypointsFromLine(routePart, currentFixes);
                    }
                    continue;
                }
                
                // If we're in a FLEX ROUTE section and line starts with whitespace, it's a continuation
                if (isFlexRoute && line.StartsWith(" ") && !trimmedLine.Contains("ROUTE"))
                {
                    ParseWaypointsFromLine(trimmedLine, currentFixes);
                    continue;
                }
                
                // Stop parsing FLEX ROUTE when we hit another section
                if (trimmedLine.Contains("ROUTE") && !trimmedLine.StartsWith("FLEX", StringComparison.OrdinalIgnoreCase))
                {
                    isFlexRoute = false;
                }
                
                // Also stop on RMK
                if (trimmedLine.StartsWith("RMK", StringComparison.OrdinalIgnoreCase))
                {
                    isFlexRoute = false;
                }
            }
            
            // Don't forget the last track
            if (currentTrackId != null)
            {
                // For TDM tracks, parse all collected waypoint text at once
                if (currentWaypointText.Count > 0)
                {
                    var combinedText = string.Join(" ", currentWaypointText);
                    ParseWaypointsFromLine(combinedText, currentFixes);
                    currentWaypointText.Clear();
                }
                if (currentFixes.Count > 1)
                {
                    tracks.Add(new Track(currentTrackId, start, end, new List<Fix>(currentFixes)));
                }
            }
            
            return tracks;
        }
        
        /// <summary>
        /// Parses waypoints from a line of text and adds them to the fixes list
        /// </summary>
        private static void ParseWaypointsFromLine(string line, List<Fix> fixes)
        {
            var words = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(w => w.Trim().ToUpper())
                            .Where(w => !string.IsNullOrWhiteSpace(w))
                            .ToArray();
            
            for (int i = 0; i < words.Length; i++)
            {
                var cleanWord = words[i];
                
                // Skip common non-waypoint words
                if (IsSkipWord(cleanWord))
                    continue;
                
                // Check for coordinate format: 43N160E, 50N170W, etc.
                var coordMatch = Regex.Match(cleanWord, @"^(\d{2})([NS])(\d{2,3})([EW])$");
                if (coordMatch.Success)
                {
                    double latitude = double.Parse(coordMatch.Groups[1].Value);
                    double longitude = double.Parse(coordMatch.Groups[3].Value);

                    if (coordMatch.Groups[2].Value == "S")
                        latitude = -latitude;
                    if (coordMatch.Groups[4].Value == "W")
                        longitude = -longitude;

                    fixes.Add(new Fix(cleanWord, latitude, longitude));
                    continue;
                }
                
                // Check for airway format: A123, B590, R220, etc.
                var airwayMatch = Regex.Match(cleanWord, @"^[A-Z]\d{1,3}$");
                if (airwayMatch.Success && fixes.Count > 0)
                {
                    try
                    {
                        var airway = Airspace2.GetAirway(cleanWord);
                        if (airway != null && airway.Intersections != null && airway.Intersections.Count > 0)
                        {
                            // Get the entry fix (last fix in our list)
                            var entryFix = fixes[fixes.Count - 1];
                            
                            // Look ahead to find the exit fix
                            string exitFixName = null;
                            for (int j = i + 1; j < words.Length; j++)
                            {
                                var nextWord = words[j];
                                if (IsSkipWord(nextWord))
                                    continue;
                                // Check if it's a waypoint (5 letters) or coordinate
                                if (Regex.IsMatch(nextWord, @"^[A-Z]{5}$") || 
                                    Regex.IsMatch(nextWord, @"^(\d{2})([NS])(\d{2,3})([EW])$"))
                                {
                                    exitFixName = nextWord;
                                    break;
                                }
                                // If we hit another airway, stop looking
                                if (Regex.IsMatch(nextWord, @"^[A-Z]\d{1,3}$"))
                                    break;
                            }
                            
                            // Find entry and exit indices on the airway
                            int entryIndex = -1;
                            int exitIndex = -1;
                            
                            for (int k = 0; k < airway.Intersections.Count; k++)
                            {
                                var intersection = airway.Intersections[k];
                                if (intersection.Name.Equals(entryFix.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    entryIndex = k;
                                }
                                if (exitFixName != null && intersection.Name.Equals(exitFixName, StringComparison.OrdinalIgnoreCase))
                                {
                                    exitIndex = k;
                                }
                            }
                            
                            // If we found both entry and exit on the airway, add all fixes between them
                            if (entryIndex != -1 && exitIndex != -1 && entryIndex != exitIndex)
                            {
                                int start, end, step;
                                if (entryIndex < exitIndex)
                                {
                                    start = entryIndex + 1; // Skip entry, it's already in our list
                                    end = exitIndex;       // Include exit
                                    step = 1;
                                }
                                else
                                {
                                    start = entryIndex - 1;
                                    end = exitIndex;
                                    step = -1;
                                }
                                
                                for (int k = start; step > 0 ? k <= end : k >= end; k += step)
                                {
                                    var intersection = airway.Intersections[k];
                                    fixes.Add(new Fix(intersection.Name, intersection.LatLong.Latitude, intersection.LatLong.Longitude));
                                }
                                
                                // Skip the exit fix in main loop since we already added it
                                // Find and skip to after the exit fix
                                for (int j = i + 1; j < words.Length; j++)
                                {
                                    if (words[j].Equals(exitFixName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        i = j; // Main loop will increment past this
                                        break;
                                    }
                                }
                            }
                            else if (entryIndex != -1)
                            {
                                // Only found entry - just add fixes from entry in the direction of travel
                                // This is a fallback if exit isn't on the airway
                            }
                        }
                    }
                    catch
                    {
                        // Airway not found, skip
                    }
                    continue;
                }
                
                // Check for 5-letter waypoint identifier
                if (Regex.IsMatch(cleanWord, @"^[A-Z]{5}$"))
                {
                    // Get the previous fix's coordinate to help resolve duplicates
                    Coordinate latlongQualify = null;
                    if (fixes.Count > 0)
                    {
                        var lastFix = fixes[fixes.Count - 1];
                        latlongQualify = new Coordinate(lastFix.Latitude, lastFix.Longitude);
                    }
                    else
                    {
                        // No previous fix - look ahead for the next coordinate to use as reference
                        for (int j = i + 1; j < words.Length; j++)
                        {
                            var nextWord = words[j];
                            if (IsSkipWord(nextWord))
                                continue;
                            
                            // Check if it's a coordinate format
                            var nextCoordMatch = Regex.Match(nextWord, @"^(\d{2})([NS])(\d{2,3})([EW])$");
                            if (nextCoordMatch.Success)
                            {
                                double lat = double.Parse(nextCoordMatch.Groups[1].Value);
                                double lon = double.Parse(nextCoordMatch.Groups[3].Value);
                                if (nextCoordMatch.Groups[2].Value == "S") lat = -lat;
                                if (nextCoordMatch.Groups[4].Value == "W") lon = -lon;
                                latlongQualify = new Coordinate(lat, lon);
                                break;
                            }
                        }
                    }
                    
                    // Use our helper method that searches both dictionaries for the closest fix
                    var fix = GetClosestIntersection(cleanWord, latlongQualify);
                    
                    if (fix != null)
                    {
                        fixes.Add(new Fix(cleanWord, fix.LatLong.Latitude, fix.LatLong.Longitude));
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets the closest intersection by searching both local and navigraph intersection dictionaries.
        /// This works around a limitation in Airspace2.GetIntersection which only checks one dictionary.
        /// </summary>
        private static Airspace2.Intersection GetClosestIntersection(string name, Coordinate latlongQualify)
        {
            var candidates = new List<Airspace2.Intersection>();
            
            try
            {
                // Try to get from local Intersections via reflection
                var intersectionsField = typeof(Airspace2).GetField("Intersections",
                    BindingFlags.NonPublic | BindingFlags.Static);
                
                if (intersectionsField != null)
                {
                    var intersections = intersectionsField.GetValue(null) as Dictionary<string, List<Airspace2.Intersection>>;
                    if (intersections != null && intersections.TryGetValue(name, out var localList))
                    {
                        candidates.AddRange(localList);
                    }
                }
                
                // Try to get from navigraphIntersections via reflection
                var navigraphField = typeof(Airspace2).GetField("navigraphIntersections",
                    BindingFlags.NonPublic | BindingFlags.Static);
                
                if (navigraphField != null)
                {
                    var navigraphIntersections = navigraphField.GetValue(null) as Dictionary<string, List<Airspace2.Intersection>>;
                    if (navigraphIntersections != null && navigraphIntersections.TryGetValue(name, out var navList))
                    {
                        // Add navigraph fixes that aren't already in candidates (avoid duplicates)
                        foreach (var navFix in navList)
                        {
                            if (!candidates.Any(c => c.LatLong.Equals(navFix.LatLong)))
                            {
                                candidates.Add(navFix);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Reflection failed, fall back to standard lookup
            }
            
            // If we found candidates, pick the closest one
            if (candidates.Count > 0)
            {
                if (latlongQualify != null && candidates.Count > 1)
                {
                    return candidates.OrderBy(c => Conversions.CalculateDistance(c.LatLong, latlongQualify)).First();
                }
                
                // No qualifying coordinate but multiple candidates - for PACOTS, prefer Pacific region fixes
                // Pacific region is roughly: latitude 0-60N, longitude 120E-180 or 180-120W (i.e., > 120 or < -120)
                if (candidates.Count > 1)
                {
                    var pacificCandidates = candidates.Where(c => 
                        c.LatLong.Latitude >= 0 && c.LatLong.Latitude <= 60 &&
                        (c.LatLong.Longitude > 120 || c.LatLong.Longitude < -120)).ToList();
                    
                    if (pacificCandidates.Count == 1)
                    {
                        return pacificCandidates.First();
                    }
                    else if (pacificCandidates.Count > 1)
                    {
                        // Multiple in Pacific - return the one closest to typical PACOTS track area (around 30N 150E)
                        var pacotCenter = new Coordinate(30, 150);
                        return pacificCandidates.OrderBy(c => Conversions.CalculateDistance(c.LatLong, pacotCenter)).First();
                    }
                }
                
                return candidates.First();
            }
            
            // Fallback to standard GetIntersection
            return Airspace2.GetIntersection(name, latlongQualify);
        }
        
        /// <summary>
        /// Check if a word should be skipped when parsing waypoints
        /// </summary>
        private static bool IsSkipWord(string word)
        {
            var skipWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ROUTE", "RTS", "TDM", "TRK", "TRACK", "PACOTS", "PACOT",
                "FLEX", "FROM", "TO", "VIA", "AND", "THE", "FOR", "NAR", "NOPAC",
                "RTE", "EAST", "WEST", "NORTH", "SOUTH", "EASTBOUND", "WESTBOUND",
                "FL", "FLT", "LVL", "LEVEL", "BTN", "BETWEEN", "VALID", "EFF",
                "EFFECTIVE", "UNTIL", "TMI", "ADVISORY", "NOTAM", "JAPAN", "HAWAII",
                "ASIA", "AMERICA", "RCTP", "VHHH", "ACFT", "LDG", "DEST", "OTHER",
                "ATM", "CENTER", "TEL", "NOT", "AVAILABLE", "RMK", "OTR5", "OTR7",
                "OTR11", "OTR15", "Y74", "V75", "ADNAP", "AVBET", "POVAL", "BORDO",
                "TOPAT"
            };
            
            return skipWords.Contains(word) || word.Length < 3;
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
        
        /// <summary>
        /// Gets NOTAMs for a specific ICAO location
        /// </summary>
        public static async Task<List<Notam>> GetNotamsForLocationAsync(string icaoId)
        {
            if (_notamService == null)
            {
                throw new InvalidOperationException("NOTAM service not configured");
            }
            
            var response = await _notamService.GetNotamsByLocationAsync(icaoId);
            return Notam.FromResponse(response);
        }
        
        /// <summary>
        /// Gets NOTAMs filtered by classification
        /// </summary>
        public static async Task<List<Notam>> GetNotamsByClassificationAsync(string classification)
        {
            if (_notamService == null)
            {
                throw new InvalidOperationException("NOTAM service not configured");
            }
            
            var response = await _notamService.GetNotamsByClassificationAsync(classification);
            return Notam.FromResponse(response);
        }
        
        /// <summary>
        /// Gets NOTAMs by geospatial search
        /// </summary>
        public static async Task<List<Notam>> GetNotamsByRadiusAsync(double latitude, double longitude, double radiusNm)
        {
            if (_notamService == null)
            {
                throw new InvalidOperationException("NOTAM service not configured");
            }
            
            var response = await _notamService.GetNotamsByLocationRadiusAsync(latitude, longitude, radiusNm);
            return Notam.FromResponse(response);
        }
        
        /// <summary>
        /// Tests connectivity to the NOTAM API by attempting to get a token
        /// </summary>
        public static async Task<bool> TestNotamApiConnectionAsync()
        {
            if (_notamService == null)
            {
                return false;
            }
            
            try
            {
                await _notamService.GetBearerTokenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<List<Sigmets>> GetSigmets()
        {
            var getSigmet = await _httpClient.GetAsync(SigmetUrl);

            var poly = new List<Sigmets>();

            if (!getSigmet.IsSuccessStatusCode)
            {
                return poly;
            }

            var content = await getSigmet.Content.ReadAsStringAsync();

            var sigmets = JsonConvert.DeserializeObject<List<Sigmet>>(content);

            for (int s = 0; s < sigmets.Count; s++)
            {
                if (sigmets[s].FirId != "KZAK") continue;

                var points = new List<Fix>();

                for (int coordList = 0; coordList < sigmets[s].Coords.Count; coordList++)
                {
                    for (int c = 0; c < sigmets[s].Coords[coordList].Count; c++)
                    {
                        double latitude = sigmets[s].Coords[coordList][c].Lat;
                        double longitude = sigmets[s].Coords[coordList][c].Lon;

                        points.Add(new Fix(sigmets[s].Coords[coordList][c].Lat + sigmets[s].Coords[coordList][c].Lon.ToString(), latitude, longitude));
                    }
                }

                var from = DateTimeOffset.FromUnixTimeSeconds(long.Parse(sigmets[s].ValidTimeFrom.ToString())).DateTime;
                var to = DateTimeOffset.FromUnixTimeSeconds(long.Parse(sigmets[s].ValidTimeTo.ToString())).DateTime;

                poly.Add(new Sigmets(sigmets[s].SeriesId, from, to, points));
            }

            return poly;
        }

        public static DateTime ToDateTime(string input)
        {
            // Parse YYMMDDHHMM format - add 2000 to get full year
            int year = 2000 + int.Parse(input.Substring(0, 2));
            int month = int.Parse(input.Substring(2, 2));
            int day = int.Parse(input.Substring(4, 2));
            int hour = int.Parse(input.Substring(6, 2));
            int minute = int.Parse(input.Substring(8, 2));
            return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
        }

        private static void InitializeTDMMenu()
        {
            var tdmMenuItem = new CustomToolStripMenuItem(CustomToolStripMenuItemWindowType.Main,
                CustomToolStripMenuItemCategory.Custom, new ToolStripMenuItem("TDM"))
            {
                CustomCategoryName = CategoryName
            };
            tdmMenuItem.Item.Click += (sender, e) => MMI.InvokeOnGUI(() => TDMWindow.Show());
            MMI.AddCustomMenuItem(tdmMenuItem);
        }

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
