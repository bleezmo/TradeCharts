using System;

namespace TradeCharts
{
    public class Trade
    {
        public long Id { get; set; }
        public string Symbol { get; set; }
        public decimal Price { get; set; }
        public long Size { get; set; }
        public DateTime TimeUtc { get; set; }
        public long TimeUtcMilliseconds { get; set; }
        public double SMA { get; set; }
        public double SMAUpper { get; set; }
        public double SMALower { get; set; }
        public double Alma { get; set; }
        public double Slope { get; set; }
        public double SMASlope { get; set; }
        public double AlmaSlope { get; set; }
        public double VolumeSMA { get; set; }
    }
}
