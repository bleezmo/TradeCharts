// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your Javascript code.

var ctx = document.getElementById('myChart').getContext('2d');
var ordersCtx = document.getElementById('ordersChart').getContext('2d');
$.ajax({
    dataType: "json",
    url: "/Trades/NRG/6/0",
    success: function (trades) {
        document.getElementById('total').innerHTML = "Total: "+trades.total;
        document.getElementById('outstanding').innerHTML = "Outstanding: " + trades.outstanding;
        document.getElementById('buysoutstanding').innerHTML = "Buys Outstanding: " + trades.buysOutstanding;
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
                        label: "SMAUpper",
                        data: trades.smaUpper,
                        fill: false,
                        borderColor: "rgb(42, 112, 64)",
                        borderWidth: 2,
                        pointRadius: 0
                    },
                    {
                        label: "IdealUpper",
                        data: trades.idealUpper,
                        fill: false,
                        borderColor: "rgb(245, 66, 239)",
                        borderWidth: 2,
                        pointRadius: 0
                    },
                    {
                        label: "SMALower",
                        data: trades.smaLower,
                        fill: false,
                        borderColor: "rgb(42, 112, 64)",
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
});