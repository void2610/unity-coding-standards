using System.Threading.Tasks;
using Xunit;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Void2610.Unity.Analyzers.SerializeFieldNullCheckAnalyzer,
    Void2610.Unity.Analyzers.SerializeFieldNullCheckCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Void2610.Unity.Analyzers.Tests
{
    public class SerializeFieldNullCheckCodeFixTests
    {
        private const string SerializeFieldAttribute = @"
namespace UnityEngine
{
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class SerializeField : System.Attribute { }

    public class Object
    {
        public static implicit operator bool(Object value) => value != null;
    }
}
";

        [Fact]
        public async Task VUA1001_RemoveImplicitBoolGuard()
        {
            var test = SerializeFieldAttribute + @"
public class TestComponent
{
    [UnityEngine.SerializeField] private UnityEngine.Object target;

    public void Method()
    {
        {|#0:if (target) target.ToString();|}
    }
}";
            var fixedCode = SerializeFieldAttribute + @"
public class TestComponent
{
    [UnityEngine.SerializeField] private UnityEngine.Object target;

    public void Method()
    {
        target.ToString();
    }
}";
            var expected = Verify.Diagnostic("VUA1001")
                .WithLocation(0)
                .WithArguments("target");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task VUA1001_RemoveImplicitBoolGuardWithBlockBody_NoCodeFix()
        {
            var test = SerializeFieldAttribute + @"
public class TestComponent
{
    [UnityEngine.SerializeField] private UnityEngine.Object target;

    public void Method()
    {
        {|#0:if (target)
        {
            target.ToString();
        }|}
    }
}";
            var expected = Verify.Diagnostic("VUA1001")
                .WithLocation(0)
                .WithArguments("target");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }
    }
}
