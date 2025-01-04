using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using StrattonTradeBotTelegram.Services.BinanceServices.BinanceCoinService;
using Binance.Net.Enums;

namespace StrattonTradeBotTelegram.Services.TelegramServices
{
    public class TelegramBotService
    {
        private readonly ITelegramBotClient _botService;
        private readonly BinanceCoinService _binanceCoinService;
        private readonly PriceReminderService _priceReminderService;
        private readonly SymbolFibonacciService _symbolFibonacciService;
        private bool _isPriceReminderStarted = false;
        public TelegramBotService(string token, string binanceApiKey, string binanceApiSecret, bool isTestnet,BinanceCoinService binanceCoinService, PriceReminderService priceReminderService, SymbolFibonacciService symbolFibonacciService)
        {
            var botOptions = new TelegramBotClientOptions(token);
            _botService = new TelegramBotClient(botOptions);
            _binanceCoinService = new BinanceCoinService(binanceApiKey, binanceApiSecret, isTestnet);
            _binanceCoinService = binanceCoinService;
            _priceReminderService = priceReminderService;
            _symbolFibonacciService = symbolFibonacciService;
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
                if (messageText.StartsWith("/Komutlar"))
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "/SYMBOL");
                }
                if (messageText.StartsWith("/Balance"))
                {
                    var balance= await _binanceCoinService.GetUserFuturesBalanceAsync();
                    await botClient.SendTextMessageAsync(message.Chat.Id, $"Balance: {balance} $");
                }
                if (messageText.StartsWith("/FiyatTakipAç"))
                {
                    // Fiyat hatırlatıcı servisi başlatılmadıysa başlat
                    if (!_isPriceReminderStarted)
                    {
                        // PriceReminderService başlat
                        _priceReminderService.StartPriceReminder(message.Chat.Id);  // Chat ID'yi buraya geçir
                        _isPriceReminderStarted = true;  // Zamanlayıcı başlatıldı

                        await botClient.SendTextMessageAsync(message.Chat.Id, "Fiyat hatırlatıcı başlatıldı!");
                    }
                    else
                    {
                        // Zaten başlatıldıysa kullanıcıyı bilgilendir
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Fiyat hatırlatıcı zaten başlatıldı.");
                    }
                }

                else if (messageText.StartsWith("/Kapat"))
                {
                    // Fiyat hatırlatıcıyı durdur
                    if (_isPriceReminderStarted)
                    {
                        // Timer'ı durdur
                        _priceReminderService?.Dispose();
                        _isPriceReminderStarted = false;

                        // Kullanıcıya yanıt gönder
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Fiyat hatırlatıcı durduruldu.");
                    }
                }

