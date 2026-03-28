using System;
using System.Collections.Generic;
using System.Text;

namespace Banking.Domain.Entities
{
    internal class BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
    }
}
