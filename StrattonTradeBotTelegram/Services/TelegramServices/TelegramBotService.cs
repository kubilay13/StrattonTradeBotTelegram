using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using StrattonTradeBotTelegram.Services.BinanceServices.BinanceCoinService;

namespace StrattonTradeBotTelegram.Services.TelegramServices
{
    public class TelegramBotService
    {
        private readonly ITelegramBotClient _botService;
        private readonly BinanceCoinService _binanceCoinService;

        public TelegramBotService(ITelegramBotClient telegramBotClient,IConfiguration configuration,BinanceCoinService binanceCoinService)
        {
            _binanceCoinService = binanceCoinService;
            _botService = telegramBotClient;
        }
        public void StartReceiving()
        {
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>() 
            };

            _botService.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken: CancellationToken.None);

            Console.WriteLine("Bot çalışmaya başladı...");
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message is { } message)
            {
                var messageText = update.Message.Text;

                if (messageText.StartsWith("/"))
                {
                    // BinanceCoinService'den fiyat bilgisini al
                    string symbol = messageText.Substring(1); // /BTCUSDT kısmındaki '/' karakterini çıkar
                    var price = await _binanceCoinService.BinanceCoinAmount(symbol);
                    // Fiyat bilgisini kullanıcıya gönder
                    await botClient.SendTextMessageAsync(update.Message.Chat.Id, $"{symbol} fiyatı: {price}");
                }

            }
          
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Hata oluştu: {exception.Message}");
            return Task.CompletedTask;
        }
    }

}

