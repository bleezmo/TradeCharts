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
        [HttpGet("{symbol}/{range}/{skip}")]
        public async Task<Trades> Get(string symbol, int range, int skip)
        {
            using var stocksContext = new StocksContext();
            var recent = await stocksContext.Trades.Where(t => t.Symbol == symbol).OrderByDescending(t => t.TimeUtcMilliseconds).FirstOrDefaultAsync();
            var start = recent.TimeUtcMilliseconds - TimeSpan.FromHours(skip).TotalMilliseconds;
            var stop = recent.TimeUtcMilliseconds - TimeSpan.FromHours(skip+range).TotalMilliseconds;
            var trades = stocksContext.Trades.Where(t => t.Symbol == symbol && t.TimeUtcMilliseconds < start && t.TimeUtcMilliseconds > stop);
            var tradesAscending = await trades.OrderBy(t => t.TimeUtcMilliseconds).ToListAsync();
            var tradesDescending = await trades.OrderByDescending(t => t.TimeUtcMilliseconds).ToListAsync();
            var orders = new List<Order>();
            var tradesObj = new Trades();

            foreach(var trade in tradesAscending)
            {
                var smaUppserSMA = SMAUpperIdeal(tradesDescending.Where(t => t.TimeUtcMilliseconds < trade.TimeUtcMilliseconds), trade, 30);
                if (trade.SMAUpper > smaUppserSMA && trade.Alma < trade.SMA && 
                    //orders.Where(o => !o.SellTime.HasValue).Count() < 3 && 
                    (!orders.Any() || trade.TimeUtcMilliseconds > (orders.Last().OrderTime + TimeSpan.FromMinutes(30).TotalMilliseconds)) 
                    /*&& tradesDescending.Where(t => t.TimeUtcMilliseconds < trade.TimeUtcMilliseconds).Aggregate((double)0, (acc,t) => acc+t.AlmaSlope) < 0*/)
                {
                    var stdev = (trade.SMA - trade.SMALower) / 2;
                    var stdev_coeff = 1 - (Math.Atan(trade.SMASlope) / 90);
                    var buy = trade.SMASlope < 0 ? (trade.SMALower+((trade.SMA - trade.SMALower)*0.05)) : trade.SMA - (stdev * stdev_coeff);
                    if (!orders.Any() || (!orders.Any(o => !o.SellTime.HasValue && buy < o.Buy) && (orders.Last().Sell + (orders.Last().Sell / 4)) > buy))
                    {
                        orders.Add(new Order
                        {
                            Buy = buy,
                            Sell = Math.Max(buy*1.004, trade.SMAUpper * 0.8),
                            OrderTime = trade.TimeUtcMilliseconds
                        });
                    }
                }
                foreach(var order in orders)
                {
                    if(order.BuyTime.HasValue && !order.SellTime.HasValue && order.OrderTime < (trade.TimeUtcMilliseconds + TimeSpan.FromMinutes(60).TotalMilliseconds))
                    {
                        order.Sell = order.Sell - ((order.Sell - order.Buy) / 2);
                    }
                    if (order.BuyTime.HasValue && !order.SellTime.HasValue && order.Sell <= decimal.ToDouble(trade.Price)/* && Math.Atan(trade.SMASlope) <= 20 && Math.Atan(trade.AlmaSlope) <= 5*/)
                    {
                        order.SellTime = trade.TimeUtcMilliseconds;
                        order.Sell = decimal.ToDouble(trade.Price);
                    }
                    if (!order.BuyTime.HasValue && order.Buy >= decimal.ToDouble(trade.Price))
                    {
                        order.BuyTime = trade.TimeUtcMilliseconds;
                    }
                }
                orders = orders.Where(o => o.BuyTime.HasValue || trade.TimeUtcMilliseconds < (o.OrderTime + TimeSpan.FromHours(1).TotalMilliseconds)).ToList();
                tradesObj.SMA.Add(new Point(trade.TimeUtcMilliseconds, trade.SMA));
                tradesObj.Price.Add(new Point(trade.TimeUtcMilliseconds, decimal.ToDouble(trade.Price)));
                tradesObj.SMAUpper.Add(new Point(trade.TimeUtcMilliseconds, trade.SMAUpper));
                tradesObj.IdealUpper.Add(new Point(trade.TimeUtcMilliseconds, smaUppserSMA)); //tradesList.Select(t => new Point(t.TimeUtcMilliseconds, t.SMA * 1.003)),
                tradesObj.SMALower.Add(new Point(trade.TimeUtcMilliseconds, trade.SMALower));
                tradesObj.Alma.Add(new Point(trade.TimeUtcMilliseconds, trade.Alma));
            }
            tradesObj.Orders = orders.Select(o => new Point(o.OrderTime, o.Buy)).ToList();
            tradesObj.Buys = orders.Where(o => o.BuyTime.HasValue).Select(o => new Point(o.BuyTime.Value, o.Buy)).ToList();
            tradesObj.Sells = orders.Where(o => o.SellTime.HasValue).Select(o => new Point(o.SellTime.Value, o.Sell)).ToList();
            tradesObj.Total = orders.Aggregate((double)0, (acc, o) => o.SellTime.HasValue ? acc + (o.Sell - o.Buy) : acc);
            tradesObj.BuysOutstanding = orders.Where(o => o.BuyTime.HasValue && !o.SellTime.HasValue).Count();
            tradesObj.Outstanding = orders.Where(o => !o.BuyTime.HasValue).Count();
            return tradesObj;
        }

        private static double SMAUpperIdeal(IEnumerable<Trade> trades, Trade current, int lookbackMinutes)
        {
            var cutoff = current.TimeUtc.Subtract(TimeSpan.FromMinutes(lookbackMinutes));
            var allTrades = new List<Trade> { current };
            allTrades.AddRange(trades.Where(t => t.TimeUtc.CompareTo(cutoff) > 0));
            var upperSMA = allTrades.Aggregate((double)0, (acc, tp) => acc + tp.SMAUpper) / allTrades.Count;
            var stdev = Math.Sqrt(allTrades.Aggregate((double)0, (acc, tp) => acc + Math.Pow(tp.SMAUpper - upperSMA, 2)) / allTrades.Count);
            return Math.Max(current.SMA * 1.0025, upperSMA + (stdev / 2));
        }
    }
    public class Order
    {
        public double Buy { get; set; }
        public long? BuyTime { get; set; }
        public double Sell { get; set; }
        public long? SellTime { get; set; }
        public long OrderTime { get; set; }
    }
    public class Trades
    {
        public Trades()
        {
            SMA = new List<Point>();
            Price = new List<Point>();
            SMAUpper = new List<Point>();
            SMAUpperSMA = new List<Point>();
            SMALower = new List<Point>();
            Alma = new List<Point>();
            IdealUpper = new List<Point>();
        }
        public List<Point> SMA { get; set; }
        public List<Point> Price { get; set; }
        public List<Point> SMAUpper { get; set; }
        public List<Point> IdealUpper { get; set; }
        public List<Point> SMAUpperSMA { get; set; }
        public List<Point> SMALower { get; set; }
        public List<Point> Alma { get; set; }
        public List<Point> Orders { get; set; }
        public List<Point> Buys { get; set; }
        public List<Point> Sells { get; set; }
        public double Total { get; set; }
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
