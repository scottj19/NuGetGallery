// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Hosting;
using AnglicanGeek.MarkdownMailer;
using Elmah;
using NuGetGallery.Configuration;

namespace NuGetGallery.Services
{
    public class BackgroundMessageService : MessageService
    {
        public BackgroundMessageService(IMailSender mailSender, IAppConfiguration config, ITelemetryService telemetryService, ErrorLog errorLog, BackgroundMessageServiceFactory messageServiceFactory)
            :base(mailSender, config, telemetryService)
        {
            this.errorLog = errorLog;
            this.messageServiceFactory = messageServiceFactory;
            sentMessage = false;
        }

        private ErrorLog errorLog;
        private BackgroundMessageServiceFactory messageServiceFactory;
        private bool sentMessage;

        protected override Task SendMessageAsync(MailMessage mailMessage)
        {
            // Some MVC controller actions send more than one message. Since this method sends
            // the message async, we need a new IMessageService per email, to avoid calling
            // SmtpClient.SendAsync on an instance already with an Async operation in progress.

            if (sentMessage)
            {
                var newMessageService = messageServiceFactory.Create();
                return newMessageService.SendMessageAsync(mailMessage);
            }
            else
            {
                sentMessage = true;

                // Send email as background task, as we don't want to delay the HTTP response.
                // Particularly when sending email fails and needs to be retried with a delay.
                // MailMessage is IDisposable, so first clone the  message, to ensure if the
                // caller disposes it, the message is available until the async task is complete.
                var messageCopy = CloneMessage(mailMessage);

                HostingEnvironment.QueueBackgroundWorkItem(async _ =>
                    {
                        try
                        {
                            await base.SendMessageAsync(messageCopy);
                        }
                        catch (Exception ex)
                        {
                            // Log but swallow the exception.
                            QuietLog.LogHandledException(ex, errorLog);
                        }
                        finally
                        {
                            messageCopy.Dispose();
                        }
                    });

                return Task.CompletedTask;
            }
        }

        private MailMessage CloneMessage(MailMessage mailMessage)
        {
            string from = mailMessage.From.ToString();
            string to = mailMessage.To.ToString();

            MailMessage copy = new MailMessage(from, to, mailMessage.Subject, mailMessage.Body);

            copy.IsBodyHtml = mailMessage.IsBodyHtml;
            copy.BodyTransferEncoding = mailMessage.BodyTransferEncoding;
            copy.BodyEncoding = mailMessage.BodyEncoding;
            copy.HeadersEncoding = mailMessage.HeadersEncoding;
            foreach (System.Collections.Specialized.NameValueCollection header in mailMessage.Headers)
            {
                copy.Headers.Add(header);
            }
            copy.SubjectEncoding = mailMessage.SubjectEncoding;
            copy.DeliveryNotificationOptions = mailMessage.DeliveryNotificationOptions;
            foreach (var cc  in mailMessage.CC)
            {
                copy.CC.Add(cc);
            }
            foreach(var attachment in mailMessage.Attachments)
            {
                copy.Attachments.Add(attachment);
            }
            foreach (var bcc in mailMessage.Bcc)
            {
                copy.Bcc.Add(bcc);
            }
            foreach (var replyTo in mailMessage.ReplyToList)
            {
                copy.ReplyToList.Add(replyTo);
            }
            copy.Sender = mailMessage.Sender;
            copy.Priority = mailMessage.Priority;
            foreach (var view in mailMessage.AlternateViews)
            {
                copy.AlternateViews.Add(view);
            }

            return copy;
        }
    }
}