using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Void2610.Unity.Analyzers.SerializeFieldNullCheckAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Void2610.Unity.Analyzers.Tests
{
    public class SerializeFieldNullCheckAnalyzerTests
    {
        // テスト用のSerializeField属性定義
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
        public async Task SerializeFieldEqualsNull_VUA1001()
        {
            // field == null → 検出
            var test = SerializeFieldAttribute + @"
public class TestClass
{
    [UnityEngine.SerializeField] private UnityEngine.SerializeField target;
    public void Method()
    {
        if ({|#0:target == null|}) return;
    }
}";
            var expected = Verify.Diagnostic("VUA1001")
                .WithLocation(0)
                .WithArguments("target");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task SerializeFieldNotEqualsNull_VUA1001()
        {
            // field != null → 検出
            var test = SerializeFieldAttribute + @"
public class TestClass
{
    [UnityEngine.SerializeField] private object target;
    public void Method()
    {
        if ({|#0:target != null|}) { }
    }
}";
            var expected = Verify.Diagnostic("VUA1001")
                .WithLocation(0)
                .WithArguments("target");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task NullEqualsSerializeField_VUA1001()
        {
            // null == field → 検出
            var test = SerializeFieldAttribute + @"
public class TestClass
{
    [UnityEngine.SerializeField] private object target;
    public void Method()
    {
        if ({|#0:null == target|}) return;
    }
}";
            var expected = Verify.Diagnostic("VUA1001")
                .WithLocation(0)
                .WithArguments("target");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task SerializeFieldConditionalAccess_VUA1001()
        {
            // field?.Method() → 検出
            var test = SerializeFieldAttribute + @"
public class TestClass
{
    [UnityEngine.SerializeField] private object target;
    public void Method()
    {
        {|#0:target?.ToString()|};
    }
}";
            var expected = Verify.Diagnostic("VUA1001")
                .WithLocation(0)
                .WithArguments("target");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task SerializeFieldCoalesce_VUA1001()
        {
            // field ?? fallback → 検出
            var test = SerializeFieldAttribute + @"
public class TestClass
{
    [UnityEngine.SerializeField] private string target;
    public string Method() => {|#0:target ?? ""fallback""|};
}";
            var expected = Verify.Diagnostic("VUA1001")
                .WithLocation(0)
                .WithArguments("target");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task SerializeFieldIsNull_VUA1001()
        {
            // field is null → 検出
            var test = SerializeFieldAttribute + @"
public class TestClass
{
    [UnityEngine.SerializeField] private object target;
    public void Method()
    {
        if ({|#0:target is null|}) return;
    }
}";
            var expected = Verify.Diagnostic("VUA1001")
                .WithLocation(0)
                .WithArguments("target");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task SerializeFieldIsNotNull_VUA1001()
        {
            // field is not null → 検出
            var test = SerializeFieldAttribute + @"
public class TestClass
{
    [UnityEngine.SerializeField] private object target;
    public void Method()
    {
        if ({|#0:target is not null|}) { }
    }
}";
            var expected = Verify.Diagnostic("VUA1001")
                .WithLocation(0)
                .WithArguments("target");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task NormalFieldNullCheck_NoDiagnostic()
        {
            // SerializeFieldでないフィールドのnullチェック → 検出なし
            var test = SerializeFieldAttribute + @"
public class TestClass
{
    private object _target;
    public void Method()
    {
        if (_target == null) return;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task SerializeFieldWithoutNullCheck_NoDiagnostic()
        {
            // SerializeFieldだがnullチェックなし → 検出なし
            var test = SerializeFieldAttribute + @"
public class TestClass
{
    [UnityEngine.SerializeField] private object target;
    public void Method()
    {
        target.ToString();
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task LocalVariableNullCheck_NoDiagnostic()
        {
            // ローカル変数のnullチェック → 検出なし
            var test = SerializeFieldAttribute + @"
public class TestClass
{
    public void Method()
    {
        object local = null;
        if (local == null) return;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task ParameterNullCheck_NoDiagnostic()
        {
            // パラメータのnullチェック → 検出なし
            var test = SerializeFieldAttribute + @"
public class TestClass
{
    public void Method(object param)
    {
        if (param == null) return;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task PropertyNullCheck_NoDiagnostic()
        {
            // プロパティのnullチェック → 検出なし
            var test = SerializeFieldAttribute + @"
public class TestClass
{
    public object Target { get; set; }
    public void Method()
    {
        if (Target == null) return;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task SerializeFieldImplicitBoolCheck_VUA1001()
        {
            // if (field) → 検出
            var test = SerializeFieldAttribute + @"
public class TestComponent
{
    [UnityEngine.SerializeField] private UnityEngine.Object target;
    public void Method()
    {
        if ({|#0:target|}) return;
    }
}";
            var expected = Verify.Diagnostic("VUA1001")
                .WithLocation(0)
                .WithArguments("target");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task SerializeFieldImplicitBoolNegation_VUA1001()
        {
            // if (!field) → 検出
            var test = SerializeFieldAttribute + @"
public class TestComponent
{
    [UnityEngine.SerializeField] private UnityEngine.Object target;
    public void Method()
    {
        if ({|#0:!target|}) return;
    }
}";
            var expected = Verify.Diagnostic("VUA1001")
                .WithLocation(0)
                .WithArguments("target");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task SerializeFieldBoolCondition_NoDiagnostic()
        {
            // bool型フィールドの通常分岐 → 検出なし
            var test = SerializeFieldAttribute + @"
public class TestComponent
{
    [UnityEngine.SerializeField] private bool isVisible;
    public void Method()
    {
        if (isVisible) return;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }
    }
}
