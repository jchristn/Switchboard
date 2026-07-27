// Integration suites bind real localhost TCP ports; running test collections in parallel would
// cause port conflicts. Force fully sequential execution. The project namespace (Test.Xunit)
// collides with the Xunit namespace, so the attribute is fully qualified via global::.
[assembly: global::Xunit.CollectionBehavior(DisableTestParallelization = true)]
