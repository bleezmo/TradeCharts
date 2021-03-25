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
        [HttpGet("{symbol}/{dayOffset}")]
        public async Task<Trades> Get(string symbol, int dayOffset)
        {
            using var stocksContext = new StocksContext();

            var today = DateTime.UtcNow;
            var day = new DateTimeOffset(new DateTime(today.Year, today.Month, today.Day - dayOffset,23, 0, 0)).ToUnixTimeMilliseconds();
            var recent = await stocksContext.Trades.Where(t => t.Symbol == symbol && t.TimeUtcMilliseconds < day).OrderByDescending(t => t.TimeUtcMilliseconds).FirstOrDefaultAsync();
            var start = recent.TimeUtcMilliseconds;
            var stop = recent.TimeUtcMilliseconds - TimeSpan.FromHours(8).TotalMilliseconds;

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
                var startcut = tradesDescending.Last().TimeUtcMilliseconds + TimeSpan.FromMinutes(30).TotalMilliseconds;
                var tradesSoFar = tradesDescending.Where(t => t.TimeUtcMilliseconds < trade.TimeUtcMilliseconds && t.TimeUtcMilliseconds > startcut);
                var stdev = (trade.SMA - trade.SMALower) / 2;
                if(tradesSoFar.Any())
                {
                    var last = tradesSoFar.Last();
                    if (trade.AlmaSlope > 0 && trade.Alma < trade.SMA && trade.Alma >= (trade.SMA - stdev)
                        && (!orders.Any() || (orders.Last().Sell.HasValue && orders.Last().Sell > orders.Last().Buy && trade.TimeUtcMilliseconds > (orders.Last().OrderTime + TimeSpan.FromMinutes(30).TotalMilliseconds)))
                        && trade.TimeUtcMilliseconds < (recent.TimeUtcMilliseconds-TimeSpan.FromMinutes(30).TotalMilliseconds))
                    {
                        var lastPositive = tradesSoFar.FirstOrDefault(t => t.Alma >= t.SMA);
                        if (lastPositive != null && lastPositive.SMA > trade.SMATwo && lastPositive.SMALower > trade.Alma /*&& lastPositive.Alma - trade.Alma >= trade.SMA*.002*/)
                        {
                            var buy = trade.Alma + ((trade.SMA - trade.Alma)*.25);
                            if(!orders.Any() || (orders.Last().Sell.HasValue && (orders.Last().Buy + (orders.Last().Sell-orders.Last().Buy)*2) > buy))
                            {
                                var buys = Math.Floor(4000 / buy);
                                for (var i = 0; i < buys; i++)
                                {
                                    orders.Add(new Order
                                    {
                                        Buy = buy,
                                        Stop = Math.Min(buy - (stdev * 3), buy * .997),
                                        OrderTime = trade.TimeUtcMilliseconds
                                    });
                                }
                            }
                        }
                    }
                }
                foreach(var order in orders)
                {
                    /********************/
                    if (order.BuyTime.HasValue && !order.Sell.HasValue)
                    {
                        if(trade.Alma > trade.SMA && trade.AlmaSlope < 0 && trade.SMASlope > 0 && trade.Alma < (trade.SMA+stdev) && order.Buy < trade.SMA)
                        {
                            order.Sell = trade.Alma - ((trade.Alma - trade.SMA) * .25);
                        }
                        else if(trade.TimeUtcMilliseconds > (order.OrderTime + TimeSpan.FromMinutes(60).TotalMilliseconds))
                        {
                            order.Sell = order.Buy;
                        }
                        //else if (decimal.ToDouble(trade.Price) > order.Buy)
                        //{
                        //    order.Stop = order.Stop + (decimal.ToDouble(trade.Price) - order.Buy);
                        //}
                    }
                    /********************/
                    if (order.BuyTime.HasValue && !order.SellTime.HasValue && order.Sell.HasValue && order.Sell <= decimal.ToDouble(trade.Price))
                    {
                        order.SellTime = trade.TimeUtcMilliseconds;
                        
                        //else if (decimal.ToDouble(trade.Price) > order.Buy)
                        //{
                        //    order.Stop = order.Stop + (decimal.ToDouble(trade.Price) - order.Buy);
                        //}
                    }
                    else if (order.BuyTime.HasValue && !order.SellTime.HasValue && order.Stop >= decimal.ToDouble(trade.Price))
                    {
                        order.SellTime = trade.TimeUtcMilliseconds;
                        order.Sell = order.Stop;
                    }
                    if (!order.BuyTime.HasValue && order.Buy >= decimal.ToDouble(trade.Price))
                    {
                        order.BuyTime = trade.TimeUtcMilliseconds;
                    }
                }

                orders = orders.Where(o => o.BuyTime.HasValue || trade.TimeUtcMilliseconds < (o.OrderTime + TimeSpan.FromHours(1).TotalMilliseconds)).ToList();
                tradesObj.SMA.Add(new Point(trade.TimeUtcMilliseconds, trade.SMA));
                tradesObj.SMATwo.Add(new Point(trade.TimeUtcMilliseconds, trade.SMATwo));
                tradesObj.Price.Add(new Point(trade.TimeUtcMilliseconds, decimal.ToDouble(trade.Price)));
                tradesObj.SMAUpper.Add(new Point(trade.TimeUtcMilliseconds, trade.SMAUpper));
                tradesObj.SMALower.Add(new Point(trade.TimeUtcMilliseconds, trade.SMALower));
                tradesObj.Alma.Add(new Point(trade.TimeUtcMilliseconds, trade.Alma));
            }
            foreach(var order in orders)
            {
                if(order.BuyTime.HasValue && !order.SellTime.HasValue)
                {
                    order.SellTime = tradesAscending.Last().TimeUtcMilliseconds;
                    order.Sell = decimal.ToDouble(tradesAscending.Last().Price);
                }
            }
            tradesObj.Orders = orders.Select(o => new Point(o.OrderTime, o.Buy)).ToList();
            tradesObj.Buys = orders.Where(o => o.BuyTime.HasValue).Select(o => new Point(o.BuyTime.Value, o.Buy)).ToList();
            tradesObj.Sells = orders.Where(o => o.SellTime.HasValue).Select(o => new Point(o.SellTime.Value, o.Sell.Value)).ToList();
            tradesObj.Total = orders.Aggregate((double)0, (acc, o) => o.SellTime.HasValue ? acc + (o.Sell.Value - o.Buy) : acc);
            tradesObj.TotalSpent = orders.Aggregate((double)0, (acc, o) => o.BuyTime.HasValue ? acc + o.Buy : acc);
            tradesObj.BuysOutstanding = orders.Where(o => o.BuyTime.HasValue && !o.SellTime.HasValue).Count();
            tradesObj.Outstanding = orders.Where(o => !o.BuyTime.HasValue).Count();
            return tradesObj;
        }
    }
    public class Order
    {
        public double Buy { get; set; }
        public long? BuyTime { get; set; }
        public double? Sell { get; set; }
        public double Stop { get; set; }
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
            IdealUpper = new List<Point>();
        }
        public List<Point> SMA { get; set; }
        public List<Point> SMATwo { get; set; }
        public List<Point> SMAThree { get; set; }
        public List<Point> Price { get; set; }
        public List<Point> SMAUpper { get; set; }
        public List<Point> IdealUpper { get; set; }
        public List<Point> SMALower { get; set; }
        public List<Point> Alma { get; set; }
        public List<Point> Orders { get; set; }
        public List<Point> Buys { get; set; }
        public List<Point> Sells { get; set; }
        public double Total { get; set; }
        public double TotalSpent { get; set; }
        public int BuysOutstanding { get; set; }
        public int Outstanding { get; set; }
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
