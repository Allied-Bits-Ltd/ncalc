# Parameters

## Static Parameters

Static parameters are values which can be defined before the evaluation of an expression.
These parameters can be accessed using the <xref:NCalc.ExpressionBase`1.Parameters> property of the <xref:NCalc.Expression>
instance.

```c#
var expression = new Expression("2 * [x] ** 2 + 5 * [y]");
expression.Parameters["x"] = 5;
expression.Parameters["y"] = 1;

Console.WriteLine(expression.Evaluate());
```

Parameters can be useful when a value is unknown at compile time, or when performance is important and the parsing can
be saved for further calculations.

## Expression Parameters

Expressions can be split into several ones by defining expression parameters. Those parameters are not simple values but
<xref:NCalc.Expression> instances themselves.

```c#
Expression volume = new Expression("[surface] * h");
Expression surface = new Expression("[l] * [L]");
volume.Parameters["surface"] = surface;
surface.Parameters["l"] = 1;
surface.Parameters["L"] = 2;
```

An expression used as a parameter value is evaluated with its own parameters first, then with those of the outer expression (`Parameters`, `DynamicParameters` and `Functions`; functions declared with `fn` in the outer expression are not visible to it), and the handlers of the outer expression are called for it too. The parameter expression itself is not changed. An expression that refers to itself through its parameters throws <xref:NCalc.Exceptions.NCalcEvaluationException>.

## Dynamic Parameters

Sometimes parameters can be even more complex to evaluate and need a dedicated method to be evaluated. This can be done
using the <xref:NCalc.ExpressionParameter> delegate.

```c#
var expression = new Expression("Round(Pow([Pi], 2) + Pow([Pi2], 2) + [X], 2)");

expression.Parameters["Pi2"] = new Expression("Pi * [Pi]");
expression.Parameters["X"] = 10;

expression.DynamicParameters["Pi"] = _ => 
{
    Console.WriteLine("I'm evaluating π!");
    return 3.14;
};

// expression.Evaluate() returns 117.07
```

To find the value of a name, the `EvaluateParameter` handlers are called first; when no handler sets a result, the name is looked up in `Parameters` and then in `DynamicParameters`. A name that none of them provides throws <xref:NCalc.Exceptions.NCalcParameterNotDefinedException>.

## Square Brackets Parameters

Parameters in between square brackets can contain special characters like spaces, dots, and also start with digits.

```c#
var expression = new Expression("[My First Parameter] + [My Second Parameter]");
```

## Curly Braces Parameters

In Allied Bits NCalc, curly braces are used to group expressions. 
So while the '{PageState}' will work when referencing the value of the parameter, assigning to '{PageState}' will not work (use square brackets instead). 

## Multi-Valued Parameters

When parameters are `IEnumerable` and the <xref:NCalc.ExpressionOptions.IterateParameters> is
used, the result is a `List<object?>` made of the evaluation of each value in the parameter.

```csharp
var expression = new Expression("(a * b) ** c", ExpressionOptions.IterateParameters);
expression.Parameters["a"] = new int[] { 1, 2, 3, 4, 5 };
expression.Parameters["b"] = new int[] { 6, 7, 8, 9, 0 };
expression.Parameters["c"] = 3;

foreach (var result in (IList)expression.Evaluate())
{
    Console.WriteLine(result);
}

//  216
//  2744
//  13824
//  46656
//  0
```

All list parameters must have the same number of items; the other parameters (including strings and dictionaries) are used as single values in each iteration. The `Parameters` dictionary is not changed during the iteration, so it may be read-only.

## Indexed parameters

If a parameter contains a list of values (IList), it is possible to access an element by accessing the variable and specifying a zero-based index (which may be an expression):
```csharp
var expression = new Expression("a := (1,2,3); a[ 2 / 2 ]", ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences); // 2
```

See [Indexed Access to Lists and Strings](operators.md#indexed-access-to-lists-and-strings) for ranges, indices from the end and assignments to elements.

## Using Event Handlers
You can also use event handlers to handle parameters.
```csharp
expression.EvaluateParameter += delegate(string name, ParameterArgs args)
{
    if (name == "Pi")
        args.Result = 3.14;
};
```

## Compare with Null Parameters

When parameter is null and the <xref:NCalc.ExpressionOptions.AllowNullParameter> flag is used, comparison of values to null is
allowed.

```c#
var expression = new Expression("'a string' == null", ExpressionOptions.AllowNullParameter);
(bool)expression.Evaluate();

//  False
```

## Getting all Parameters from an Expression

```c#
var expression = new Expression ("if(x=0,x,y)"); 
expression.Parameters["x"] = 1;
expression.Parameters["y"] = "pan";
var parameters = expression.GetParameterNames(); 
//  x
//  y
```

<xref:NCalc.ExpressionBase`1.GetParameterNames(System.Threading.CancellationToken)> and <xref:NCalc.ExpressionBase`1.GetFunctionNames(System.Threading.CancellationToken)> return each name once. The parameters of functions declared with `fn` are not reported.

## Case Sensitivity
See [case_sensitivity](case_sensitivity.md) for more info.

## Assigning and Updating Parameters 

Parameters can be assigned in expressions. 
Support for assignments must be enabled by including the <xref:NCalc.ExpressionOptions.UseAssignments> flag into <xref:NCalc.ExpressionOptions> of an <xref:NCalc.Expression> or <xref:NCalc.AsyncExpression>.

When a parameter is assigned, first the <xref:NCalc.Expression.UpdateParameter> event is fired. An event handler may tell the evaluation engine to update the static parameter table or bypass this step by setting the UpdateParameterArgs.<xref:NCalc.Handlers.UpdateParameterArgs.UpdateParameterLists> or AsyncUpdateParameterArgs.<xref:NCalc.Handlers.AsyncUpdateParameterArgs.UpdateParameterLists> property to `true` or `false` respectively.

Only a parameter or an indexed parameter can be assigned to: `5 := 1`, `Abs(x) := 7` and `5 += 1` are parse errors. Assignments are written with `:=`, or with `=` when the <xref:NCalc.ExpressionOptions.UseCStyleAssignments> flag is set (`==` is then the equality operator). A sequence of statements such as the examples below also requires the <xref:NCalc.ExpressionOptions.UseStatementSequences> flag.

An assignment is an expression, so it can be used wherever a value is accepted. E.g., the following operations are equivalent:

```
if (true, a := 2, a := 4); a + Max(2; 4)
a := if (true; 2; 4); a + Max(2; 4)
```

An assignment can be combined with an arithmetic operator (such as "+=" for addition with assignment); please, see the [Operators](operators.md) topic for the list of supported operators with assignment.

An assignment may be combined with indexed access:

```
a := (1; 2; 3); a[1] := -2;
```

### Accessing and Updating Parameters in Functions

The body of a function declared in an expression with `fn` can read the parameters (`Parameters`), dynamic parameters (`DynamicParameters`) and custom functions of the expression, but its assignments do not change them: a value assigned in the body stays in the body. 
The <xref:NCalc.Expression.EvaluateParameter> and <xref:NCalc.Expression.UpdateParameter> events are still fired; so, if the application needs to have a global list of parameters / variables, it can make use of these events to provide and update the global values. See [Scope of names in a function body](functions.md#scope-of-names-in-a-function-body) for the details.
