using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Polling;
using Newtonsoft.Json;

class Program
{
    private static readonly string TelegramToken = "7590668760:AAHU7thIfi6Aniir6f34KpetFdcvfyeESIs";
    private static readonly string WebAppUrl = "https://script.google.com/macros/s/AKfycbwoEsUQlug9tupfiLe8r40UckhszIqnBf-DxoICgdhdM2wvr7UKJRiNfY-ykVBXktP4/exec";
    private static readonly string ResumeFilePathUz = "C:\\Users\\PAVILION\\Desktop\\REZUME.doc";
    private static readonly string ResumeFilePathRu = "C:\\Users\\PAVILION\\Desktop\\РЕЗЮМЕ.doc";
    private static readonly ITelegramBotClient botClient = new TelegramBotClient(TelegramToken);
    private static Dictionary<long, string> userLanguages = new Dictionary<long, string>();
    private static Dictionary<long, string> userBranches = new Dictionary<long, string>();

    private static async Task Main()
    {
        var cts = new CancellationTokenSource();
        var receiverOptions = new ReceiverOptions { AllowedUpdates = new UpdateType[] { } };

        botClient.StartReceiving(
            async (bot, update, token) => await HandleUpdateAsync(bot, update, token),
            async (bot, ex, token) => Console.WriteLine($"Xatolik: {ex.Message}"),
            receiverOptions,
            cancellationToken: cts.Token
        );

        Console.WriteLine("Bot ishlayapti...");
        Console.ReadLine();
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message == null) return;
        var chatId = update.Message.Chat.Id;

        if (update.Message.Type == MessageType.Text)
        {
            string text = update.Message.Text;

            if (text == "/start")
            {
                await botClient.SendTextMessageAsync(chatId, "🇺🇿 Tilni tanlang | 🇷🇺 Выберите язык:", replyMarkup: LanguageKeyboard());
            }
            else if (text == "O'zbekcha 🇺🇿" || text == "Русский 🇷🇺")
            {
                string lang = text.Contains("O'zbekcha") ? "uz" : "ru";
                userLanguages[chatId] = lang;
                string welcomeMessage = lang == "uz" ? "📞 Kontakt raqamingizni jo‘nating." : "📞 Отправьте ваш контакт.";
                await botClient.SendTextMessageAsync(chatId, welcomeMessage, replyMarkup: ContactKeyboard(lang));
            }
            else if (text == "Toshkent" || text == "Samarqand" || text == "Ташкент" || text == "Самарканд")
            {
                userBranches[chatId] = text;
                await SendToWebApp(new { branch = text });

                string lang = userLanguages.ContainsKey(chatId) ? userLanguages[chatId] : "uz";
                string schoolKindMessage = lang == "uz" ? "🏫 Maktab yoki bog‘chani tanlang:" : "🏫 Выберите школу или детский сад:";

                await botClient.SendTextMessageAsync(chatId, schoolKindMessage, replyMarkup: SchoolKindKeyboard(lang));
            }
            else if (text == "Maktab 🏫" || text == "Bog‘cha 🏡" || text == "Школа 🏫" || text == "Детский сад 🏡")
            {
                await SendToWebApp(new { schoolKind = text });

                string lang = userLanguages.ContainsKey(chatId) ? userLanguages[chatId] : "uz";
                string jobMessage = lang == "uz" ? "💼 Ish o‘rnini tanlang:" : "💼 Выберите должность:";

                var keyboard = await JobVacanciesKeyboard();
                await botClient.SendTextMessageAsync(chatId, jobMessage, replyMarkup: keyboard);
            }

            else
            {
                await SendToWebApp(new { position = text });

                string lang = userLanguages.ContainsKey(chatId) ? userLanguages[chatId] : "uz";

                // Google Sheets'dan vakansiya haqida ma'lumot olish
                string jobInfo = await GetJobInfoFromSheet(text);
                string message = lang == "uz"
                    ? $"Vakansiya: {text}\nIsh haqida: {jobInfo}"
                    : $"Вакансия: {text}\nО работе: {jobInfo}";

                await botClient.SendTextMessageAsync(chatId, message);

                // Faylni yuborish
                string resumeFilePath = lang == "uz" ? ResumeFilePathUz : ResumeFilePathRu;
                string caption = lang == "uz" ? "Ish tavsifi" : "Описание работы";

                using (var fileStream = new FileStream(resumeFilePath, FileMode.Open, FileAccess.Read))
                {
                    await botClient.SendDocumentAsync(
                        chatId,
                        new InputFileStream(fileStream, Path.GetFileName(resumeFilePath)),
                        caption: caption
                    );
                }

                string resumeMessage = lang == "uz" ? "📎 O‘z rezumengizni jo‘nating." : "📎 Отправьте своё резюме.";
                await botClient.SendTextMessageAsync(chatId, resumeMessage);
            }
        }
        // Google Sheets'dan vakansiyaga oid ma'lumot olish

