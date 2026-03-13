using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Void2610.Unity.Analyzers.MemberOrderAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Void2610.Unity.Analyzers.Tests
{
    public class MemberOrderAnalyzerTests
    {
        [Fact]
        public async Task CorrectOrder_AllCategories_NoDiagnostic()
        {
            // 全11カテゴリが正しい順序
            var test = @"
using System;

public class SerializeFieldAttribute : Attribute { }

public class TestClass
{
    public enum State { Idle, Running }

    [SerializeField] private int health;

    public int Value { get; set; }

    public const int MaxValue = 100;
    private static readonly int DefaultValue = 10;

    private int _count;
    private readonly int _id;

    public TestClass(int id) { _id = id; }

    public int GetValue() => _count;

    public void DoSomething()
    {
        _count++;
        _count++;
    }

    private void Helper()
    {
        _count = 0;
    }

    private void Awake()
    {
        _count = DefaultValue;
    }

    private void OnDestroy()
    {
        _count = 0;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task SerializeFieldAfterConstants_VUA3002()
        {
            // SerializeFieldがconstantsより後にある
            var test = @"
using System;

public class SerializeFieldAttribute : Attribute { }

public class TestClass
{
    public const int MaxValue = 100;

    [SerializeField] private int {|#0:health|};
}";
            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("health", "SerializeField", "constants");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task PublicPropertyAfterPrivateField_VUA3002()
        {
            // public propertyがprivate fieldより後にある
            var test = @"
public class TestClass
{
    private int _count;

    public int {|#0:Value|} { get; set; }
}";
            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("Value", "public properties", "private fields");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task UnityEventFollowedByPrivateMethod_VUA3002()
        {
            // Unity eventの後にprivate methodがある
            var test = @"
public class TestClass
{
    private int _count;

    private void Awake()
    {
        _count = 0;
    }

    private void {|#0:Helper|}()
    {
        _count = 1;
    }
}";
            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("Helper", "private methods (multi line)", "Unity events");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task CleanupFollowedByUnityEvent_VUA3002()
        {
            // cleanupの後にUnity eventがある
            var test = @"
public class TestClass
{
    private int _count;

    private void OnDestroy()
    {
        _count = 0;
    }

    private void {|#0:Awake|}()
    {
        _count = 1;
    }
}";
            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("Awake", "Unity events", "cleanup");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task ExpressionBodyPublicAfterBlockBodyPublic_VUA3002()
        {
            // 式本体publicがブロック本体publicより後にある
            var test = @"
public class TestClass
{
    private int _count;

    public void DoSomething()
    {
        _count++;
        _count++;
    }

    public int {|#0:GetValue|}() => _count;
}";
            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("GetValue", "public methods (one line)", "public methods (multi line)");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task ProtectedOneLineAfterPrivateOneLine_SecondPattern_VUA3002()
        {
            var test = @"
public class TestClass
{
    private int _count;

    private int GetPrivate() => _count;

    protected int {|#0:GetProtected|}() => _count;
}";
            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("GetProtected", "protected methods (one line)", "private methods (one line)");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task ConstructorBetweenPrivateFieldsAndPublicMethods_NoDiagnostic()
        {
            // コンストラクタがprivate fieldsの後、public methodsの前 → 正しい
            var test = @"
public class TestClass
{
    private int _count;

    public TestClass(int count)
    {
        _count = count;
    }

    public int GetValue() => _count;
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task ConstructorAfterPublicMethods_VUA3002()
        {
            // コンストラクタがpublic methodsの後にある
            var test = @"
public class TestClass
{
    private int _count;

    public int GetValue() => _count;

    public {|#0:TestClass|}(int count)
    {
        _count = count;
    }
}";
            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("TestClass", "constructors", "public methods (one line)");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task StaticMethodFollowsAccessModifier_NoDiagnostic()
        {
            // staticメソッドがアクセス修飾子に従った正しい位置にある
            var test = @"
public class TestClass
{
    private int _count;

    public static int Create() => 0;

    public void DoSomething()
    {
        _count++;
        _count++;
    }

    private static void HelperStatic()
    {
        var x = 0;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task NestedClassExcluded_NoDiagnostic()
        {
            // ネストされたクラスは除外
            var test = @"
public class TestClass
{
    private int _count;

    private void Awake()
    {
        _count = 0;
    }

    private class InnerClass
    {
        public int Value { get; set; }
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task EmptyClass_NoDiagnostic()
        {
            // 空クラス
            var test = @"
public class TestClass
{
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task ProtectedOverrideAwake_UnityEvent_NoDiagnostic()
        {
            // protected override Awake() → Unity eventとして扱う
            var test = @"
public class BaseClass
{
    protected virtual void Awake() { }
}

public class TestClass : BaseClass
{
    private int _count;

    private void Helper()
    {
        _count = 0;
    }

    protected override void Awake()
    {
        _count = 1;
    }

    private void OnDestroy()
    {
        _count = 0;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task ProtectedMethodAfterPublicMethod_NoDiagnostic()
        {
            var test = @"
public class TestClass
{
    private int _count;

    public int GetPublic() => _count;

    protected virtual int GetProtected() => _count;

    public void Execute()
    {
        _count++;
    }

    protected virtual void Hook()
    {
        _count = 1;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task ProtectedOneLineAfterProtectedMultiLine_VUA3002()
        {
            var test = @"
public class TestClass
{
    private int _count;

    protected virtual void Hook()
    {
        _count = 1;
    }

    protected virtual int {|#0:GetValue|}() => _count;
}";
            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("GetValue", "protected methods (one line)", "protected methods (multi line)");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task ProtectedMultiLineAfterPrivateMultiLine_VUA3002()
        {
            var test = @"
public class TestClass
{
    private int _count;

    private void Helper()
    {
        _count = 0;
    }

    protected virtual void {|#0:Hook|}()
    {
        _count = 1;
    }
}";
            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("Hook", "protected methods (multi line)", "private methods (multi line)");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task ProtectedOneLineAfterPrivateOneLine_VUA3002()
        {
            var test = @"
public class TestClass
{
    private int _count;

    private int GetPrivate() => _count;

    protected int {|#0:GetProtected|}() => _count;
}";
            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("GetProtected", "protected methods (one line)", "private methods (one line)");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task StructMembers_CorrectOrder_NoDiagnostic()
        {
            // 構造体の正しい順序
            var test = @"
public struct TestStruct
{
    public int Value { get; set; }

    public const int MaxValue = 100;

    private int _count;

    public TestStruct(int count)
    {
        _count = count;
        Value = count;
    }

    public int GetValue() => _count;
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task StructMembers_WrongOrder_VUA3002()
        {
            // 構造体の順序違反
            var test = @"
public struct TestStruct
{
    private int _count;

    public int {|#0:Value|} { get; set; }
}";
            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("Value", "public properties", "private fields");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task DisposeIsCleanup_NoDiagnostic()
        {
            // DisposeメソッドはUnity eventの後（cleanup）に配置
            var test = @"
using System;

public class TestClass : IDisposable
{
    private int _count;

    private void Awake()
    {
        _count = 0;
    }

    public void Dispose()
    {
        _count = 0;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task MultipleViolations_ReportsAll()
        {
            // 複数の順序違反がある場合、すべて報告される
            var test = @"
public class TestClass
{
    private void Awake()
    {
        var x = 0;
    }

    private int {|#0:_count|};

    public int {|#1:Value|} { get; set; }
}";
            var expected0 = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("_count", "private fields", "Unity events");
            var expected1 = Verify.Diagnostic("VUA3002")
                .WithLocation(1)
                .WithArguments("Value", "public properties", "Unity events");
            await Verify.VerifyAnalyzerAsync(test, expected0, expected1);
        }

        [Fact]
        public async Task DestructorExcluded_NoDiagnostic()
        {
            // デストラクタは除外
            var test = @"
public class TestClass
{
    private int _count;

    private void Awake()
    {
        _count = 0;
    }

    ~TestClass()
    {
        _count = 0;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task PrivatePropertyExcluded_NoDiagnostic()
        {
            // privateプロパティは除外
            var test = @"
public class TestClass
{
    private int _count;

    private void Awake()
    {
        _count = 0;
    }

    private int InternalValue => _count;
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task ProtectedPropertyExcluded_NoDiagnostic()
        {
            // protectedプロパティは除外
            var test = @"
public class TestClass
{
    private int _count;

    private void Awake()
    {
        _count = 0;
    }

    protected int InternalValue => _count;
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task ConstAndPrivateFieldWithoutBlankLine_VUA3003()
        {
            var test = @"
public class TestClass
{
    private const int MaxValue = 1;
    private int {|#0:_count|};
}";

            var expected = Verify.Diagnostic("VUA3003")
                .WithLocation(0)
                .WithArguments("_count");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task MultipleBlankLinesBetweenMembers_VUA3003()
        {
            var test = @"
public class TestClass
{
    private int _a;


    private int {|#0:_b|};
}";

            var expected = Verify.Diagnostic("VUA3003")
                .WithLocation(0)
                .WithArguments("_b");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }
    }
}
