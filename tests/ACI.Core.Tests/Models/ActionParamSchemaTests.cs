using ACI.Core.Models;

namespace ACI.Core.Tests.Models;

public class ActionParamSchemaTests
{
    // 测试点：基础类型应输出对应 prompt 签名。
    // 预期结果：String -> string，Boolean -> boolean。
    [Fact]
    public void ToPromptSignature_PrimitiveKinds_ShouldReturnExpectedText()
    {
        var stringSchema = new ActionParamSchema
        {
            Kind = ActionParamKind.String
        };
        var boolSchema = new ActionParamSchema
        {
            Kind = ActionParamKind.Boolean
        };

        Assert.Equal("string", stringSchema.ToPromptSignature());
        Assert.Equal("boolean", boolSchema.ToPromptSignature());
    }

    // 测试点：数组未声明 Items 时应回退为 array<any>。
    // 预期结果：返回 "array<any>"。
    [Fact]
    public void ToPromptSignature_ArrayWithoutItems_ShouldUseAny()
    {
        var schema = new ActionParamSchema
        {
            Kind = ActionParamKind.Array
        };

        var signature = schema.ToPromptSignature();

        Assert.Equal("array<any>", signature);
    }

    // 测试点：对象字段签名应标识可选字段后缀 ?。
    // 预期结果：required 字段无后缀，optional 字段带 ?。
    [Fact]
    public void ToPromptSignature_ObjectWithRequiredAndOptionalFields_ShouldMarkOptionalField()
    {
        var schema = new ActionParamSchema
        {
            Kind = ActionParamKind.Object,
            Properties = new Dictionary<string, ActionParamSchema>
            {
                ["query"] = new ActionParamSchema
                {
                    Kind = ActionParamKind.String,
                    Required = true
                },
                ["limit"] = new ActionParamSchema
                {
                    Kind = ActionParamKind.Integer,
                    Required = false
                }
            }
        };

        var signature = schema.ToPromptSignature();

        Assert.Equal("{query:string, limit:integer?}", signature);
    }

    // 测试点：空对象签名应回退为 object。
    // 预期结果：返回 "object"。
    [Fact]
    public void ToPromptSignature_EmptyObject_ShouldReturnObject()
    {
        var schema = new ActionParamSchema
        {
            Kind = ActionParamKind.Object,
            Properties = new Dictionary<string, ActionParamSchema>()
        };

        var signature = schema.ToPromptSignature();

        Assert.Equal("object", signature);
    }
}

