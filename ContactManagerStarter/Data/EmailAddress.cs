using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace ContactManager.Data
{
    public class EmailAddress : Entity
    {
        public string Email { get; set; }
        public EmailType Type { get; set; }

        public bool? IsPrimary { get; set; }
        public virtual Contact Contact { get; set; }
    }
}
