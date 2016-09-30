using System;
using System.Collections.Generic;
using HandlebarsDotNet;
using Seq.App.DigestEmail.Tests.Support;
using Seq.Apps;
using Seq.Apps.LogEvents;
using Xunit;

namespace Seq.App.DigestEmail.Tests
{
    public class DigestEmailReactorTests
    {
        [Fact]
        public void BuiltInPropertiesAreRenderedInTemplates()
        {
            var template = Handlebars.Compile("{{$Events.[0].$Level}}");
            var data = Some.LogEvent();
            var result = DigestEmailReactor.FormatTemplate(template, new [] { data }, Some.Host(), Some.App(), Some.String());
            Assert.Equal(data.Data.Level.ToString(), result);
        }

        [Fact]
        public void PayloadPropertiesAreRenderedInTemplates()
        {
            var template = Handlebars.Compile("See {{$Events.[0].What}}");
            var data = Some.LogEvent(new Dictionary<string, object> { { "What", 10 } });
            var result = DigestEmailReactor.FormatTemplate(template, new [] { data }, Some.Host(), Some.App(), Some.String());
            Assert.Equal("See 10", result);
        }

        [Fact]
        public void NoPropertiesAreRequiredOnASourceEvent()
        {
            var template = Handlebars.Compile("No properties");
            var id = Some.EventId();
            var timestamp = Some.UtcTimestamp();
            var data = new Event<LogEventData>(id, Some.EventType(), timestamp, new LogEventData
            {
                Exception = null,
                Id = id,
                Level = LogEventLevel.Fatal,
                LocalTimestamp = new DateTimeOffset(timestamp),
                MessageTemplate = "Some text",
                RenderedMessage = "Some text",
                Properties = null
            });
            var result = DigestEmailReactor.FormatTemplate(template, new [] { data }, Some.Host(), Some.App(), Some.String());
            Assert.Equal("No properties", result);
        }
    }
}
