using Telegram.Bot;
using StrattonTradeBotTelegram.Services.TelegramServices;
using StrattonTradeBotTelegram.Services.BinanceServices.BinanceCoinService;

var builder = WebApplication.CreateBuilder(args);


// Telegram Bot Token'i yap�land�rmadan al�n

var telegramBotToken = builder.Configuration["TelegramBot:Token"];
var binanceApiKey = builder.Configuration["Binance:ApiKey"];
var binanceApiSecret = builder.Configuration["Binance:ApiSecret"];

// Testnet veya Live se�ene�ini belirleyin
bool isTestnet = builder.Configuration.GetValue<bool>("Binance:IsTestnet", true);

var binanceService = new BinanceCoinService(binanceApiKey, binanceApiSecret, isTestnet);
// Telegram Bot istemcisini ba��ml�l�k enjeksiyonuna ekleyin
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(telegramBotToken));

// Di�er servisleri ekle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(new TelegramBotService(telegramBotToken, binanceApiKey, binanceApiSecret, isTestnet, binanceService));
var app = builder.Build();

// Telegram bot servisini ba�lat
var telegramBotService = app.Services.GetRequiredService<TelegramBotService>();
telegramBotService.StartReceiving();

// HTTP pipeline'� yap�land�r
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
