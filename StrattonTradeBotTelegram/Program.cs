using Telegram.Bot;
using StrattonTradeBotTelegram.Services.TelegramServices;
using StrattonTradeBotTelegram.Services.BinanceServices.BinanceCoinService;

var builder = WebApplication.CreateBuilder(args);


// Telegram Bot Token'i yapýlandýrmadan alýn

var telegramBotToken = builder.Configuration["TelegramBot:Token"];
var binanceApiKey = builder.Configuration["Binance:ApiKey"];
var binanceApiSecret = builder.Configuration["Binance:ApiSecret"];

// Testnet veya Live seçeneðini belirleyin
bool isTestnet = builder.Configuration.GetValue<bool>("Binance:IsTestnet", true);

var binanceService = new BinanceCoinService(binanceApiKey, binanceApiSecret, isTestnet);
// Telegram Bot istemcisini baðýmlýlýk enjeksiyonuna ekleyin
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(telegramBotToken));

// Diðer servisleri ekle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(new TelegramBotService(telegramBotToken, binanceApiKey, binanceApiSecret, isTestnet, binanceService));
var app = builder.Build();

// Telegram bot servisini baþlat
var telegramBotService = app.Services.GetRequiredService<TelegramBotService>();
telegramBotService.StartReceiving();

// HTTP pipeline'ý yapýlandýr
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
