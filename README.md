# Seq Digest Email App [![Join the chat at https://gitter.im/datalust/seq](https://img.shields.io/gitter/room/datalust/seq.svg)](https://gitter.im/datalust/seq) [![NuGet Release](https://img.shields.io/nuget/v/Seq.App.DigestEmail.svg)](https://nuget.org/packages/Seq.App.DigestEmail) [![Build status](https://ci.appveyor.com/api/projects/status/x8ydgq6s8a4h2d44?svg=true)](https://ci.appveyor.com/project/datalust/seq-app-digestemail)

A plug-in for [Seq](https://getseq.net) that sends HTML email over SMTP. The digest email app sends multiple events in each email.

**Important note:** requires Seq version 3.4 or later.

### Getting started

The digest email app is distributed as [Seq.App.DigestEmail](https://nuget.org/packages/seq.app.digestemail) on NuGet.

Follow the instructions for [configuring the non-batched email app](http://docs.getseq.net/docs/formatting-html-email), but substitute the app name _Seq.App.DigestEmail_.

In the email template, the batch of events is in the `{{$Events}}` variable. You can view the default email template in this repository.

### Acknowledgements

The digest email app is based on the [Seq.App.EmailPlus codebase](https://github.com/datalust/seq-apps). Thanks to @kll for the original [pull request](https://github.com/datalust/seq-apps/pull/6) that drove the creation of this app.
