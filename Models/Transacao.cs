using System;

namespace FinanceBot.Models;

public class Transacao
{
        public Guid Id { get; set; }
        public string Descricao { get; set; }
        public decimal Valor { get; set; }
        public DateTime Data { get; set; }
}
