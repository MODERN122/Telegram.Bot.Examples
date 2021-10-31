using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Examples.Polling;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace Telegram.Bot.Examples.Echo
{
    public class Handlers
    {
        CancellationTokenSource cancellationToken = new CancellationTokenSource();
        Message lastMessage;
        private Guid currentBirthdayId;
        public static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var handler = update.Type switch
            {
                // UpdateType.Unknown:
                // UpdateType.ChannelPost:
                // UpdateType.EditedChannelPost:
                //UpdateType.ShippingQuery
                // UpdateType.PreCheckoutQuery:
                // UpdateType.Poll:
                UpdateType.Message => BotOnMessageReceived(botClient, update.Message),
                UpdateType.EditedMessage => BotOnMessageReceived(botClient, update.EditedMessage),
                UpdateType.CallbackQuery => BotOnCallbackQueryReceived(botClient, update.CallbackQuery),
                UpdateType.InlineQuery => BotOnInlineQueryReceived(botClient, update.InlineQuery),
                UpdateType.ChosenInlineResult => BotOnChosenInlineResultReceived(botClient, update.ChosenInlineResult),
                _ => UnknownUpdateHandlerAsync(botClient, update)
            };

            try
            {
                await handler;
            }
            catch (Exception exception)
            {
                await HandleErrorAsync(botClient, exception, cancellationToken);
            }
        }

        private async Task BotOnMessageReceived(ITelegramBotClient botClient, Message message)
        {
            Console.WriteLine($"Receive message type: {message.Type}");
            if (message.Type != MessageType.Text)
                return;

            var action = (message.Text.Split(' ').First()) switch
            {
                "/add" => AddBirthday(botClient, message),
                "/all" => GetAllBirthdays(botClient, message),
                "/last" => GetLastAddedBirthday(botClient, message),
                _ => Usage(botClient, message)
            };
            var sentMessage = await action;
            Console.WriteLine($"The message was sent with id: {sentMessage.MessageId}");

            async Task<Message> AddBirthday(ITelegramBotClient botClient, Message message)
            {
                using var db = new BirthdayContext();
                cancellationToken = new CancellationTokenSource();

                db.Database.EnsureCreated();
                // Note: This sample requires the database to be created before running.
                Console.WriteLine($"Database path: {db.DbPath}.");
                var text = message.Text;
                var fioRegex = new Regex(@"-n\s([А-ЯA-Zа-яa-z]+\s?[А-ЯA-Zа-яa-z]+\s?[А-ЯA-Zа-яa-z]+)");

                var fioMatch = fioRegex.Match(text);
                if (!fioMatch.Success)
                {
                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                                text: "ФИО не было найдено в тексте");
                }
                var fio = fioMatch.Groups.Values.ToArray()[1].Value;
                Regex dateRegex = new Regex(@"-d\s(([0-9]{1,2}).([0-9]{1,2}).([0-9]{4}))");
                var dateMatch = dateRegex.Match(text);
                if (!dateMatch.Success)
                {
                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                                text: "Даты рождения не найдено в тексте");
                }
                DateTimeOffset dateTime;
                var parseResult = DateTimeOffset.TryParseExact(dateMatch.Groups.Values.ToArray()[1].Value, "dd.MM.yyyy",
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None, out dateTime);
                if (!parseResult)
                {
                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                                text: "Введите корректную дату рождения");
                }
                var birthday = new Birthday(dateTime, message.From.Id, fio, message.Chat.Id);
                currentBirthdayId = birthday.Id;
                db.Add(birthday);
                db.SaveChanges();

                var inlineKeyboardMarkup = new InlineKeyboardMarkup(
                    new InlineKeyboardButton() { Text = "Пропустить", CallbackData = "skipNickname" }
                    );
                var result = await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: "Введите никнейм/кличку вашего знакомого или нажмите Пропустить", replyMarkup: inlineKeyboardMarkup);
                lastMessage = message;
                if (result != null)
                {
                    await TelegramBotClientPollingExtensions.ReceiveAsync(botClient, new DefaultUpdateHandler(HandleUpdate1Async, HandleErrorAsync), cancellationToken.Token);
                }
                return result;
            }

            async Task<Message> Usage(ITelegramBotClient botClient, Message message)
            {
                const string usage = "Usage:\n" +
                                     "/add -d дд.мм.гггг -n Фамилия Имя Отчество(если есть) - добавить день рождения знакомого\n" +
                                     "/all - вывести все напоминания о днях рождения";

                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: usage,
                                                            replyMarkup: new ReplyKeyboardRemove());
            }

            async Task<Message> GetAllBirthdays(ITelegramBotClient botClient, Message message)
            {

                using var context = new BirthdayContext();
                if (context.Birthdays == null)
                {
                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                                text: "Нет дней рождений",
                                                                replyMarkup: new ReplyKeyboardRemove());
                }
                var birthdays = await context.Birthdays.Where(x => x.TelegramId == message.From.Id).ToListAsync();

                var inlineKeyboards = new List<List<InlineKeyboardButton>>();
                foreach (var birthday in birthdays)
                {
                    inlineKeyboards.Add(new List<InlineKeyboardButton>() { new InlineKeyboardButton()
                    {
                        Text =  $"{birthday.Fio.Split(" ")[1]} " +
                                $"{birthday.NickName} " +
                                $"{birthday.BirthDate.ToString("dd.MM.yy")} ",

                        CallbackData = "openBirthday" +"|"+ birthday.Id.ToString(),
                    }});
                }
                return
                    await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                                text: "Все напоминания о днях рождения выведены",
                                                                replyMarkup: new InlineKeyboardMarkup(inlineKeyboards));
            }
        }

        private async Task<Message> GetLastAddedBirthday(ITelegramBotClient botClient, Message message)
        {
            throw new NotImplementedException();
        }

        private async Task HandleUpdate1Async(ITelegramBotClient botClient, Update update, CancellationToken arg3)
        {
            if (update.Message != null && update.Message.MessageId != lastMessage.MessageId)
            {
                using var context = new BirthdayContext();
                var birthday = await context.Birthdays.FirstOrDefaultAsync(x => x.Id == currentBirthdayId);
                if (birthday == null)
                {
                    await botClient.SendTextMessageAsync(chatId: update.Message.Chat.Id,
                                                                text: "Возникла ошибка, попробуйте добавить новый день рождения",
                                                                replyMarkup: new ReplyKeyboardRemove());
                    cancellationToken.Cancel();
                    return;
                }
                birthday.NickName = update.Message.Text;
                await context.SaveChangesAsync();
                lastMessage = update.Message;
            }
            else if (update.CallbackQuery != null)
            {
                if (update.CallbackQuery.Data != "skipNickname")
                {
                    return;
                }
            }
            else
            {
                return;
            }
            var inlineKeyboardMarkup = new InlineKeyboardMarkup(
                new InlineKeyboardButton() { Text = "Пропустить", CallbackData = "skipCity" }
                );
            cancellationToken.Cancel();
            cancellationToken = new CancellationTokenSource();
            var result = await botClient.SendTextMessageAsync(chatId: update.Message?.Chat.Id ?? update.CallbackQuery.Message!.Chat.Id,
                                                        text: "Введите город проживания или нажмите Пропустить", replyMarkup: inlineKeyboardMarkup);
            if (result != null)
            {
                await TelegramBotClientPollingExtensions.ReceiveAsync(botClient, new DefaultUpdateHandler(HandleUpdate2Async, HandleErrorAsync), cancellationToken.Token);
            }
        }

        private async Task HandleUpdate2Async(ITelegramBotClient botClient, Update update, CancellationToken arg3)
        {
            if (update.Message != null && update.Message.MessageId != lastMessage.MessageId)
            {
                using var context = new BirthdayContext();
                var birthday = await context.Birthdays.FirstOrDefaultAsync(x => x.Id == currentBirthdayId);
                if (birthday == null)
                {
                    await botClient.SendTextMessageAsync(chatId: update.Message.Chat.Id,
                                                                text: "Возникла ошибка, попробуйте добавить новый день рождения",
                                                                replyMarkup: new ReplyKeyboardRemove());
                    cancellationToken.Cancel();
                    return;
                }
                birthday.City = update.Message.Text;
                await context.SaveChangesAsync();
                lastMessage = update.Message;
            }
            else if (update.CallbackQuery != null)
            {
                if (update.CallbackQuery.Data != "skipCity")
                {
                    return;
                }
            }
            else
            {
                return;
            }
            var inlineKeyboardMarkup = new InlineKeyboardMarkup(
                new InlineKeyboardButton() { Text = "Пропустить", CallbackData = "skipAddress" }
                );
            cancellationToken.Cancel();
            cancellationToken = new CancellationTokenSource();
            var result = await botClient.SendTextMessageAsync(chatId: update.Message?.Chat.Id ?? update.CallbackQuery.Message!.Chat.Id,
                                                        text: "Введите адрес проживания или нажмите Пропустить", replyMarkup: inlineKeyboardMarkup);
            if (result != null)
            {
                await TelegramBotClientPollingExtensions.ReceiveAsync(botClient, new DefaultUpdateHandler(HandleUpdate3Async, HandleErrorAsync), cancellationToken.Token);
            }
        }

        private async Task HandleUpdate3Async(ITelegramBotClient arg1, Update arg2, CancellationToken arg3)
        {
            cancellationToken.Cancel();
        }

        // Process Inline Keyboard callback data
        private async Task BotOnCallbackQueryReceived(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            await botClient.AnswerCallbackQueryAsync(
                callbackQueryId: callbackQuery.Id,
                text: $"Received {callbackQuery.Data}");
            var splits = callbackQuery.Data.Split("|");
            if (splits.First() == "openBirthday")
            {
                using var context = new BirthdayContext();
                Guid id;
                if (!Guid.TryParse(splits[1], out id))
                {
                    return;
                }
                var birthday = await context.Birthdays.FirstOrDefaultAsync(x => x.Id == id);
                if (birthday == null)
                {
                    await botClient.SendTextMessageAsync(
                chatId: callbackQuery.Message.Chat.Id,
                text: $"Возникла ошибка при поиске напоминания");
                    return;
                }
                var inlineKeyboardMarkup = new InlineKeyboardMarkup(
                  new List<List<InlineKeyboardButton>>() {
                      new List<InlineKeyboardButton>() { new InlineKeyboardButton() { Text = "Фио:"+ birthday.Fio, CallbackData = "fio"+"|"+splits[1] }, },
                      new List<InlineKeyboardButton>() { new InlineKeyboardButton() { Text = "Никнейм:"+ birthday.NickName, CallbackData = "nick" + "|" + splits[1] },new InlineKeyboardButton() { Text = "Дата:"+ birthday.BirthDate.ToString("dd.MM.yy"), CallbackData = "date" + "|" + splits[1] }, },
                      new List<InlineKeyboardButton>() { new InlineKeyboardButton() { Text = "Город:"+ birthday.City, CallbackData = "city" + "|" + splits[1] }, new InlineKeyboardButton() { Text = "Адрес:"+ birthday.StreetAddress, CallbackData = "address" + "|" + splits[1] },},
                      new List<InlineKeyboardButton>() {new InlineKeyboardButton() { Text = "Напоминания", CallbackData = "reminders" + "|" + splits[1] }, },
                  }
                );
                var result = await botClient.SendTextMessageAsync(chatId: callbackQuery.Message!.Chat.Id,
                                                            text: "Выберите какое значение хотите изменить", replyMarkup: inlineKeyboardMarkup);

            }
        }

        private static async Task BotOnInlineQueryReceived(ITelegramBotClient botClient, InlineQuery inlineQuery)
        {
            Console.WriteLine($"Received inline query from: {inlineQuery.From.Id}");

            InlineQueryResultBase[] results = {
                // displayed result
                new InlineQueryResultArticle(
                    id: "3",
                    title: "TgBots",
                    inputMessageContent: new InputTextMessageContent(
                        "hello"
                    )
                )
            };

            await botClient.AnswerInlineQueryAsync(
                inlineQueryId: inlineQuery.Id,
                results: results,
                isPersonal: true,
                cacheTime: 0);
        }

        private static Task BotOnChosenInlineResultReceived(ITelegramBotClient botClient, ChosenInlineResult chosenInlineResult)
        {
            Console.WriteLine($"Received inline result: {chosenInlineResult.ResultId}");
            return Task.CompletedTask;
        }

        private static Task UnknownUpdateHandlerAsync(ITelegramBotClient botClient, Update update)
        {
            Console.WriteLine($"Unknown update type: {update.Type}");
            return Task.CompletedTask;
        }
    }
}
