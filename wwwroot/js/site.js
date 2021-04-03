// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your Javascript code.

var ctx = document.getElementById('myChart').getContext('2d');
var ordersCtx = document.getElementById('ordersChart').getContext('2d');
var symbols = [
    "NRG",
    "ENVA",
    "VZ",
    "ANF",
    "FLO",
    "CARA",
    "SLB",
    "F",
    "C",
    "SXC",
    "AMKR",
    "ARRY",
    "KOP",
    "KMI",
    "DNOW",
    "PFE",
    "VTRS",
    "WFC",
    "CMCO",
    "COP",
    "CXW",
    "APOG",
    "EAF",
    "CMC"];

var chartSymbol = "PFE";
var totaltotal = 0;
var totalbuysoutstanding = 0;
var totalspent = 0;
var hits = 0;
var misses = 0;
var count = 0;
for (var i = 0; i < symbols.length; i++) {
    $.ajax({
        dataType: "json",
        url: "/Trades/" + symbols[i] + "/0/0/0",
        success: (function (index) {
            return function (trades) {
                count++;
                totaltotal = totaltotal + trades.total;
                totalbuysoutstanding = totalbuysoutstanding + trades.buysOutstanding
                totalspent = totalspent + trades.totalSpent;
                hits = hits + trades.hits;
                misses = misses + trades.misses;
                if (symbols[index] === chartSymbol) {
                    chartTrades(trades, symbols[index]);
                }
                if (count == symbols.length) {
                    document.getElementById('totalhits').innerHTML = "Hits: " + hits;
                    document.getElementById('totalmisses').innerHTML = "Misses: " + misses;
                    document.getElementById('hitmiss').innerHTML = "Hit/Miss ratio: " + (hits/misses);
                    document.getElementById('totaltotal').innerHTML = "Total: " + totaltotal;
                    document.getElementById('totalspent').innerHTML = "Total Spent: " + totalspent;
                    document.getElementById('totalgain').innerHTML = "Gain: " + (totaltotal / totalspent);
                }
            }
        })(i)
    });
}
function chartTrades(trades, symbol) {
    document.getElementById('total').innerHTML = "Total: " + trades.total;
    document.getElementById('hits').innerHTML = "Hits: " + trades.hits;
    document.getElementById('misses').innerHTML = "Misses: " + trades.misses;
    var myChart = new Chart(ctx, {
        type: 'line',
        data: {
            datasets: [
                {
                    label: "Price",
                    data: trades.price,
                    fill: false,
                    borderColor: "rgb(36, 109, 173)",
                    borderWidth: 2,
                    pointRadius: 0
                },
                {
                    label: "SMA",
                    data: trades.sma,
                    fill: false,
                    borderColor: "rgb(97, 74, 81)",
                    borderWidth: 2,
                    pointRadius: 0
                },
                {
                    label: "SMATwo",
                    data: trades.smaTwo,
                    fill: false,
                    borderColor: "rgb(156, 159, 217)",
                    borderWidth: 2,
                    pointRadius: 0
                },
                {
                    label: "SMAUpper",
                    data: trades.smaUpper,
                    fill: false,
                    borderColor: "rgb(0, 0, 0)",
                    borderWidth: 2,
                    pointRadius: 0
                },
                {
                    label: "SMALower",
                    data: trades.smaLower,
                    fill: false,
                    borderColor: "rgb(0, 0, 0)",
                    borderWidth: 2,
                    pointRadius: 0
                },
                {
                    label: "SMATwoUpper",
                    data: trades.smaTwoUpper,
                    fill: false,
                    borderColor: "rgb(3, 8, 105)",
                    borderWidth: 2,
                    pointRadius: 0
                },
                {
                    label: "SMATwoLower",
                    data: trades.smaTwoLower,
                    fill: false,
                    borderColor: "rgb(3, 8, 105)",
                    borderWidth: 2,
                    pointRadius: 0
                },
                {
                    label: "Alma",
                    data: trades.alma,
                    fill: false,
                    borderColor: "rgb(232, 108, 46)",
                    borderWidth: 2,
                    pointRadius: 0
                },
            ]
        },
        options: {
            scales: {
                xAxes: [{
                    type: 'linear',
                    position: 'bottom'
                }]
            }
        }
    });
    var slopeChart = new Chart(ordersCtx, {
        type: 'line',
        data: {
            datasets: [
                {
                    label: "Price",
                    data: trades.price,
                    fill: false,
                    borderColor: "rgb(36, 109, 173)",
                    lineTension: 0.1,
                    borderWidth: 2,
                    pointRadius: 0
                },
                {
                    label: "orders",
                    data: trades.orders,
                    fill: false,
                    borderColor: "rgb(247, 186, 204)",
                    lineTension: 0.1,
                    borderWidth: 2,
                },
                {
                    label: "Buys",
                    data: trades.buys,
                    fill: false,
                    borderColor: "rgb(97, 74, 81)",
                    lineTension: 0.1,
                    borderWidth: 2,
                },
                {
                    label: "Sells",
                    data: trades.sells,
                    fill: false,
                    borderColor: "rgb(232, 108, 46)",
                    lineTension: 0.1,
                    borderWidth: 2,
                },
            ]
        },
        options: {
            scales: {
                xAxes: [{
                    type: 'linear',
                    position: 'bottom'
                }]
            }
        }
    });
}