        else if (update.Message.Type == MessageType.Contact && update.Message.Contact != null)
        {
            await SendToWebApp(new { contact = update.Message.Contact.PhoneNumber });

            string lang = userLanguages.ContainsKey(chatId) ? userLanguages[chatId] : "uz";
            string branchMessage = lang == "uz" ? "🌍 Filialni tanlang:" : "🌍 Выберите филиал:";
            await botClient.SendTextMessageAsync(chatId, branchMessage, replyMarkup: BranchKeyboard(lang));
        }
        else if (update.Message.Type == MessageType.Document && update.Message.Document != null)
        {
            string fileId = update.Message.Document.FileId;
            var file = await botClient.GetFileAsync(fileId);
            string filePath = $"https://api.telegram.org/file/bot{TelegramToken}/{file.FilePath}";
            string driveLink = await UploadToDrive(filePath);

            await SendToWebApp(new { file = driveLink });

            string lang = userLanguages.ContainsKey(chatId) ? userLanguages[chatId] : "uz";
            string successMessage = lang == "uz" ? "✅ Rezume saqlandi! Rahmat." : "✅ Резюме сохранено! Спасибо.";
            await botClient.SendTextMessageAsync(chatId, successMessage);
        }
    }

    private static async Task SendToWebApp(object data)
    {
        using (var client = new HttpClient())
        {
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await client.PostAsync(WebAppUrl, content);
        }
    }

    private static async Task<string> UploadToDrive(string fileUrl)
    {
        using (var client = new HttpClient())
        {
            var data = new { file = fileUrl };
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(WebAppUrl, content);
            return await response.Content.ReadAsStringAsync();
        }
    }

    private static IReplyMarkup LanguageKeyboard() =>
        new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton("O'zbekcha 🇺🇿"),
            new KeyboardButton("Русский 🇷🇺")
        })
        { ResizeKeyboard = true };

    private static IReplyMarkup ContactKeyboard(string lang) =>
        new ReplyKeyboardMarkup(new KeyboardButton(lang == "uz" ? "📞 Kontakt yuborish" : "📞 Отправить контакт") { RequestContact = true }) { ResizeKeyboard = true };

    private static IReplyMarkup BranchKeyboard(string lang) =>
        new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton(lang == "uz" ? "Toshkent" : "Ташкент"),
            new KeyboardButton(lang == "uz" ? "Samarqand" : "Самарканд")
        })
        { ResizeKeyboard = true };

    private static IReplyMarkup SchoolKindKeyboard(string lang) =>
        new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton(lang == "uz" ? "Maktab 🏫" : "Школа 🏫"),
            new KeyboardButton(lang == "uz" ? "Bog‘cha 🏡" : "Детский сад 🏡")
        })
        { ResizeKeyboard = true };


    private static async Task<string> GetJobInfoFromSheet(string position)
    {
        using (var client = new HttpClient())
        {
            var response = await client.GetStringAsync($"{WebAppUrl}?sheet=MALUMOT&position={position}");
            return string.IsNullOrEmpty(response) ? "Ma'lumot topilmadi" : response;
        }


    }
    private static async Task<IReplyMarkup> JobVacanciesKeyboard()
    {
        using (var client = new HttpClient())
        {
            var response = await client.GetStringAsync(WebAppUrl + "?sheet=NASTROYKA");
            var jobList = JsonConvert.DeserializeObject<List<string>>(response);

            var buttons = new List<KeyboardButton[]>();
            foreach (var job in jobList)
            {
                buttons.Add(new KeyboardButton[] { new KeyboardButton(job) });
            }

            return new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true };
        }
    }
}
