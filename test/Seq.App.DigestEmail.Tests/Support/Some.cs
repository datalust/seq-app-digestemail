using System;
using System.Collections.Generic;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.DigestEmail.Tests.Support
{
    public static class Some
    {
        public static string String()
        {
            return Guid.NewGuid().ToString();
        }

        public static uint Uint()
        {
            return 5417u;
        }

        public static uint EventType()
        {
            return Uint();
        }

        public static Event<LogEventData> LogEvent(IDictionary<string, object> includedProperties = null)
        {
            var id = EventId();
            var timestamp = UtcTimestamp();
            var properties = new Dictionary<string, object>
            {
                {"Who", "world"},
                {"Number", 42}
            };

            if (includedProperties != null)
            {
                foreach (var includedProperty in includedProperties)
                {
                    properties.Add(includedProperty.Key, includedProperty.Value);
                }
            }

            return new Event<LogEventData>(id, EventType(), timestamp, new LogEventData
            {
                Exception = null,
                Id = id,
                Level = LogEventLevel.Fatal,
                LocalTimestamp = new DateTimeOffset(timestamp),
                MessageTemplate = "Hello, {Who}",
                RenderedMessage = "Hello, world",
                Properties = properties
            });
        }

        public static string EventId()
        {
            return "event-" + String();
        }

        public static DateTime UtcTimestamp()
        {
            return DateTime.UtcNow;
        }

        public static string Uri()
        {
            return "https://" + String();
        }

        public static Host Host()
        {
            return new Host(new [] { Uri() }, String() );
        }

        public static Apps.App App()
        {
            return new Apps.App(String(), String(), new Dictionary<string, string>(), String());
        }
    }
}