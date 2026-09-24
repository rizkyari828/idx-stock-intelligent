using IdxStockIntelligence.Infrastructure;

var dataRoot = Environment.GetEnvironmentVariable("IDX_DATA_ROOT")
    ?? Path.Combine(Directory.GetCurrentDirectory(), "data", "raw");

_ = new RawArtifactArchiver(dataRoot);

Console.WriteLine("IDX Stock Intelligence Phase 0 foundation is ready.");
Console.WriteLine($"Raw artifact root: {Path.GetFullPath(dataRoot)}");
Console.WriteLine("No provider was contacted. Review the bootstrap before source experimentation.");
