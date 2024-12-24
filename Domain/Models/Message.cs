﻿using System;
using System.Collections.Generic;

namespace Domain.Models
{
    public partial class Message
    {
        public int Id { get; set; }
        public int? SenderId { get; set; }
        public int? ReceiverId { get; set; }
        public string? Content { get; set; }
        public bool? ReadStatus { get; set; }
        public DateTime? CreatedAt { get; set; }

        public virtual User? Receiver { get; set; }
        public virtual User? Sender { get; set; }
    }
}
