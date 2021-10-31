using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Bot.Examples.Polling
{
    public class Birthday
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTimeOffset BirthDate { get; set; }
        public int DayOfYear { get; set; }
        public List<Reminder>? Reminders { get; set; }
        public long TelegramId { get; set; }

        public string? NickName { get; set; }
        public string Fio { get; set; }
        public long ChatId { get; private set; }
        public string? PhoneNumber { get; set; }

        public string? PostCode { get; set; }
        public string? StreetAddress { get; set; }
        public string? City { get; set; }
        public Birthday(DateTimeOffset birthDate, long telegramId, string fio, long chatId)
        {
            BirthDate = birthDate;
            DayOfYear = birthDate.DayOfYear;
            TelegramId = telegramId;
            Fio = fio;
            ChatId = chatId;
        }
    }
    public enum RemindTime
    {
        Hour, Day, Week, Month
    }
    public class Reminder
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public Birthday? Birthday { get; set; }
        public Guid BirthdayId { get; set; }
        public DateTimeOffset RemindDate { get; set; }
        public int DayOfYear { get; set; }
        public Reminder(Guid birthdayId, DateTimeOffset remindDate)
        {
            BirthdayId = birthdayId;
            RemindDate = remindDate;
            DayOfYear = remindDate.DayOfYear;
        }
    }
}
