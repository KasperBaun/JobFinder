namespace Jobmatch.Gui.Server;

public static class Routes
{
    public static class System
    {
        public const string Ping = "/api/ping";
        public const string Shutdown = "/api/shutdown";
    }

    public static class Whoami
    {
        public const string Get = "/api/whoami";
    }
}
