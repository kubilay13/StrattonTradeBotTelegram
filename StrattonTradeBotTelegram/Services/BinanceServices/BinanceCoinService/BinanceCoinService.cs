using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Objects.Options;
using CryptoExchange.Net.Authentication;

namespace StrattonTradeBotTelegram.Services.BinanceServices.BinanceCoinService
{
    public class BinanceCoinService
    {
        private readonly BinanceRestClient _binanceRestClient;

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

    }
}