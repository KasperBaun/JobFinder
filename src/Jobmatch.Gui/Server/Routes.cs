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

    public static class Providers
    {
        public const string Get = "/api/providers";
        public const string GetOne = "/api/providers/{id:int}";
        public const string Update = "/api/providers/{id:int}";
        public const string Test = "/api/providers/{id:int}/test";
        public const string SetSecrets = "/api/providers/{id:int}/secrets";
    }

    public static class Skillset
    {
        public const string Get = "/api/skillset";
        public const string Put = "/api/skillset";
    }

    public static class Search
    {
        public const string Run = "/api/search";
    }

    public static class History
    {
        public const string List = "/api/history";
        public const string Detail = "/api/history/{runId}";
        public const string Delete = "/api/history/delete";
    }

    public static class Marks
    {
        public const string Set = "/api/marks";
    }
}
