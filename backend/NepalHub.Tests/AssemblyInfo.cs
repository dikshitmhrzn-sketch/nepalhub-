using Xunit;

// The tests share one MySQL database and one process-wide configuration, so they
// must not run concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