                //FİBONACCİ HESAPLAMA 
                if (messageText.StartsWith("/"))
                {
                    // Komutun parçalarını ayır ("/BTCUSDT FİBONACCİ" -> ["BTCUSDT", "FİBONACCİ"])
                    var parts = messageText.Substring(1).Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    // Coin sembolünü al
                    if (parts.Length > 0)
                    {
                        string symbol = parts[0].ToUpper();

                        // Eğer "FİBONACCİ" komutu varsa ve sembol "USDT" ile bitiyorsa analiz yap
                        if (parts.Length > 1 && parts[1].Equals("FİBONACCİ", StringComparison.OrdinalIgnoreCase) && symbol.EndsWith("USDT"))
                        {
                            try
                            {
                                // Binance API'den Kline verilerini al (son 1 saatlik veri)
                                var klineResult = await _symbolFibonacciService.GetKlinesAsync(
                                 symbol,
                                 KlineInterval.FiveMinutes,
                                 limit: 200);


                                // Kline verileri üzerinden analiz yapın
                                decimal highestHigh = klineResult.Max(k => k.HighPrice);
                                decimal lowestLow = klineResult.Min(k => k.LowPrice);

                                Console.WriteLine($"Highest High: {highestHigh}, Lowest Low: {lowestLow}");

                                // Fibonacci seviyelerini hesaplayın
                                var fibLevels = _symbolFibonacciService.CalculateFibonacciLevels(highestHigh, lowestLow);

                                

                                Console.WriteLine($"Highest High: {highestHigh}, Lowest Low: {lowestLow}");


                                // Fibonacci seviyelerini kullanıcıya gönderins
                                string fibMessage = $"{symbol} Fibonacci Seviyeleri:\n" +
                                                    $"0.000: {fibLevels[0]:F2} USDT\n" +
                                                    $"0.236: {fibLevels[1]:F2} USDT\n" +
                                                    $"0.382: {fibLevels[2]:F2} USDT\n" +
                                                    $"0.500: {fibLevels[3]:F2} USDT\n" +
                                                    $"0.618: {fibLevels[4]:F2} USDT\n" +
                                                    $"0.786: {fibLevels[5]:F2} USDT\n" +
                                                    $"1.000: {fibLevels[6]:F2} USDT";

                                await botClient.SendTextMessageAsync(message.Chat.Id, fibMessage);
                            }
                            catch (Exception ex)
                            {
                                await botClient.SendTextMessageAsync(message.Chat.Id, $"Bir hata oluştu: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        // Coin sembolü belirtilmediğinde uyarı mesajı
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Eğer geçerli bir Coinin Fibonaccisini öğrenmek için: (örneğin, /BTCUSDT FİBONACCİ).");
                    }
                }

                //İLKEL ANALİZ

                if (messageText.StartsWith("/"))
                {
                    // Coin sembolünü al ("/BTCUSDT" -> "BTCUSDT")
                    string symbol = messageText.Substring(1).ToUpper();

                    // Eğer coin sembolü "USDT" ile bitiyorsa fiyat bilgisini al
                    if (symbol.EndsWith("USDT"))
                    {
                        try
                        {
                            // Binance API üzerinden fiyat bilgisini al
                            string price = await _binanceCoinService.BinanceCoinAmount(symbol);

                            // Fiyatı temizleyin: '$' ve diğer metinlerden kurtulun
                            string cleanPrice = new string(price.Where(c => char.IsDigit(c) || c == '.').ToArray());

                            // Eğer temizlenmiş fiyat bir decimal'a dönüşebiliyorsa, dönüştürme işlemi yapılır
                            if (decimal.TryParse(cleanPrice, out decimal entryPrice))
                            {
                                // Başarıyla dönüşüm yapıldı, entryPrice kullanılabilir
                                Console.WriteLine($"Giriş fiyatı: {entryPrice}");
                            }
                            else
                            {
                                // Fiyat geçerli bir decimal değilse, hata mesajı
                                Console.WriteLine("Fiyat geçerli bir decimal değeri değil.");
                            }

                            // TP ve SL seviyelerini belirle
                            decimal tpPrice = entryPrice * 1.02m;  // %2 TP
                            decimal slPrice = entryPrice * 0.99m;  // %1 SL

                            // Kullanıcıya giriş fiyatı, TP ve SL seviyelerini gönder

                            if(tpPrice>entryPrice)
                            {
                                await botClient.SendTextMessageAsync(message.Chat.Id, "LONG");
                                await botClient.SendTextMessageAsync(message.Chat.Id,
                              $"{symbol} için işlem bilgileri:\n" +
                              $"Giriş Fiyatı: {entryPrice} USDT\n" +
                              $"Take Profit (TP): {tpPrice} USDT\n" +
                              $"Stop Loss (SL): {slPrice} USDT");
                                await botClient.SendTextMessageAsync(message.Chat.Id, "Yatırım Tavsiyesi Değildir.");
                            }else
                            {
                                await botClient.SendTextMessageAsync(message.Chat.Id, "SHORT");
                                await botClient.SendTextMessageAsync(message.Chat.Id,
                              $"{symbol} için işlem bilgileri:\n" +
                              $"Giriş Fiyatı: {entryPrice} USDT\n" +
                              $"Take Profit (TP): {tpPrice} USDT\n" +
                              $"Stop Loss (SL): {slPrice} USDT");
                                await botClient.SendTextMessageAsync(message.Chat.Id, "Yatırım Tavsiyesi Değildir.");
                            }
                          
                        }
                        catch (Exception ex)
                        {
                            // Eğer hata olursa hata mesajı gönder
                            await botClient.SendTextMessageAsync(message.Chat.Id, "Fiyat alınırken bir hata oluştu. Lütfen tekrar deneyin.");
                        }
                    }
                }

                //PİYASA FİYATINDAN İŞLEM AÇMA
                if (messageText.StartsWith("/"))
                {
                    // Komutun parçalarını ayrıştır
                    var parts = messageText.Substring(1).Split(' '); // '/' işaretini çıkar ve boşluklardan ayır
                    if (parts.Length == 4)
                    {
                        string symbol = parts[0].ToUpper();  // Sembol (ör. BTCUSDT)
                        //string action = parts[1].ToUpper();  // İşlem türü (LONG/SHORT)
                        int leverage;
                        decimal amount;
                        var tradeSuggestion = await _binanceCoinService.GetTradeSuggestion(symbol);

                        // Ticaret önerisini kullanıcıya gönder
                        await botClient.SendTextMessageAsync(message.Chat.Id, tradeSuggestion);
                        // Geçerli kaldıracı ve miktarı doğrula
                        //if (int.TryParse(parts[2], out leverage) && decimal.TryParse(parts[3], out amount))
                        //{
                        //    if (symbol.EndsWith("USDT") && (action == "LONG" || action == "SHORT"))
                        //    {
                        //        // Binance API'si üzerinden işlem aç
                        //        var result = await _binanceCoinService.OpenPosition(symbol, action, leverage, amount);

                        //        // Kullanıcıya işlem sonucu mesajı gönder
                        //        await botClient.SendTextMessageAsync(message.Chat.Id, result);
                        //        var balance = await _binanceCoinService.GetUserFuturesBalanceAsync();
                        //        await botClient.SendTextMessageAsync(message.Chat.Id, $"Balance: {balance-amount} $");

                        //    }
                        //    else
                        //    {
                        //        await botClient.SendTextMessageAsync(message.Chat.Id, "Hatalı komut. Doğru format: /symbol LONG/SHORT LEVERAGE AMOUNT");
                        //    }
                        //}
                        //else
                        //{
                        //    await botClient.SendTextMessageAsync(message.Chat.Id, "Hatalı kaldırac veya miktar. Örnek: /BTCUSDT LONG 25 100");
                        //}
                    }
                   
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

