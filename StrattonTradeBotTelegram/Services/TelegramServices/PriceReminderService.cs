using StrattonTradeBotTelegram.Services.BinanceServices.BinanceCoinService;
using Telegram.Bot;

namespace StrattonTradeBotTelegram.Services.TelegramServices
{
    public class PriceReminderService : IDisposable
    {
        private readonly ITelegramBotClient _telegramBotClient;
        private readonly BinanceCoinService _binanceCoinService;
        private  long _chatId;
        private Timer? _timer;
        private bool _isReminderActive;
        public PriceReminderService(ITelegramBotClient telegramBotClient,BinanceCoinService binanceCoinService, long chatId)
        {
            _binanceCoinService = binanceCoinService;
            _telegramBotClient = telegramBotClient;
            _chatId = chatId;
            _isReminderActive = false;
        }
        public void StartPriceReminder(long chatId)
        {
            if (_isReminderActive)
            {
                return; // Zaten aktifse, tekrar başlatma
            }

            _isReminderActive = true; // Hatırlatıcı başlatılıyor
            _chatId = chatId;

            _timer = new Timer(async _ =>
            {
                // Fiyatları al
                var prices = await _binanceCoinService.BinancePopularCoinAmountTimer();

                // Fiyatları Telegram botu üzerinden gönder
                await _telegramBotClient.SendTextMessageAsync(_chatId, prices);
            }, null, TimeSpan.Zero, TimeSpan.FromHours(1)); // İlk başlatma hemen, sonra her 1 dakikada bir
        }

        // Timer'ı durdurma metodu
        public void StopPriceReminder()
        {
            if (!_isReminderActive) return; // Zaten durdurulmuşsa, işlem yapma

            _timer?.Dispose(); // Timer'ı durdur
            _isReminderActive = false; // Hatırlatıcı durdu
        }

        // IDisposable interface'ini uygulamak için Dispose() metodunu ekliyoruz
        public void Dispose()
        {
            StopPriceReminder(); // Timer'ı durdur
        }

    }
}
