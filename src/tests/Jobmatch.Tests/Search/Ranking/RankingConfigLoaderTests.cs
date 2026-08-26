using Jobmatch.Infrastructure.Llm;
using Jobmatch.Search.Ranking;
using Jobmatch;

namespace Jobmatch.Tests.Search.Ranking;

public sealed class RankingConfigLoaderTests
{
    [Fact]
    public void Parse_Valid_Yaml_Returns_Config()
    {
        var yaml = """
            weights:
              primary_stack: 0.40
              secondary_stack: 0.15
              seniority: 0.15
              location_remote: 0.15
              domain: 0.10
              freshness: 0.05

            disqualifier_penalty: 0.0
            top_n: 10
            freshness_half_life_days: 14
            min_score_to_include: 0.25
            """;

        var cfg = RankingConfigLoader.Parse(yaml);

        Assert.Equal(0.40, cfg.Weights.PrimaryStack);
        Assert.Equal(0.15, cfg.Weights.SecondaryStack);
        Assert.Equal(0.15, cfg.Weights.Seniority);
        Assert.Equal(0.15, cfg.Weights.LocationRemote);
        Assert.Equal(0.10, cfg.Weights.Domain);
        Assert.Equal(0.05, cfg.Weights.Freshness);
        Assert.Equal(1.00, cfg.Weights.Sum(), 3);
        Assert.Equal(10, cfg.TopN);
        Assert.Equal(14.0, cfg.FreshnessHalfLifeDays);
        Assert.Equal(0.25, cfg.MinScoreToInclude);
        Assert.Equal(0.0, cfg.DisqualifierPenalty);
    }

    [Fact]
    public void Parse_Missing_Weights_Throws()
    {
        var yaml = """
            top_n: 5
            """;
        var ex = Assert.Throws<ConfigException>(() => RankingConfigLoader.Parse(yaml));
        Assert.Contains("weights", ex.Message);
    }

    [Fact]
    public void Parse_PreferredCompanyBoost_When_Present()
    {
        var yaml = """
            weights:
              primary_stack: 1.0
              secondary_stack: 0.0
              seniority: 0.0
              location_remote: 0.0
              domain: 0.0
              freshness: 0.0
            preferred_company_boost: 1.5
            """;
        var cfg = RankingConfigLoader.Parse(yaml);
        Assert.Equal(1.5, cfg.PreferredCompanyBoost);
    }

    [Fact]
    public void Parse_PreferredCompanyBoost_Defaults_When_Missing()
    {
        var yaml = """
            weights:
              primary_stack: 1.0
              secondary_stack: 0.0
              seniority: 0.0
              location_remote: 0.0
              domain: 0.0
              freshness: 0.0
            """;
        var cfg = RankingConfigLoader.Parse(yaml);
        Assert.Equal(1.25, cfg.PreferredCompanyBoost);
    }

    [Fact]
    public void Parse_MaxAgeDays_When_Present()
    {
        var yaml = """
            weights:
              primary_stack: 1.0
              secondary_stack: 0.0
              seniority: 0.0
              location_remote: 0.0
              domain: 0.0
              freshness: 0.0
            max_age_days: 60
            """;
        var cfg = RankingConfigLoader.Parse(yaml);
        Assert.Equal(60, cfg.MaxAgeDays);
    }

    [Fact]
    public void Parse_MaxAgeDays_Defaults_To_Null_When_Missing()
    {
        var yaml = """
            weights:
              primary_stack: 1.0
              secondary_stack: 0.0
              seniority: 0.0
              location_remote: 0.0
              domain: 0.0
              freshness: 0.0
            """;
        var cfg = RankingConfigLoader.Parse(yaml);
        Assert.Null(cfg.MaxAgeDays);
    }

    [Fact]
    public void Parse_LocationTierWeights_When_Present()
    {
        var yaml = """
            weights:
              primary_stack: 1.0
              secondary_stack: 0.0
              seniority: 0.0
              location_remote: 0.0
              domain: 0.0
              freshness: 0.0
            location_tier_weights:
              city: 1.0
              metro: 0.9
              country: 0.5
              region: 0.2
              else: 0.0
            """;
        var cfg = RankingConfigLoader.Parse(yaml);
        Assert.Equal(1.0, cfg.LocationTierWeights.City);
        Assert.Equal(0.9, cfg.LocationTierWeights.Metro);
        Assert.Equal(0.5, cfg.LocationTierWeights.Country);
        Assert.Equal(0.2, cfg.LocationTierWeights.Region);
        Assert.Equal(0.0, cfg.LocationTierWeights.Else);
    }

