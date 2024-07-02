using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NATPlugin
{
    public class Coord
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }
    }

    public class Sigmet
    {
        [JsonPropertyName("isigmetId")]
        public int IsigmetId { get; set; }

        [JsonPropertyName("icaoId")]
        public string IcaoId { get; set; }

        [JsonPropertyName("firId")]
        public string FirId { get; set; }

        [JsonPropertyName("firName")]
        public string FirName { get; set; }

        [JsonPropertyName("receiptTime")]
        public string ReceiptTime { get; set; }

        [JsonPropertyName("validTimeFrom")]
        public int ValidTimeFrom { get; set; }

        [JsonPropertyName("validTimeTo")]
        public int ValidTimeTo { get; set; }

        [JsonPropertyName("seriesId")]
        public string SeriesId { get; set; }

        [JsonPropertyName("hazard")]
        public string Hazard { get; set; }

        [JsonPropertyName("qualifier")]
        public string Qualifier { get; set; }

        [JsonPropertyName("base")]
        public int? Base { get; set; }

        [JsonPropertyName("top")]
        public int? Top { get; set; }

        [JsonPropertyName("coords")]
        public List<Coord> Coords { get; set; }

        [JsonPropertyName("rawSigmet")]
        public string RawSigmet { get; set; }
    }

}
