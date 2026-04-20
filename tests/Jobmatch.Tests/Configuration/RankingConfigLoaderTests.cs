using Jobmatch;
using Jobmatch.Configuration;

namespace Jobmatch.Tests.Configuration;

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
}
