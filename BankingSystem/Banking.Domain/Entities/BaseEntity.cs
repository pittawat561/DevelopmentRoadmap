using System;
using System.Collections.Generic;
using System.Text;

namespace Banking.Domain.Entities
{
    public class BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdateAt { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
