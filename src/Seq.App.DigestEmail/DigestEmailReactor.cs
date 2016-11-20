using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using HandlebarsDotNet;
using Newtonsoft.Json;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.DigestEmail
{
    [SeqApp("Digest Email",
         Description = "Send batched HTML email using SMTP.")]
    public class DigestEmailReactor : Reactor, ISubscribeTo<LogEventData>, IDisposable
    {
        const int MaxSubjectLength = 130;
        const int DefaultBatchSizeLimit = 50;

        readonly object _sync = new object();
        readonly Lazy<Func<object, string>> _bodyTemplate;
        readonly List<Event<LogEventData>> _waiting = new List<Event<LogEventData>>();
        readonly Timer _timer;
        bool _disposed;

        static DigestEmailReactor()
        {
            Handlebars.RegisterHelper("pretty", PrettyPrint);
        }

        public DigestEmailReactor()
        {
            _bodyTemplate = new Lazy<Func<object, string>>(() =>
            {
                var bodyTemplate = BodyTemplate;
                if (string.IsNullOrEmpty(bodyTemplate))
                    bodyTemplate = Resources.DefaultBodyTemplate;
                return Handlebars.Compile(bodyTemplate);
            });

            _timer = new Timer(_ => SendBatch());
        }

        [SeqAppSetting(
             DisplayName = "From address",
             HelpText = "The account from which the email is being sent.")]
        public string From { get; set; }

        [SeqAppSetting(
             DisplayName = "To address",
             HelpText = "The account to which the email is being sent. Multiple addresses are separated by a comma or semicolon.")]
        public string To { get; set; }

        [SeqAppSetting(
             IsOptional = true,
             DisplayName = "Subject",
             HelpText = "The subject of the email. If blank, the title of the app instance will be used.")]
        public string Subject { get; set; }

        [SeqAppSetting(
             DisplayName = "Batch time (seconds)",
             HelpText = "The length of time to wait after receiving an event before sending the email batch.")]
        public int BatchTimeInSeconds { get; set; }

        [SeqAppSetting(
             IsOptional = true,
             DisplayName = "Maximum batch size",
             HelpText = "The maximum number of events to package into a single batch. If not specified, the default limit of 50 will be applied.")]
        public int? BatchSizeLimit { get; set; }

        [SeqAppSetting(
             HelpText = "The name of the SMTP server machine.")]
        public new string Host { get; set; }

        [SeqAppSetting(
             IsOptional = true,
             HelpText =
                 "The port on the SMTP server machine to send mail to. Leave this blank to use the standard port (25).")
        ]
        public int? Port { get; set; }

        [SeqAppSetting(
             IsOptional = true,
             DisplayName = "Enable SSL",
             HelpText = "Check this box if SSL is required to send email messages.")]
        public bool? EnableSsl { get; set; }

        [SeqAppSetting(
             IsOptional = true,
             InputType = SettingInputType.LongText,
             DisplayName = "Body template",
             HelpText =
                 "The template to use when generating the email body, using Handlebars.NET syntax. Leave this blank to use " +
                 "the default template that includes event messages and properties (https://github.com/datalust/seq-app-digestemail/tree/master/src/Seq.App.DigestEmail/Resources/DefaultBodyTemplate.html)."
         )]
        public string BodyTemplate { get; set; }

        [SeqAppSetting(
             IsOptional = true,
             HelpText = "The username to use when authenticating to the SMTP server, if required.")]
        public string Username { get; set; }

        [SeqAppSetting(
             IsOptional = true,
             InputType = SettingInputType.Password,
             HelpText = "The password to use when authenticating to the SMTP server, if required.")]
        public string Password { get; set; }

        public void On(Event<LogEventData> evt)
        {
            if (BatchTimeInSeconds < 0) return;

            lock (_sync)
            {
                if (_disposed)
                    return;

                _waiting.Add(evt);

                if (_waiting.Count == 1)
                {
                    _timer.Change(TimeSpan.FromSeconds(BatchTimeInSeconds), Timeout.InfiniteTimeSpan);
                }
            }
        }

        public static string FormatTemplate(Func<object, string> template, Event<LogEventData>[] evts, Host host,
            Apps.App app, string subject)
        {
            var payload = (IDictionary<string, object>) ToDynamic(new Dictionary<string, object>
            {
                {"$Events",    ToDynamic(evts.Select(ToPayload).ToArray())},
                {"$AppTitle",  app.Title},
                {"$Subject",   subject},
                {"$Instance",  host.InstanceName},
                {"$ServerUri", host.ListenUris.FirstOrDefault()}
            });

            return template(payload);
        }

        static Dictionary<string, object> ToPayload(Event<LogEventData> evt)
        {
            var properties =
                (IDictionary<string, object>) ToDynamic(evt.Data.Properties ?? new Dictionary<string, object>());

            var payload = new Dictionary<string, object>
            {
                {"$Id", evt.Id},
                {"$UtcTimestamp", evt.TimestampUtc},
                {"$LocalTimestamp", evt.Data.LocalTimestamp},
                {"$Level", evt.Data.Level},
                {"$MessageTemplate", evt.Data.MessageTemplate},
                {"$Message", evt.Data.RenderedMessage},
                {"$Exception", evt.Data.Exception},
                {"$Properties", properties},
                {"$EventType", "0x" + evt.EventType.ToString("X8")}
            };

            foreach (var property in properties)
            {
                payload[property.Key] = property.Value;
            }

            return payload;
        }

        static void PrettyPrint(TextWriter output, object context, object[] arguments)
        {
            var value = arguments.FirstOrDefault();
            if (value == null)
                output.WriteSafeString("null");
            else if (value is IEnumerable<object> || value is IEnumerable<KeyValuePair<string, object>>)
                output.WriteSafeString(JsonConvert.SerializeObject(FromDynamic(value)));
            else
                output.WriteSafeString(value.ToString());
        }

        static object FromDynamic(object o)
        {
            var dictionary = o as IEnumerable<KeyValuePair<string, object>>;
            if (dictionary != null)
            {
                return dictionary.ToDictionary(kvp => kvp.Key, kvp => FromDynamic(kvp.Value));
            }

            var enumerable = o as IEnumerable<object>;
            if (enumerable != null)
            {
                return enumerable.Select(FromDynamic).ToArray();
            }

            return o;
        }

        static object ToDynamic(object o)
        {
            var dictionary = o as IEnumerable<KeyValuePair<string, object>>;
            if (dictionary != null)
            {
                var result = new ExpandoObject();
                var asDict = (IDictionary<string, object>) result;
                foreach (var kvp in dictionary)
                    asDict.Add(kvp.Key, ToDynamic(kvp.Value));
                return result;
            }

            var enumerable = o as IEnumerable<object>;
            if (enumerable != null)
            {
                return enumerable.Select(ToDynamic).ToArray();
            }

            return o;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;

                _disposed = true;

                var wh = new ManualResetEvent(false);
                if (!_timer.Dispose(wh))
                    wh.WaitOne();

                if (_waiting.Any())
                    SendBatch();
            }
        }

        void SendBatch()
        {
            try
            {
                Event<LogEventData>[] evts;
                lock (_sync)
                {
                    evts = _waiting.ToArray();
                    _waiting.Clear();
                }

                if (!evts.Any())
                    return;

                var batchLimit = BatchSizeLimit ?? DefaultBatchSizeLimit;

                var batch = evts.Take(batchLimit).ToArray();
                evts = evts.Skip(batchLimit).ToArray();
                while (batch.Any())
                {
                    var subject = string.IsNullOrWhiteSpace(Subject) ? App.Title : Subject;
                    var body = FormatTemplate(_bodyTemplate.Value, batch, base.Host, App, subject);

                    if (subject.Length > MaxSubjectLength)
                        subject = subject.Substring(0, MaxSubjectLength);

                    var client = new SmtpClient(Host, Port ?? 25) { EnableSsl = EnableSsl ?? false };
                    if (!string.IsNullOrWhiteSpace(Username))
                        client.Credentials = new NetworkCredential(Username, Password);

                    var message = new MailMessage(From, To.Replace(";", ","), subject, body) { IsBodyHtml = true };

                    client.Send(message);

                    batch = evts.Take(batchLimit).ToArray();
                    evts = evts.Skip(batchLimit).ToArray();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not send digest email");
            }
        }
    }
}
