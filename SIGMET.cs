using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NATPlugin
{
    public class SIGMET
    {
        [JsonProperty(PropertyName = "firId")] public string FIR { get; set; }
        [JsonProperty(PropertyName = "seriesId")] public string Name { get; set; }
        [JsonProperty(PropertyName = "estimating_time")] public string EstimatingTime { get; set; }
        [JsonProperty(PropertyName = "validTimeFrom")] public DateTime From { get; set; }
        [JsonProperty(PropertyName = "validTimeTo")] public DateTime To { get; set; }
        [JsonProperty(PropertyName = "coordinates")] public double[] Coordinates { get; set; }
    }
}
