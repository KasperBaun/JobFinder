namespace Jobmatch.Api;

/// <summary>
/// Every URL the API answers, in one place. This is the table of contents for the HTTP surface —
/// deliberately central rather than split per feature, because "what endpoints exist?" is the
/// question it exists to answer.
/// </summary>
public static class Routes
{
    /// <summary>Every route sits under this prefix; the SPA is served from the root.</summary>
    public const string Prefix = "/api";

    public static class System
    {
        public const string Tag = "System";
        public const string Base = $"{Prefix}/system";
        public const string Ping = $"{Base}/ping";
        // Shutdown is mapped only by Jobmatch.Host (HostShutdownEndpoint). The
        // standalone Jobmatch.Api project intentionally does not expose this.
        public const string Shutdown = $"{Base}/shutdown";
    }

    public static class Whoami
    {
        public const string Tag = "Whoami";
        public const string Base = $"{Prefix}/whoami";
        public const string Get = Base;
    }

    public static class Providers
    {
        public const string Tag = "Providers";
        public const string Base = $"{Prefix}/providers";
        public const string ById = $"{Base}/{{id:int}}";
        public const string GetAll = Base;
        public const string GetById = ById;
        public const string Update = ById;
        public const string Test = $"{ById}/test";
        public const string SetSecrets = $"{ById}/secrets";
        public const string SetConfig = $"{ById}/config";
        // Add-a-source flow: detect a pasted URL, preview-test the candidate, create, delete.
        public const string Create = Base;
        public const string Delete = ById;
        public const string Detect = $"{Base}/detect";
        public const string PreviewTest = $"{Base}/detect/test";
    }

    public static class Skillset
    {
        public const string Tag = "Skillset";
        public const string Base = $"{Prefix}/skillset";
        public const string Get = Base;
        public const string Update = Base;
        // CV-driven profile setup (R-011): background extraction + status poll.
        public const string Extract = $"{Base}/extract";
        public const string ExtractStatus = $"{Base}/extract/status";
    }

    public static class Search
    {
        public const string Tag = "Search";
        public const string Base = $"{Prefix}/search";
        // POST: enqueue a background run, returns { id }. Repurposed from the old synchronous SSE run.
        public const string Run = Base;
        // GET literal — must stay above ById so routing prefers it over the {id} parameter.
        public const string Active = $"{Base}/active";
        public const string ById = $"{Base}/{{id}}";
        public const string Stream = $"{Base}/{{id}}/stream";
        public const string Cancel = $"{Base}/{{id}}/cancel";
    }

    public static class History
    {
        public const string Tag = "History";
        public const string Base = $"{Prefix}/history";
        public const string ByRunId = $"{Base}/{{runId}}";
        public const string GetAll = Base;
        public const string GetByRunId = ByRunId;
        public const string Delete = $"{Base}/delete";
    }

    public static class Marks
    {
        public const string Tag = "Marks";
        public const string Base = $"{Prefix}/marks";
        public const string Set = Base;
        public const string SetStatus = $"{Base}/status";
    }

    public static class Applications
    {
        public const string Tag = "Applications";
        public const string Base = $"{Prefix}/applications";
        public const string GetAll = Base;
    }

    public static class Cv
    {
        public const string Tag = "Cv";
        public const string Base = $"{Prefix}/cv";
        public const string Get = Base;
        public const string Update = Base;
    }

    public static class Drafting
    {
        public const string Tag = "Drafting";
        public const string Base = $"{Prefix}/drafts";
        // Writing two documents takes minutes on CPU, so it runs in the background and the GUI
        // polls, the same shape as the CV extraction flow (R-121).
        public const string Draft = Base;
        public const string Status = $"{Base}/status";
    }

    public static class Llm
    {
        public const string Tag = "Llm";
        public const string Base = $"{Prefix}/llm";
        public const string Status = $"{Base}/status";
        public const string DownloadModel = $"{Base}/download-model";
    }

    public static class Config
    {
        public const string Tag = "Config";
        public const string Base = $"{Prefix}/config";
        public const string Export = $"{Base}/export";
        public const string Import = $"{Base}/import";
    }

    public static class Setup
    {
        public const string Tag = "Setup";
        public const string Base = $"{Prefix}/setup";
        public const string Status = $"{Base}/status";
        public const string Complete = Base;
    }

    public static class Settings
    {
        public const string Tag = "Settings";
        public const string Base = $"{Prefix}/settings";
        // Reads ride along on Setup.Status, which the GUI already fetches at boot.
        public const string SetLanguage = $"{Base}/language";
    }
}
