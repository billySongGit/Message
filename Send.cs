using Com.Clout2.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Diagnostics;
using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.Extensions.Options;

namespace Com.Clout2.Api.Controllers
{
    /// <summary>
    /// Messages RESTful API Controller (ASP.NET Core)
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    public class MessageApiController : MessageBaseApiController
    {
        private SendGridOptions _sendGridOptions;

        /// <summary>
        /// Message API Controller
        /// </summary>
        /// <param name="apiDataContext">DbContext for EntityFramework</param>
        /// <param name="logger">Injected ILoggerFactory</param>
        /// <param name="_sendGridOptions"></param>
        public MessageApiController(ApiDataContext apiDataContext, ILoggerFactory logger, IOptions<SendGridOptions> _sendGridOptions) : base(apiDataContext, logger)
        {
            this._sendGridOptions = _sendGridOptions.Value;
        }

        // *************************************************************************************** //
        // Customized methods                                                                      //
        // *************************************************************************************** //

        /// <summary>
        /// Send or resend a message
        /// </summary>
        /// <param name="loginToken">The token for the user requesting this data. 
        ///	 If provided, the system verifies the user rights to access the data</param>
        /// <param name="id">the Id of the message to be sent</param> 
        /// <response code="200">successful operation</response> 
        /// <response code="404">Category object not found</response> 
        [HttpPut]
        [Route("/message/send")]
        [ProducesResponseType(typeof(Message), 200)]
        [ProducesResponseType(typeof(IDictionary<string, string>), 400)]
        public virtual async Task<IActionResult> SendMessageAsync(
            [FromQuery]string loginToken,
            [FromQuery]long? id)
        {
            var loggedInUser = _tokenizer.ValidateToken(loginToken);
            if (loggedInUser == null) return BadRequest("Invalid Login Token");

            var body = _dbContext.Messages
                .Include("FromUser")
                .Include("ToUser")
                .Include("ToEmail")
                .Include("ToPhone")
                .Include("MessageTemplate")
                .FirstOrDefault(m => m.Id == id);
            if (body == null) return NotFound();

            await SendMessageAsync(id);

            return new OkObjectResult(body);
        }

        private async Task SendMessageAsync(long? messageId)
        {
            //Reaccess from data store to make sure we have all data
            var Message = _dbContext.Messages
                .SingleOrDefault(m => m.Id == messageId);

            _dbContext.Entry(Message).Reference(m => m.FromUser).Load();
            _dbContext.Entry(Message).Reference(m => m.ToUser).Load();
            _dbContext.Entry(Message).Reference(m => m.ToPhone).Load();
            _dbContext.Entry(Message).Reference(m => m.ToEmail).Load();
            _dbContext.Entry(Message).Reference(m => m.MessageTemplate).Load();
            _dbContext.Entry(Message.MessageTemplate).Collection(m => m.MessageTemplateFields).Load();

            Dictionary<string, string> fields = JsonConvert.DeserializeObject<Dictionary<string, string>>(Message.Fields);


            if (Message.SendEmail == "Y")
            {
                //// Format email from template
                //var emailMessage = new MimeMessage();

                //emailMessage.From.Add(new MailboxAddress(Message.FromUser.FirstName + " " + Message.FromUser.LastName, _smtpOptions.smtpFrom));
                //emailMessage.To.Add(new MailboxAddress(Message.ToUser.FirstName + " " + Message.ToUser.LastName, Message.ToEmail.EmailAddress));

                FillTemplate(Message, fields, out string emailSubject, out string htmlBody, out string textBody);

                //emailMessage.Subject = emailSubject;
                //var bodyBuilder = new BodyBuilder
                //{
                //    HtmlBody = htmlBody,
                //    TextBody = textBody
                //};
                //emailMessage.Body = bodyBuilder.ToMessageBody();

                //Int32.TryParse(_smtpOptions.smtpPort, out int port);

                //// Send the email through configured SMTP Server (probably AWS SES)
                //using (var smtpClient = new SmtpClient())
                //{
                //    smtpClient.LocalDomain = "clout.com";
                //    smtpClient.Connect(_smtpOptions.smtpHost, port, SecureSocketOptions.Auto);
                //    smtpClient.Authenticate(_smtpOptions.smtpUser, _smtpOptions.smtpPass);
                //    smtpClient.Send(emailMessage);
                //    smtpClient.Disconnect(true);
                //}
                //moving to SendGrid. commenting out AWS SES

                //TODO sendgrid here
                //Send Emails through SendGrid

                var client = new SendGridClient(_sendGridOptions.ApiKey);

                string regexFromFirst = Regex.Replace(Message.FromUser.FirstName, @"(@|&|‘|\(|\)|<|>|#|;|:|,|{|}|[|]|\^|%|\$|\!|\?|/)", "");
                string regexFromLast = Regex.Replace(Message.FromUser.LastName, @"(@|&|‘|\(|\)|<|>|#|;|:|,|{|}|[|]|\^|%|\$|\!|\?|/)", "");

                var msg = new SendGridMessage()
                {

                    From = new EmailAddress(_sendGridOptions.From, String.Format("{0} {1}", regexFromFirst, regexFromLast)),
                    Subject = emailSubject,
                    PlainTextContent = textBody,
                    HtmlContent = htmlBody
                };

                string regexToFirst = Regex.Replace(Message.ToUser.FirstName, @"(@|&|‘|\(|\)|<|>|#|;|:|,|{|}|[|]|\^|%|\$|\!|\?|/)", "");
                string regexToLast = Regex.Replace(Message.ToUser.LastName, @"(@|&|‘|\(|\)|<|>|#|;|:|,|{|}|[|]|\^|%|\$|\!|\?|/)", "");

                msg.AddTo(new EmailAddress(Message.ToEmail.EmailAddress, String.Format("{0} {1}", regexToFirst, regexToLast)));

                var response = await client.SendEmailAsync(msg);


                // Update EmailSent
                Message.EmailSent = DateTime.UtcNow;
                _dbContext.SaveChanges();
            }

            if (Message.SendText == "Y")
            {
                //// Format email from template
                //var emailMessage = new MimeMessage();

                var Provider = _dbContext.Providers
                    .Where(m => m.Id == Message.ToPhone.ProviderId)
                    .SingleOrDefault();

                //phoneNumber = Regex.Replace(Message.Phone.Telephone, @"[^\d]", "");
                string number = new string(Message.ToPhone.Telephone.Where(char.IsDigit).ToArray());
                Debug.WriteLine("formatted Number = " + number);
                Debug.WriteLine("sending to SMS ...");
                Debug.WriteLine(number + "@" + Provider.SmsEmailDomain);

                //emailMessage.From.Add(new MailboxAddress(Message.FromUser.FirstName + " " + Message.FromUser.LastName, _smtpOptions.smtpFrom));
                //emailMessage.To.Add(new MailboxAddress(number + "@" + Provider.SmsEmailDomain));
                var smsBody = Message.MessageTemplate.SmsBody;

                foreach (var templateField in Message.MessageTemplate.MessageTemplateFields)
                {
                    if (fields.Keys.Contains(templateField.FieldName))
                    {
                        smsBody = smsBody.Replace(templateField.Pattern, fields[templateField.FieldName]);
                    }
                    else
                    {
                        smsBody = smsBody.Replace(templateField.Pattern, templateField.DefaultValue);
                    }
                }

                //Send Text through SendGrid
                var client = new SendGridClient(_sendGridOptions.ApiKey);
                var from = new EmailAddress("admin@clout.com");

                string regexToFirst = Regex.Replace(Message.ToUser.FirstName, @"(@|&|‘|\(|\)|<|>|#|;|:|,|{|}|[|]|\^|%|\$|\!|\?|/)", "");
                string regexToLast = Regex.Replace(Message.ToUser.LastName, @"(@|&|‘|\(|\)|<|>|#|;|:|,|{|}|[|]|\^|%|\$|\!|\?|/)", "");

                var subject = String.Format("Sent to {0} {1}", regexToFirst, regexToLast);

                var msg = new SendGridMessage()
                {
                    From = from,
                    Subject = subject,
                    PlainTextContent = smsBody,
                    HtmlContent = smsBody
                };

                msg.AddTo(new EmailAddress(number + "@" + Provider.SmsEmailDomain));
                var response = await client.SendEmailAsync(msg);


                // Update SmsSent
                Message.SmsSent = DateTime.UtcNow;
                _dbContext.SaveChanges();
            }
            // return Ok("successful Operation");
        }

        /// <summary>
        /// Send or resend multiple message
        /// </summary>
        /// <param name="loginToken">The token for the user requesting this data. 
        ///	 If provided, the system verifies the user rights to access the data</param>
        /// <param name="body">Message object that needs to be added to the datastore</param>
        /// <param name="email">Email of the referred person</param>
        /// <response code="200">successful operation</response> 
        /// <response code="404">Category object not found</response> 
        [HttpPost]
        [Route("/message/send/referral")]
        [ProducesResponseType(typeof(Message), 200)]
        [ProducesResponseType(typeof(IDictionary<string, string>), 400)]
        public virtual async Task<IActionResult> SendMessageReferral(
            [FromQuery]string loginToken,
            [FromBody]Message body,
            [FromQuery]string email)
        {
            body.TemplateId = 4; // referral_email
            IActionResult result = AddMessage(loginToken, body);
            if (result.GetType() == typeof(OkObjectResult))
            {
                OkObjectResult t = (OkObjectResult)result;
                body = (Message) t.Value;
                await SendMessageReferralAsync(body.Id, email);
            }

            return result;
        }

        private async Task SendMessageReferralAsync(long? messageId, string email)
        {
            var Message = _dbContext.Messages
                .SingleOrDefault(m => m.Id == messageId);

            _dbContext.Entry(Message).Reference(m => m.FromUser).Load();
            _dbContext.Entry(Message).Reference(m => m.MessageTemplate).Load();
            _dbContext.Entry(Message.MessageTemplate).Collection(m => m.MessageTemplateFields).Load();

            Dictionary<string, string> fields = JsonConvert.DeserializeObject<Dictionary<string, string>>(Message.Fields);

            FillTemplate(Message, fields, out string emailSubject, out string htmlBody, out string textBody);

            var client = new SendGridClient(_sendGridOptions.ApiKey);

            string regexFromFirst = Regex.Replace(Message.FromUser.FirstName, @"(@|&|‘|\(|\)|<|>|#|;|:|,|{|}|[|]|\^|%|\$|\!|\?|/)", "");
            string regexFromLast = Regex.Replace(Message.FromUser.LastName, @"(@|&|‘|\(|\)|<|>|#|;|:|,|{|}|[|]|\^|%|\$|\!|\?|/)", "");

            var msg = new SendGridMessage()
            {
                From = new EmailAddress(_sendGridOptions.From, String.Format("{0} {1}", regexFromFirst, regexFromLast)),
                Subject = emailSubject,
                PlainTextContent = textBody,
                HtmlContent = htmlBody
            };

            msg.AddTo(new EmailAddress(email));

            var response = await client.SendEmailAsync(msg);

            // Update EmailSent
            Message.EmailSent = DateTime.UtcNow;
            _dbContext.SaveChanges();
        }

        private static void FillTemplate(Message Message, Dictionary<string, string> fields, out string emailSubject, out string htmlBody, out string textBody)
        {
            emailSubject = Message.MessageTemplate.EmailSubject;
            htmlBody = Message.MessageTemplate.HtmlBody;
            textBody = Message.MessageTemplate.TextBody;
            foreach (var templateField in Message.MessageTemplate.MessageTemplateFields)
            {
                if (fields.Keys.Contains(templateField.FieldName))
                {
                    emailSubject = emailSubject.Replace(templateField.Pattern, fields[templateField.FieldName]);
                    htmlBody = htmlBody.Replace(templateField.Pattern, fields[templateField.FieldName]);
                    textBody = textBody.Replace(templateField.Pattern, fields[templateField.FieldName]);
                }
                else
                {
                    emailSubject = emailSubject.Replace(templateField.Pattern, templateField.DefaultValue);
                    htmlBody = htmlBody.Replace(templateField.Pattern, templateField.DefaultValue);
                    textBody = textBody.Replace(templateField.Pattern, templateField.DefaultValue);
                }
            }
        }

    } // End of Class MessageApiController
}
