namespace Jobmatch.Api;

public static class Routes
{
    public static class System
    {
        public const string Tag = "System";
        public const string Base = $"{ApiConstants.RouteBase}/system";
        public const string Ping = $"{Base}/ping";
        // Shutdown is mapped only by Jobmatch.Host (HostShutdownEndpoint). The
        // standalone Jobmatch.Api project intentionally does not expose this.
        public const string Shutdown = $"{Base}/shutdown";
    }

    public static class Whoami
    {
        public const string Tag = "Whoami";
        public const string Base = $"{ApiConstants.RouteBase}/whoami";
        public const string Get = Base;
    }

    public static class Providers
    {
        public const string Tag = "Providers";
        public const string Base = $"{ApiConstants.RouteBase}/providers";
        public const string ById = $"{Base}/{{id:int}}";
        public const string GetAll = Base;
        public const string GetById = ById;
        public const string Update = ById;
        public const string Test = $"{ById}/test";
        public const string SetSecrets = $"{ById}/secrets";
    }

    public static class Skillset
    {
        public const string Tag = "Skillset";
        public const string Base = $"{ApiConstants.RouteBase}/skillset";
        public const string Get = Base;
        public const string Update = Base;
    }

    public static class Search
    {
        public const string Tag = "Search";
        public const string Base = $"{ApiConstants.RouteBase}/search";
        public const string Run = Base;
    }

    public static class History
    {
        public const string Tag = "History";
        public const string Base = $"{ApiConstants.RouteBase}/history";
        public const string ByRunId = $"{Base}/{{runId}}";
        public const string GetAll = Base;
        public const string GetByRunId = ByRunId;
        public const string Delete = $"{Base}/delete";
    }

    public static class Marks
    {
        public const string Tag = "Marks";
        public const string Base = $"{ApiConstants.RouteBase}/marks";
        public const string Set = Base;
    }

    public static class Llm
    {
        public const string Tag = "Llm";
        public const string Base = $"{ApiConstants.RouteBase}/llm";
        public const string Status = $"{Base}/status";
        public const string DownloadModel = $"{Base}/download-model";
    }

    public static class Config
    {
        public const string Tag = "Config";
        public const string Base = $"{ApiConstants.RouteBase}/config";
        public const string Export = $"{Base}/export";
        public const string Import = $"{Base}/import";
    }

    public static class Setup
    {
        public const string Tag = "Setup";
        public const string Base = $"{ApiConstants.RouteBase}/setup";
        public const string Status = $"{Base}/status";
        public const string Complete = Base;
    }
}
