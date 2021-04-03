using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TradeCharts.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TradesController : ControllerBase
    {
        private readonly ILogger<TradesController> _logger;
        private const double _balance = 4000;
        private readonly double _maxOrderAmount = _balance*.015;

        public TradesController(ILogger<TradesController> logger)
        {
            _logger = logger;
        }
        [HttpGet("{symbol}/{dayOffset}/{skip}/{cut}")]
        public async Task<Trades> Get(string symbol, int dayOffset,int skip, int cut)
        {
            using var stocksContext = new StocksContext();

            var today = DateTime.UtcNow;
            var day = new DateTimeOffset(new DateTime(today.Year, today.Month, today.Day - dayOffset,23, 0, 0)).ToUnixTimeMilliseconds();
            var recent = await stocksContext.Trades.Where(t => t.Symbol == symbol && t.TimeUtcMilliseconds < day).OrderByDescending(t => t.TimeUtcMilliseconds).FirstOrDefaultAsync();
            var start = cut > 0 ? recent.TimeUtcMilliseconds - TimeSpan.FromHours(cut).TotalMilliseconds : recent.TimeUtcMilliseconds;
            var stop = recent.TimeUtcMilliseconds - TimeSpan.FromHours(8-skip).TotalMilliseconds;

            var trades = stocksContext.Trades.Where(t => t.Symbol == symbol && t.TimeUtcMilliseconds < start && t.TimeUtcMilliseconds > stop);
            var tradesAscending = await trades.OrderBy(t => t.TimeUtcMilliseconds).ToListAsync();
            var tradesDescending = await trades.OrderByDescending(t => t.TimeUtcMilliseconds).ToListAsync();
            var orders = new List<Order>();
            var tradesObj = new Trades();
            //var curMin = double.MaxValue;
            foreach(var trade in tradesAscending)
            {
                if(trade.Alma == 0 || trade.SMA == 0 || trade.SMATwo == 0)
                {
                    continue;
                }
                //curMin = trade.Alma < curMin ? trade.Alma : curMin;
                var startcut = tradesDescending.Last().TimeUtcMilliseconds + TimeSpan.FromMinutes(40).TotalMilliseconds;
                var tradesSoFar = tradesDescending.Where(t => t.TimeUtcMilliseconds < trade.TimeUtcMilliseconds && t.TimeUtcMilliseconds > startcut);
                var stdev = (trade.SMA - trade.SMALower) / 2;
                if(tradesSoFar.Any() && tradesSoFar.Count() > 6)
                {
                    var previousAlmaMA = tradesSoFar.Skip(6).First();
                    var posPerc = (double)tradesSoFar.Where(t=> t.Alma > t.SMA).Count() / tradesSoFar.Count();
                    if (trade.Alma < trade.SMA && trade.SMASlope < 0 && previousAlmaMA.AlmaSlopeMA < 0 && trade.AlmaSlopeMA > (previousAlmaMA.AlmaSlopeMA * .4) && stdev >= (trade.SMA * .0018) && posPerc >= .42 && posPerc <= .72
                        && (!orders.Any() || (orders.Last().Sell.HasValue && orders.Last().Sell > orders.Last().Buy))
                        //&& (orders.Any() || trade.Alma < curMin * 1.0016)
                        && trade.TimeUtcMilliseconds < (tradesDescending.First().TimeUtcMilliseconds - TimeSpan.FromMinutes(75).TotalMilliseconds)
                        //&& trade.TimeUtcMilliseconds < (recent.TimeUtcMilliseconds-TimeSpan.FromMinutes(30).TotalMilliseconds)
                        )
                    {
                        var lastPositive = tradesSoFar.FirstOrDefault(t => t.Alma >= t.SMA);
                        if (lastPositive != null && lastPositive.SMA > lastPositive.SMATwo && lastPositive.SMALower > trade.Alma && trade.SMAUpper > lastPositive.SMA)
                        {
                            var rounded = Math.Round(trade.Alma, 2);
                            var buy = rounded;
                            if(buy > lastPositive.SMATwoLower)
                            {
                                var qty = (int)Math.Floor(_maxOrderAmount / buy);
                                for (var i = 0; i < qty; i++)
                                orders.Add(new Order
                                    {
                                        Buy = buy,
                                        Goal = buy + (trade.SMA - buy) + stdev,
                                        Stop = buy * .993,
                                        //InitStop = Math.Min(buy - (stdev * 3), buy * .997),
                                        OrderTime = trade.TimeUtcMilliseconds
                                    });
                            }
                        }
                    }
                    foreach(var order in orders)
                    {
                        if (order.BuyTime.HasValue && !order.Sell.HasValue)
                        {
                            if((trade.TimeUtcMilliseconds - order.BuyTime) < TimeSpan.FromMinutes(60).TotalMilliseconds)
                            {
                                var minSell = order.Buy;
                                if((trade.TimeUtcMilliseconds - order.BuyTime) < TimeSpan.FromMinutes(40).TotalMilliseconds)
                                {
                                    var minutesElapsed = TimeSpan.FromMilliseconds((double)trade.TimeUtcMilliseconds - order.BuyTime.Value).TotalMinutes;
                                    minSell = ((order.Buy - order.Goal) * minutesElapsed / 80) + order.Goal;
                                }
                                if (trade.Alma >= minSell && previousAlmaMA.AlmaSlopeMA > 0 && trade.AlmaSlopeMA < (previousAlmaMA.AlmaSlopeMA * .6))
                                {
                                    var sell = Math.Round(trade.Alma, 2);
                                    order.Sell = sell;
                                }
                            }
                            else if ((trade.TimeUtcMilliseconds - order.BuyTime) > TimeSpan.FromMinutes(60).TotalMilliseconds)
                            {
                                if(order.Stop.HasValue && order.Stop >= trade.Price)
                                {
                                    order.Sell = order.Stop;
                                }
                                else
                                {
                                    order.Sell = order.Buy;
                                }
                            }
                        }
                    }
                }
                foreach(var order in orders)
                {
                    /********************/
                    /********************/
                    if (order.BuyTime.HasValue && !order.SellTime.HasValue && order.Sell.HasValue && order.Sell <= trade.Price)
                    {
                        order.SellTime = trade.TimeUtcMilliseconds;
                    }
                    //else if (order.Stop.HasValue && order.BuyTime.HasValue && !order.SellTime.HasValue && decimal.ToDouble(trade.Price) > order.Buy)
                    //{
                    //    order.Stop = order.InitStop + (decimal.ToDouble(trade.Price) - order.Buy);
                    //}
                    if (!order.BuyTime.HasValue && order.Buy >= trade.Price)
                    {
                        order.BuyTime = trade.TimeUtcMilliseconds;
                    }
                }

                orders = orders.Where(o => o.BuyTime.HasValue || trade.TimeUtcMilliseconds < (o.OrderTime + TimeSpan.FromHours(1).TotalMilliseconds)).ToList();
                tradesObj.SMA.Add(new Point(trade.TimeUtcMilliseconds, trade.SMA));
                tradesObj.SMATwo.Add(new Point(trade.TimeUtcMilliseconds, trade.SMATwo));
                tradesObj.Price.Add(new Point(trade.TimeUtcMilliseconds, trade.Price));
                tradesObj.SMAUpper.Add(new Point(trade.TimeUtcMilliseconds, trade.SMAUpper));
                tradesObj.SMALower.Add(new Point(trade.TimeUtcMilliseconds, trade.SMALower));
                tradesObj.SMATwoUpper.Add(new Point(trade.TimeUtcMilliseconds, trade.SMATwoUpper));
                tradesObj.SMATwoLower.Add(new Point(trade.TimeUtcMilliseconds, trade.SMATwoLower));
                tradesObj.Alma.Add(new Point(trade.TimeUtcMilliseconds, trade.Alma));
            }
            //foreach(var order in orders)
            //{
            //    if(order.BuyTime.HasValue && !order.SellTime.HasValue)
            //    {
            //        order.SellTime = tradesAscending.Last().TimeUtcMilliseconds;
            //        order.Sell = tradesAscending.Last().Price;
            //    }
            //}
            tradesObj.Orders = orders.Select(o => new Point(o.OrderTime, o.Buy)).ToList();
            tradesObj.Buys = orders.Where(o => o.BuyTime.HasValue).Select(o => new Point(o.BuyTime.Value, o.Buy)).ToList();
            tradesObj.Sells = orders.Where(o => o.SellTime.HasValue).Select(o => new Point(o.SellTime.Value, o.Sell.Value)).ToList();
            tradesObj.Total = orders.Aggregate((double)0, (acc, o) => o.SellTime.HasValue ? acc + (o.Sell.Value - o.Buy) : acc);
            tradesObj.TotalSpent = orders.Aggregate((double)0, (acc, o) => o.BuyTime.HasValue ? acc + o.Buy : acc);
            tradesObj.Hits = orders.Where(o => o.SellTime.HasValue && o.Sell > o.Buy).Count();
            tradesObj.Misses = orders.Where(o => o.BuyTime.HasValue && !o.SellTime.HasValue).Count();
            return tradesObj;
        }
    }
    public class Order
    {
        public double Buy { get; set; }
        public long? BuyTime { get; set; }
        public double? Sell { get; set; }
        public double Goal { get; set; }
        public double? Stop { get; set; }
        public double InitStop { get; set; }
        public long? SellTime { get; set; }
        public long OrderTime { get; set; }
    }
    public class Trades
    {
        public Trades()
        {
            SMA = new List<Point>();
            SMATwo = new List<Point>();
            SMAThree = new List<Point>();
            Price = new List<Point>();
            SMAUpper = new List<Point>();
            SMALower = new List<Point>();
            Alma = new List<Point>();
            SMATwoUpper = new List<Point>();
            SMATwoLower = new List<Point>();
        }
        public List<Point> SMA { get; set; }
        public List<Point> SMATwo { get; set; }
        public List<Point> SMAThree { get; set; }
        public List<Point> Price { get; set; }
        public List<Point> SMAUpper { get; set; }
        public List<Point> SMALower { get; set; }
        public List<Point> SMATwoUpper { get; set; }
        public List<Point> SMATwoLower { get; set; }
        public List<Point> Alma { get; set; }
        public List<Point> Orders { get; set; }
        public List<Point> Buys { get; set; }
        public List<Point> Sells { get; set; }
        public double Total { get; set; }
        public double TotalSpent { get; set; }
        public int Hits { get; set; }
        public int Misses { get; set; }
    }
    public class Point
    {
        public Point(long x, double y)
        {
            X = x;
            Y = y;
        }
        public long X { get; set; }
        public double Y { get; set; }
    }
}
