namespace FacilityRealtime.ApiTests.Infrastructure;

/// <summary>The MySQL tests share one throwaway database, so they must not run in parallel with each other.</summary>
[CollectionDefinition("MySQL", DisableParallelization = true)]
public class MySqlCollection;
