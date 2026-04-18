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
        if ({|#0:target|}) target.ToString();
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
            // ブロック本体を持つ if には auto-fix を適用せず、警告のみ残す。
            // コード (test) と期待結果 (fixedCode) を同一にすることで "fix が適用されない" ことを検証する。
            var test = SerializeFieldAttribute + @"
public class TestComponent
{
    [UnityEngine.SerializeField] private UnityEngine.Object target;

    public void Method()
    {
        if ({|#0:target|})
        {
            target.ToString();
        }
    }
}";
            var expected = Verify.Diagnostic("VUA1001")
                .WithLocation(0)
                .WithArguments("target");
            await Verify.VerifyCodeFixAsync(test, expected, test);
        }

        [Fact]
        public async Task VUA1001_NotEqualsNullGuardWithBlockBody_NoCodeFix()
        {
            // 明示的 null チェック (!= null) のブロック本体も auto-fix 対象外。
            var test = SerializeFieldAttribute + @"
public class TestComponent
{
    [UnityEngine.SerializeField] private UnityEngine.Object target;

    public void Method()
    {
        if ({|#0:target != null|})
        {
            target.ToString();
            target.GetHashCode();
        }
    }
}";
            var expected = Verify.Diagnostic("VUA1001")
                .WithLocation(0)
                .WithArguments("target");
            await Verify.VerifyCodeFixAsync(test, expected, test);
        }

        [Fact]
        public async Task VUA1001_EqualsNullGuardWithBlockBodyEarlyReturn_NoCodeFix()
        {
            // early return パターンのブロック本体も auto-fix 対象外。
            // 従来の CodeFix はこのケースで return を消してしまい危険だった。
            var test = SerializeFieldAttribute + @"
public class TestComponent
{
    [UnityEngine.SerializeField] private UnityEngine.Object target;

    public void Method()
    {
        if ({|#0:target == null|})
        {
            return;
        }
        target.ToString();
    }
}";
            var expected = Verify.Diagnostic("VUA1001")
                .WithLocation(0)
                .WithArguments("target");
            await Verify.VerifyCodeFixAsync(test, expected, test);
        }
    }
}
