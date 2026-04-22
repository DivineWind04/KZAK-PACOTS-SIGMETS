using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace PACOTSPlugin
{
    public class CoordConverter : JsonConverter<List<List<Coord>>>
    {
        public override List<List<Coord>> ReadJson(JsonReader reader, Type objectType, List<List<Coord>> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var coords = new List<List<Coord>>();

            try
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return coords;
                }

                var token = JToken.Load(reader);
                if (token.Type == JTokenType.Array)
                {
                    foreach (var element in token)
                    {
                        if (element.Type == JTokenType.Array)
                        {
                            // Nested array of coords
                            var innerList = new List<Coord>();
                            foreach (var coordElement in element)
                            {
                                if (coordElement.Type == JTokenType.Object)
                                {
                                    var coord = ParseCoord(coordElement);
                                    if (coord != null)
                                    {
                                        innerList.Add(coord);
                                    }
                                }
                            }
                            if (innerList.Count > 0)
                            {
                                coords.Add(innerList);
                            }
                        }
                        else if (element.Type == JTokenType.Object)
                        {
                            // Single coord object
                            var coord = ParseCoord(element);
                            if (coord != null)
                            {
                                coords.Add(new List<Coord> { coord });
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Return empty coords list if parsing fails
            }

            return coords;
        }

        private Coord ParseCoord(JToken element)
        {
            try
            {
                double lat = element["lat"]?.Value<double>() ?? 0;
                double lon = element["lon"]?.Value<double>() ?? 0;
                return new Coord { Lat = lat, Lon = lon };
            }
            catch
            {
                return null;
            }
        }

        public override void WriteJson(JsonWriter writer, List<List<Coord>> value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            foreach (var innerList in value)
            {
                writer.WriteStartArray();
                foreach (var coord in innerList)
                {
                    serializer.Serialize(writer, coord);
                }
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }
    }
    public class Sigmet
    {
        [JsonProperty("isigmetId")]
        public int IsigmetId { get; set; }

        [JsonProperty("icaoId")]
        public string IcaoId { get; set; }

        [JsonProperty("firId")]
        public string FirId { get; set; }

        [JsonProperty("firName")]
        public string FirName { get; set; }

        [JsonProperty("receiptTime")]
        public string ReceiptTime { get; set; }

        [JsonProperty("validTimeFrom")]
        public int ValidTimeFrom { get; set; }

        [JsonProperty("validTimeTo")]
        public int ValidTimeTo { get; set; }

        [JsonProperty("seriesId")]
        public string SeriesId { get; set; }

        [JsonProperty("hazard")]
        public string Hazard { get; set; }

        [JsonProperty("qualifier")]
        public string Qualifier { get; set; }

        [JsonProperty("base")]
        public int? Base { get; set; }

        [JsonProperty("top")]
        public int? Top { get; set; }

        [JsonProperty("coords")]
        [JsonConverter(typeof(CoordConverter))]
        public List<List<Coord>> Coords { get; set; }

        [JsonProperty("rawSigmet")]
        public string RawSigmet { get; set; }
    }
    public class Coord
    {
        [JsonProperty("lat")]
        public double Lat { get; set; }

        [JsonProperty("lon")]
        public double Lon { get; set; }
    }

}