    [Fact]
    public void Parse_LocationTierWeights_Defaults_When_Missing()
    {
        var yaml = """
            weights:
              primary_stack: 1.0
              secondary_stack: 0.0
              seniority: 0.0
              location_remote: 0.0
              domain: 0.0
              freshness: 0.0
            """;
        var cfg = RankingConfigLoader.Parse(yaml);
        var d = Jobmatch.Search.Ranking.LocationTierWeights.Default;
        Assert.Equal(d.City, cfg.LocationTierWeights.City);
        Assert.Equal(d.Metro, cfg.LocationTierWeights.Metro);
        Assert.Equal(d.Country, cfg.LocationTierWeights.Country);
        Assert.Equal(d.Region, cfg.LocationTierWeights.Region);
        Assert.Equal(d.Else, cfg.LocationTierWeights.Else);
    }

    [Fact]
    public void Parse_Default_Values_Applied_When_Scalars_Missing()
    {
        var yaml = """
            weights:
              primary_stack: 1.0
              secondary_stack: 0.0
              seniority: 0.0
              location_remote: 0.0
              domain: 0.0
              freshness: 0.0
            """;
        var cfg = RankingConfigLoader.Parse(yaml);
        Assert.Equal(10, cfg.TopN);
        Assert.Equal(0.0, cfg.DisqualifierPenalty);
        Assert.Equal(14.0, cfg.FreshnessHalfLifeDays);
        Assert.Equal(0.25, cfg.MinScoreToInclude);
    }

    private const string Weights = """
        weights:
          primary_stack: 1.0
          secondary_stack: 0.0
          seniority: 0.0
          location_remote: 0.0
          domain: 0.0
          freshness: 0.0
        """;

    [Fact]
    public void Parse_LlamaSharp_Provider_Carries_Only_Its_Own_Settings()
    {
        var cfg = RankingConfigLoader.Parse($"""
            {Weights}
            llm:
              enabled: true
              provider: llamasharp
              model_path: models/custom.gguf
              model_download_url: https://example.com/custom.gguf
              context_size: 8192
              gpu_layer_count: 33
            """);

        Assert.True(cfg.Llm.Enabled);
        var llama = Assert.IsType<LlmProvider.LlamaSharp>(cfg.Llm.Provider);
        Assert.Equal("models/custom.gguf", llama.Model.ConfiguredPath);
        Assert.Equal(new Uri("https://example.com/custom.gguf"), llama.Model.DownloadUrl);
        Assert.Equal(8192, llama.ContextSize);
        Assert.Equal(33, llama.GpuLayerCount);
    }

    [Fact]
    public void Parse_Ollama_Provider_Carries_Only_Its_Own_Settings()
    {
        var cfg = RankingConfigLoader.Parse($"""
            {Weights}
            llm:
              enabled: true
              provider: ollama
              base_url: http://127.0.0.1:9999
              model: llama3:8b
            """);

        var ollama = Assert.IsType<LlmProvider.Ollama>(cfg.Llm.Provider);
        Assert.Equal(new Uri("http://127.0.0.1:9999"), ollama.BaseUrl);
        Assert.Equal("llama3:8b", ollama.ModelTag);
    }

    [Fact]
    public void Parse_Unknown_Provider_Throws_While_Reading_Config()
    {
        var ex = Assert.Throws<ConfigException>(() => RankingConfigLoader.Parse($"""
            {Weights}
            llm:
              enabled: true
              provider: llamashrp
            """));

        Assert.Contains("llamashrp", ex.Message, StringComparison.Ordinal);
        Assert.Contains("llamasharp", ex.Message, StringComparison.Ordinal);
        Assert.Contains("ollama", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("model_download_url", "not a url")]
    [InlineData("base_url", "localhost:11434")]
    public void Parse_Malformed_Url_Throws_While_Reading_Config(string key, string value)
    {
        var provider = key == "base_url" ? "ollama" : "llamasharp";

        var ex = Assert.Throws<ConfigException>(() => RankingConfigLoader.Parse($"""
            {Weights}
            llm:
              enabled: true
              provider: {provider}
              {key}: {value}
            """));

        Assert.Contains(key, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Judge_Policy_Reads_From_The_Llm_Block()
    {
        var cfg = RankingConfigLoader.Parse($"""
            {Weights}
            llm:
              enabled: true
              top_n: 25
              weight: 0.75
            """);

        Assert.Equal(25, cfg.Judge.FirstPassBudget);
        Assert.Equal(0.75, cfg.Judge.Weight);
    }

    [Fact]
    public void Parse_No_Llm_Block_Leaves_Ai_Off_With_Shipped_Defaults()
    {
        var cfg = RankingConfigLoader.Parse(Weights);

        Assert.False(cfg.Llm.Enabled);
        Assert.Equal(JudgeConfig.Default, cfg.Judge);
        var llama = Assert.IsType<LlmProvider.LlamaSharp>(cfg.Llm.Provider);
        Assert.Equal(LlmModelFile.ShippedDefault, llama.Model);
    }
}
