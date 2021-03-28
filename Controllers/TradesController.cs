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

            foreach(var trade in tradesAscending)
            {
                if(trade.Alma == 0 || trade.SMA == 0 || trade.SMATwo == 0)
                {
                    continue;
                }
                var startcut = tradesDescending.Last().TimeUtcMilliseconds + TimeSpan.FromMinutes(40).TotalMilliseconds;
                var tradesSoFar = tradesDescending.Where(t => t.TimeUtcMilliseconds < trade.TimeUtcMilliseconds && t.TimeUtcMilliseconds > startcut);
                var stdev = (trade.SMA - trade.SMALower) / 2;
                if(tradesSoFar.Count() > 2)
                {
                    var posPerc = (double)tradesSoFar.Where(t=> t.Alma > t.SMA).Count() / tradesSoFar.Count();
                    //var trade1 = trade;
                    //var trade2 = tradesSoFar.First();
                    //var trade3 = tradesSoFar.Skip(1).First();
                    //var accel1 = (trade1.AlmaSlope - trade2.AlmaSlope) / trade1.TimeUtc.Subtract(trade2.TimeUtc).TotalMinutes;
                    //var accel2 = (trade2.AlmaSlope - trade3.AlmaSlope) / trade2.TimeUtc.Subtract(trade3.TimeUtc).TotalMinutes;
                    //var slopeChange = (trade1.TimeUtc.Subtract(trade3.TimeUtc).TotalMinutes) * ((accel1 / 4) + (accel2 / 4));
                    var nextSlope = (trade.AlmaSlope*2) - tradesSoFar.First().AlmaSlope;
                    if (trade.Alma < (trade.SMA-stdev) && trade.SMASlope < 0 && trade.AlmaSlope < 0 && nextSlope >= 0 && stdev >= (trade.SMA * .002) && posPerc >= .45 && posPerc <= .75
                        && (!orders.Any() || (orders.Last().Sell.HasValue && orders.Last().Sell > orders.Last().Buy))
                        && trade.TimeUtcMilliseconds < (recent.TimeUtcMilliseconds-TimeSpan.FromMinutes(30).TotalMilliseconds)
                        )
                    {
                        var lastPositive = tradesSoFar.FirstOrDefault(t => t.Alma >= t.SMA);
                        if (lastPositive != null && lastPositive.SMA > lastPositive.SMATwo && lastPositive.SMALower > trade.Alma && trade.SMAUpper > lastPositive.SMA)
                        {
                            var rounded = Math.Round(trade.Alma, 2);
                            var buy = rounded;
                            if(buy > lastPositive.SMATwoLower)
                            {
                                orders.Add(new Order
                                {
                                    Buy = buy,
                                    Goal = buy + (trade.SMA - buy) + stdev,
                                    //Stop = Math.Min(buy - (stdev * 3), buy * .997),
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
                            if (((order.OrderTime < (trade.TimeUtcMilliseconds + TimeSpan.FromMinutes(10).TotalMilliseconds) && trade.Alma > order.Goal) || trade.Alma > trade.SMA) && trade.SMASlope > 0 && order.Buy < trade.SMA)
                            {
                                if (trade.AlmaSlope > 0 && nextSlope <= 0)
                                {
                                    var rounded = Math.Round(trade.Alma, 2);
                                    var sell = rounded;
                                    order.Sell = sell;
                                }
                            }
                            else if (order.BuyTime.HasValue && trade.TimeUtcMilliseconds > (order.BuyTime + TimeSpan.FromMinutes(60).TotalMilliseconds))
                            {
                                order.Sell = order.Buy;
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
                    else if (order.Stop.HasValue && order.BuyTime.HasValue && !order.SellTime.HasValue && order.Stop >= trade.Price)
                    {
                        order.SellTime = trade.TimeUtcMilliseconds;
                        order.Sell = order.Stop;
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
