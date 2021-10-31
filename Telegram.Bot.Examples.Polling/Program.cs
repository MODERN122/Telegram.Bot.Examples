using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Impl;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Examples.Polling;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types.ReplyMarkups;

namespace Telegram.Bot.Examples.Echo
{
    public static class Program
    {
        private static TelegramBotClient? Bot;

        public static async Task Main()
        {
            Bot = new TelegramBotClient(Configuration.BotToken);

            var me = await Bot.GetMeAsync();
            Console.Title = me.Username;// Get the service on the local machine

            using var cts = new CancellationTokenSource();
            Start(cts.Token);
            var hr = new Handlers();
            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            Bot.StartReceiving(new DefaultUpdateHandler(hr.HandleUpdateAsync, Handlers.HandleErrorAsync),
                               cts.Token);

            Console.WriteLine($"Start listening for @{me.Username}");
            Console.ReadLine();

            // Send cancellation request to stop bot
            cts.Cancel();
        }

        public static async Task Start(CancellationToken cancellationToken)
        {
            IScheduler scheduler = await StdSchedulerFactory.GetDefaultScheduler();
            await scheduler.Start(cancellationToken);

            IJobDetail job = JobBuilder.Create<RemindersSender>().Build();

            ITrigger trigger = TriggerBuilder.Create()  // создаем триггер
                .WithIdentity("trigger1", "group1")     // идентифицируем триггер с именем и группой
                .StartNow()                            // запуск сразу после начала выполнения
                .WithSimpleSchedule(x => x            // настраиваем выполнение действия
                    .WithIntervalInMinutes(1)          // через 1 минуту
                    .RepeatForever())                   // бесконечное повторение
                .Build();                               // создаем триггер

            await scheduler.ScheduleJob(job, trigger, cancellationToken);        // начинаем выполнение работы
        }
    }

    public class RemindersSender : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var day = DateTimeOffset.Now.DayOfYear;
            using (var dbContext = new BirthdayContext())
            {
                var botClient = new TelegramBotClient(Configuration.BotToken);
                var me = await botClient.GetMeAsync();
                if (me != null)
                {

                }
                var birthdays = await dbContext.Birthdays.Where(x=>x.DayOfYear==day).ToListAsync();
                if (birthdays != null)
                {
                    foreach (var birthday in birthdays)
                    {
                        try
                        {
                            await botClient.SendTextMessageAsync(chatId: birthday.ChatId,
                                text: $"Напоминаю, что {birthday.NickName ?? birthday.Fio} сегодня отмечает день рождения, не забудь поздравить!!!"
                                , replyMarkup: new ReplyKeyboardRemove());

                        }
                        catch(Exception ex)
                        {

                        }
}
                }
            }
            using (var dbContext = new BirthdayContext())
            {
                var botClient = new TelegramBotClient(Configuration.BotToken);
                var reminders = await (dbContext.Reminders.Where(x => x.DayOfYear == day)).ToListAsync();
                if (reminders == null)
                {
                    return;
                }
                foreach (var reminder in reminders)
                {
                    await botClient.SendTextMessageAsync(chatId: reminder.Birthday.ChatId,
                        text: $"Напоминаю, что у {reminder.Birthday.NickName ?? reminder.Birthday.Fio} {reminder.Birthday.BirthDate.ToString("dd.MM") }",
                        replyMarkup: new ReplyKeyboardRemove());

                }
            }
        }
    }
}
