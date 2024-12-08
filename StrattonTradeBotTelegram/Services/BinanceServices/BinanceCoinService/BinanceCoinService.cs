using Binance.Net.Clients;
using CryptoExchange.Net.Authentication;

namespace StrattonTradeBotTelegram.Services.BinanceServices.BinanceCoinService
{
    public class BinanceCoinService
    {
        private readonly BinanceRestClient _binanceRestClient;

        public BinanceCoinService(BinanceRestClient binanceRestClient)
        {
            var binanceClient = new BinanceRestClient(options => {
                options.ApiCredentials = new ApiCredentials("GVQvgwitqz468A3p02ahtuzE60CUqAut6nUVtxOu8Fh2NOsBrj5awx0E5IfnR4sh", "using Binance.Net.Clients;\r\nusing CryptoExchange.Net.Authentication;\r\n\r\nnamespace StrattonTradeBotTelegram.Services.BinanceServices.BinanceCoinService\r\n{\r\n    public class BinanceCoinService\r\n    {\r\n        private readonly BinanceRestClient _binanceRestClient;\r\n\r\n        public BinanceCoinService(BinanceRestClient binanceRestClient)\r\n        {\r\n            var binanceClient = new BinanceRestClient(options => {\r\n                options.ApiCredentials = new ApiCredentials(\"GVQvgwitqz468A3p02ahtuzE60CUqAut6nUVtxOu8Fh2NOsBrj5awx0E5IfnR4sh\", \"KM48GmcyVJzYV7LLPoRp3qvWYX71VfHFSULe9q2h3urowo1Ib2zzxhtmo74Gjq8k\");\r\n            });\r\n            _binanceRestClient = binanceRestClient;\r\n        }\r\n\r\n        public async Task<string> BinanceCoinAmount(string Symbol)\r\n        {\r\n            var binancerestclient= new BinanceRestClient();\r\n            var result = await binancerestclient.SpotApi.ExchangeData.GetTickerAsync(Symbol);\r\n            var futuresamount = await binancerestclient.UsdFuturesApi.Account.GetBalancesAsync();\r\n            if (result.Success)\r\n            {\r\n                return $\"{result.Data.LastPrice} $ {futuresamount}\";  \r\n            }\r\n                return $\"{Symbol} Fiyat bilgisi alındı!\"; \r\n        }\r\n        \r\n    }\r\n}\r\n");
            });
            _binanceRestClient = binanceRestClient;
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
        
    }
}
