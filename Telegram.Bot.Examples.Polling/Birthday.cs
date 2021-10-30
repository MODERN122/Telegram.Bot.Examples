using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Bot.Examples.Polling
{
    public class Birthday
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTimeOffset Birthdate { get; set; }
        //public List<DateTimeOffset>? RemindTimes { get; set; }
        public long TelegramId { get; set; }

        public string? NickName { get; set; }
        public string Fio { get; set; }
        public int MessageId { get; private set; }
        public string? PhoneNumber { get; set; }

        public string? PostCode { get; set; }
        public string? StreetAddress { get; set; }
        public string? City { get; set; }
        public Birthday(DateTimeOffset birthdate, long telegramId, string fio, int messageId)
        {
            Birthdate = birthdate;
            TelegramId = telegramId;
            Fio = fio;
            MessageId = messageId;
        }
    }
    public enum RemindTime
    {
        Hour, Day, Week, Month
    }
}
