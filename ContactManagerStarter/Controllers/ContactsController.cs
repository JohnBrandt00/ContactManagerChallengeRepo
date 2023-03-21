using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ContactManager.Data;
using ContactManager.Hubs;
using ContactManager.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MailKit;
using MimeKit;
using MailKit.Net.Smtp;
using ElmahCore;

namespace ContactManager.Controllers
{
    public class ContactsController : Controller
    {
        private readonly ApplicationContext _context;
        private readonly IHubContext<ContactHub> _hubContext;
        private readonly ILogger<ContactsController> _logger;

        public ContactsController(ApplicationContext context, IHubContext<ContactHub> hubContext, ILogger<ContactsController> logger )
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<IActionResult> DeleteContact(Guid id)
        {
            var contactToDelete = await _context.Contacts
                .Include(x => x.EmailAddresses)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (contactToDelete == null)
            {
                return BadRequest();
            }

            _context.EmailAddresses.RemoveRange(contactToDelete.EmailAddresses);
            _context.Contacts.Remove(contactToDelete);

            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("Update");

            return Ok();
        }

        public async Task<IActionResult> EditContact(Guid id)
        {
            var contact = await _context.Contacts
                .Include(x => x.EmailAddresses)
                .Include(x => x.Addresses)
                .FirstOrDefaultAsync(x => x.Id == id);


            if (contact == null)
            {
                _logger.LogError($"[Logger] {DateTime.Now}: No contact found for guid {id}");
                return NotFound();
            }

            if (contact.EmailAddresses.Count > 0)
            {
               

                if (contact.EmailAddresses.Where(i => i.IsPrimary != null).ToList().Count <= 0)
                {
                    if (contact.EmailAddresses.First() != null)
                        contact.EmailAddresses.First().IsPrimary = true;
                    _logger.LogInformation($"[Logger] {DateTime.Now}: No primary email found, setting");
                }
            }

            var viewModel = new EditContactViewModel
            {
                Id = contact.Id,
                Title = contact.Title,
                FirstName = contact.FirstName,
                LastName = contact.LastName,
                DOB = contact.DOB,
                EmailAddresses = contact.EmailAddresses,
                Addresses = contact.Addresses
            };

            return PartialView("_EditContact", viewModel);
        }

        public async Task<IActionResult> GetContacts()
        {
            var contactList = await _context.Contacts
                .OrderBy(x => x.FirstName)
                .ToListAsync();
          
            return PartialView("_ContactTable", new ContactViewModel { Contacts = contactList });
        }

        public IActionResult Index()
            {
            HttpContext.RaiseError(new InvalidOperationException("Test"));
            //logging
            _logger.LogInformation($"[Logger] {DateTime.Now}: Reached Index");
                return View();
            }

        public IActionResult NewContact()
        {
            _logger.LogInformation($"[Logger] {DateTime.Now}: New Contact");
            return PartialView("_EditContact", new EditContactViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> SaveContact([FromBody]SaveContactViewModel model)
        {
            // error handling invalid model
            if( model == null)
            {
                _logger.LogError($"[Logger] {DateTime.Now}: Model is invalid!");
                return NotFound();
            }

            var contact = model.ContactId == Guid.Empty
                ? new Contact { Title = model.Title, FirstName = model.FirstName, LastName = model.LastName, DOB = model.DOB }
                : await _context.Contacts.Include(x => x.EmailAddresses).Include(x => x.Addresses).FirstOrDefaultAsync(x => x.Id == model.ContactId);

            if (contact == null)
            {
                _logger.LogError($"[Logger] {DateTime.Now}: Contact not found!");
                return NotFound();
            }

            _context.EmailAddresses.RemoveRange(contact.EmailAddresses);
            _context.Addresses.RemoveRange(contact.Addresses);


            foreach (var email in model.Emails)
            {
                contact.EmailAddresses.Add(new EmailAddress
                {
                    Type = email.Type,
                    Email = email.Email,
                    IsPrimary = email.IsPrimary,
                    Contact = contact
                });
            }

            foreach (var address in model.Addresses)
            {
                contact.Addresses.Add(new Address
                {
                    Street1 = address.Street1,
                    Street2 = address.Street2,
                    City = address.City,
                    State = address.State,
                    Zip = address.Zip,
                    Type = address.Type
                });
            }

            contact.Title = model.Title;
            contact.FirstName = model.FirstName;
            contact.LastName = model.LastName;
            contact.DOB = model.DOB;

            if (model.ContactId == Guid.Empty)
            {
                await _context.Contacts.AddAsync(contact);
            }
            else
            {
                _context.Contacts.Update(contact);
            }


            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("Update");

            // this throws error that elmah catches
            SendEmailNotification(contact.Id);

            return Ok();
        }

        private void SendEmailNotification(Guid contactId)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress("noreply", "noreply@contactmanager.com"));
            message.To.Add(new MailboxAddress("SysAdmin", "Admin@contactmanager.com"));
            message.Subject = "ContactManager System Alert";

            message.Body = new TextPart("plain")
            {
                Text = "Contact with id:" + contactId.ToString() +" was updated"
            };

            using (var client = new SmtpClient())
            {
                // For demo-purposes, accept all SSL certificates (in case the server supports STARTTLS)
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                client.Connect("127.0.0.1", 25, false);

                client.Send(message);
                client.Disconnect(true);
            }

        }

    }

}