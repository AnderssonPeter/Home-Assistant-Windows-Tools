using System;
using System.Diagnostics;
using Topshelf;

namespace HomeAssistantService
{
    class Program
    {
        static void Main(string[] args)
        {
            const string ServiceName = "HomeAssistant";
            HostFactory.Run(x =>
            {
                x.Service<ProccessManager>(s =>
                {
                    s.ConstructUsing(name => new ProccessManager());
                    s.WhenStarted(pm => pm.Start());
                    s.WhenStopped(pm => pm.Stop());
                });
                x.RunAsPrompt();
                x.StartAutomaticallyDelayed();
                x.DependsOnEventLog();
                x.SetDisplayName("Home Assistant");
                x.SetDescription("Home Assistant is an open-source home automation platform." + Environment.NewLine +
                                 "Track and control all devices at home and automate control.");
                x.SetServiceName(ServiceName);
                x.SetStartTimeout(TimeSpan.FromSeconds(30));
                x.BeforeInstall(settings =>
                {
                    if (!EventLog.SourceExists(settings.ServiceName))
                    {
                        EventLog.CreateEventSource(settings.ServiceName, "Application");
                    }
                });
                x.AfterUninstall(() =>
                {
                    if (EventLog.SourceExists(ServiceName))
                    {
                        EventLog.DeleteEventSource(ServiceName, "Application");
                    }
                });

                x.EnableServiceRecovery(rc =>
                {
                    rc.RestartService(1);
                    rc.SetResetPeriod(0);
                });
            });
        }
    }
}
