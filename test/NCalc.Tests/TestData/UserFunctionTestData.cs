namespace NCalc.Tests.TestData
{
    internal class UserFunctionTestData : TheoryData<string, object?>
    {
        public UserFunctionTestData()
        {
            Add("fn mirror(x) { x }; mirror(1) ", 1);
            Add("fn add(a, b) { a + b }; add(1; 2) ", 3);
            Add("fn add(a, b) { return 1; a + b }; add(1; 2) ", 1);
            Add("fn add(a, b = 2) { a + b }; add(1) ", 3);
            Add("fn add(a, b) => a + b ; add(1; 2) ", 3);
            Add("fn sum3(a, b = 0, c = 3) => a + b + c; sum3(1;2)", 6);
            Add("fn DateOfBuild(n) => n * 5", null);
            Add("fn DateOfBuild(n) { return #01/01/2000# + n * 86400 * 10000000; } ", null);
            Add("fn DateOfBuild(n) { return #2000-01-01Z# + n * 86400 * 10000000; } ", null);
            Add("fn DateOfBuild(n) { return #2000-01-01T00:00:00Z# + n * 86400 * 10000000; } ", null);
            Add("fn DateOfBuild(n) => n * 86400 * 10000000 + #01/01/2000# ", null);
            Add("fn DateOfBuild(n) => #01/01/2000# + n * 86400 * 10000000 ;", null);
        }
    }
}
