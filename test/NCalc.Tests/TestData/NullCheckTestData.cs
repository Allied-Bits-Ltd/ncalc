namespace NCalc.Tests.TestData;

public class NullCheckTestData : TheoryData<string, object>
{
    public NullCheckTestData()
    {
        Add("null + 5", null);
        Add("null / 5", null);
        Add("null mod 5", null);
        Add("null div 5", null);
        Add("if((5 + null > 0), 1, 2)", 2);
        Add("if((5 - null > 0), 1, 2)", 2);
        Add("if((5 / null > 0), 1, 2)", 2);
        Add("if((5 * null > 0), 1, 2)", 2);
        Add("if((5 % null > 0), 1, 2)", 2);
    }
}