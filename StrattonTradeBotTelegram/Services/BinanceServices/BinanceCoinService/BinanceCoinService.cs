using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients.UsdFuturesApi;
using Binance.Net.Objects.Options;
using CryptoExchange.Net.Authentication;

namespace StrattonTradeBotTelegram.Services.BinanceServices.BinanceCoinService
{
    public class BinanceCoinService
    {
        private readonly BinanceRestClient _binanceRestClient;
        private readonly IBinanceRestClientUsdFuturesApi _usdFuturesApiClient;
        private readonly IBinanceRestClientUsdFuturesApiAccount _accountApiClient;
        private readonly IBinanceRestClientUsdFuturesApiTrading _tradingApiClient;


        public BinanceCoinService(string apiKey, string apiSecret, bool isTestnet)
        {
            var binanceOptions = new BinanceRestOptions
            {
                ApiCredentials = new ApiCredentials(apiKey, apiSecret),
                Environment = isTestnet ? BinanceEnvironment.Testnet : BinanceEnvironment.Live,
                AutoTimestamp = true
            };

            _binanceRestClient = new BinanceRestClient(options =>
            {
                options.ApiCredentials = binanceOptions.ApiCredentials;
                options.Environment = binanceOptions.Environment;
            });
            _usdFuturesApiClient = _binanceRestClient.UsdFuturesApi;
            _accountApiClient = _usdFuturesApiClient.Account;
            _tradingApiClient = _usdFuturesApiClient.Trading;
        }

        public async Task<string> BinanceCoinAmount(string Symbol)
        {
         
            var result = await _binanceRestClient.SpotApi.ExchangeData.GetTickerAsync(Symbol);
            var futuresamount = await _binanceRestClient.UsdFuturesApi.Account.GetBalancesAsync();
            if (result.Success)
            {
                return $"{result.Data.LastPrice} $ {futuresamount}";  
            }
                return $"{Symbol} Fiyat bilgisi alındı!"; 
        }

        public async Task<string> BinancePopularCoinAmountTimer()
        {
            var symbols = new[] { "BTCUSDT", "ETHUSDT", "BNBUSDT", "TRXUSDT" };
            var pricemessage = new List<string>();
            foreach(var i in symbols)
            {
                var result= await _binanceRestClient.SpotApi.ExchangeData.GetTickerAsync($"{i}");
                if(result.Success)
                {

                    pricemessage.Add($"{i} fiyatı {result.Data.LastPrice} $");
                    
                }
                else
                {
                    pricemessage.Add("Fiyat Bilgileri Alınamadı.");
                }
            }
            return string.Join("\n", pricemessage);
        }

        public async Task<decimal> GetUserFuturesBalanceAsync()
        {
            try
            {
                var result = await _binanceRestClient.UsdFuturesApi.Account.GetAccountInfoV2Async();
                if (result.Success)
                {
                    var balance = result.Data.TotalWalletBalance;
                    return balance;
                }
                else
                {
                    throw new Exception("Vadeli işlemler bakiyesi alınamadı.❌");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata: {ex.Message} ❌");
                throw;
            }
        }


        //public async Task<string> GetImprovedFibonacciTradeWithTPAndSL(string symbol)
        //{
        //    // Kline verisi alınması
        //    var klineResult = await _binanceRestClient.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, KlineInterval.OneHour, limit: 150);
        //    if (!klineResult.Success)
        //        return $"Kline verileri alınamadı: {klineResult.Error?.Message}";

        //    var closes = klineResult.Data.Select(k => k.ClosePrice).ToList();
        //    var highPrices = klineResult.Data.Select(k => k.HighPrice).ToList();
        //    var lowPrices = klineResult.Data.Select(k => k.LowPrice).ToList();

        //    // Swing High/Low hesapla (örnek: 20 periyotluk)
        //    decimal highestHigh = highPrices.TakeLast(20).Max();
        //    decimal lowestLow = lowPrices.TakeLast(20).Min();

        //    // Fibonacci seviyelerini hesapla
        //    var fibLevels = CalculateFibonacciLevels(highestHigh, lowestLow);

        //    // Mevcut fiyatı al
        //    var priceResult = await _binanceRestClient.UsdFuturesApi.ExchangeData.GetPriceAsync(symbol);
        //    if (!priceResult.Success)
        //        return $"Fiyat bilgisi alınamadı: {priceResult.Error?.Message}";

        //    decimal currentPrice = priceResult.Data.Price;

        //    // ATR hesapla
        //    decimal atr = CalculateATR(klineResult.Data.ToList(), 14);

        //    // Alım, TP ve SL seviyelerini belirle
        //    string tradeSuggestion = $"Mevcut Fiyat: {currentPrice}\n" +
        //                             $"Fibonacci Seviyeleri:\n" +
        //                             $"0.236: {fibLevels[1]}\n" +
        //                             $"0.382: {fibLevels[2]}\n" +
        //                             $"0.5: {fibLevels[3]}\n" +
        //                             $"0.618: {fibLevels[4]}\n";

        //    // Ticaret önerisi
        //    if (currentPrice < fibLevels[1]) // Fiyat en alt seviyede
        //    {
        //        decimal tp = fibLevels[2];
        //        decimal sl = currentPrice - atr;

        //        tradeSuggestion += $"\n**LONG ÖNERİSİ**\n" +
        //                           $"Giriş Noktası: {currentPrice}\n" +
        //                           $"Take Profit (TP): {tp}\n" +
        //                           $"Stop Loss (SL): {sl}\n";
        //    }
        //    else if (currentPrice > fibLevels[4]) // Fiyat en üst seviyede
        //    {
        //        decimal tp = fibLevels[3];
        //        decimal sl = currentPrice + atr;

        //        tradeSuggestion += $"\n**SHORT ÖNERİSİ**\n" +
        //                           $"Giriş Noktası: {currentPrice}\n" +
        //                           $"Take Profit (TP): {tp}\n" +
        //                           $"Stop Loss (SL): {sl}\n";
        //    }
        //    else
        //    {
        //        tradeSuggestion += "\n**BEKLEME ÖNERİSİ**: Fiyat Fibonacci seviyeleri arasında.";
        //    }

        //    return tradeSuggestion;
        //}
    }
}