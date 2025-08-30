namespace NCalc.Tests.TestData
{
    internal class UserFunctionTestData : TheoryData<string, object>
    {
        public UserFunctionTestData()
        {
            Add("fn mirror(x) { x }; mirror(1) ", 1);
            Add("fn add(a, b) { a + b }; add(1; 2) ", 3);
            Add("fn add(a, b) { return 1; a + b }; add(1; 2) ", 1);
            Add("fn add(a, b = 2) { a + b }; add(1) ", 3);
            Add("fn add(a, b) => a + b ; add(1; 2) ", 3);
            Add("fn sum3(a, b = 0, c = 3) => a + b + c; sum3(1;2)", 6);
        }
    }
}
