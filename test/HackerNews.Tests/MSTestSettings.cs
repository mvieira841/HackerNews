using Xunit;

// PERFORMANCE CONFIGURATION:
// By default, xUnit runs tests in parallel by class. Setting MaxParallelThreads to 0
// allows xUnit to utilize the maximum number of logical processors available on the machine,
// speeding up large test suites.
[assembly: CollectionBehavior(MaxParallelThreads = 0)]