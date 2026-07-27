using NUnit.Framework;

// Integration suites bind real localhost TCP ports; keep execution fully sequential.
[assembly: NonParallelizable]
[assembly: LevelOfParallelism(1)]
