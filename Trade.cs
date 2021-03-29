using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace TradeCharts
{
    [Index(nameof(Symbol))]
    [Index(nameof(TimeUtcMilliseconds))]
    public class Trade
    {
        public long Id { get; set; }
        public string Symbol { get; set; }
        public double Price { get; set; }
        public long Size { get; set; }
        public DateTime TimeUtc { get; set; }
        public long TimeUtcMilliseconds { get; set; }
        public double SMA { get; set; }
        public double SMASlope { get; set; }
        //window is twice SMA window
        public double SMATwo { get; set; }
        public double SMATwoUpper { get; set; }
        public double SMATwoLower { get; set; }
        public double SMATwoSlope { get; set; }
        public double SMAUpper { get; set; }
        public double SMALower { get; set; }
        public double Alma { get; set; }
        public double AlmaSlope { get; set; }
        public double NextSlope { get; set; }
        public double VolumeSMA { get; set; }
    }
}
