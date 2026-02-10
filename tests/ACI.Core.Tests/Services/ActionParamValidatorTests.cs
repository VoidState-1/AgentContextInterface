using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Tests.Common.TestData;

namespace ACI.Core.Tests.Services;

public class ActionParamValidatorTests
{
    // 测试点：当 schema 为空时，校验器应直接放行参数。
    // 预期结果：返回 null（无错误）。
    [Fact]
    public void Validate_NullSchema_ShouldPass()
    {
        var error = ActionParamValidator.Validate(null, null);

        Assert.Null(error);
    }

    // 测试点：必填参数在缺失时应被拦截。
    // 预期结果：返回 "params is required"。
    [Fact]
    public void Validate_RequiredMissing_ShouldFail()
    {
        var schema = new ActionParamSchema
        {
            Kind = ActionParamKind.String,
            Required = true
        };

        var error = ActionParamValidator.Validate(schema, null);

        Assert.Equal("params is required", error);
    }

    // 测试点：非必填参数在缺失时应允许通过。
    // 预期结果：返回 null（无错误）。
    [Fact]
    public void Validate_OptionalMissing_ShouldPass()
    {
        var schema = new ActionParamSchema
        {
            Kind = ActionParamKind.String,
            Required = false
        };

        var error = ActionParamValidator.Validate(schema, null);

        Assert.Null(error);
    }

    // 测试点：字符串类型参数在传入字符串时应通过校验。
    // 预期结果：返回 null（无错误）。
    [Fact]
    public void Validate_StringTypeWithStringValue_ShouldPass()
    {
        var schema = new ActionParamSchema
        {
            Kind = ActionParamKind.String
        };
        var parameters = TestJson.Parse("\"hello\"");

        var error = ActionParamValidator.Validate(schema, parameters);

        Assert.Null(error);
    }

    // 测试点：整数类型参数传入小数时应失败。
    // 预期结果：返回 "params must be integer"。
    [Fact]
    public void Validate_IntegerTypeWithFloatValue_ShouldFail()
    {
        var schema = new ActionParamSchema
        {
            Kind = ActionParamKind.Integer
        };
        var parameters = TestJson.Parse("1.5");

        var error = ActionParamValidator.Validate(schema, parameters);

        Assert.Equal("params must be integer", error);
    }

    // 测试点：对象参数缺少必填字段时，应返回具体字段路径。
    // 预期结果：返回 "params.name is required"。
    [Fact]
    public void Validate_ObjectMissingRequiredProperty_ShouldFail()
    {
        var schema = new ActionParamSchema
        {
            Kind = ActionParamKind.Object,
            Properties = new Dictionary<string, ActionParamSchema>
            {
                ["name"] = new ActionParamSchema
                {
                    Kind = ActionParamKind.String,
                    Required = true
                }
            }
        };
        var parameters = TestJson.Parse("{}");

        var error = ActionParamValidator.Validate(schema, parameters);

        Assert.Equal("params.name is required", error);
    }

    // 测试点：对象参数出现未声明字段时应拒绝。
    // 预期结果：返回 "params.unknown is not allowed"。
    [Fact]
    public void Validate_ObjectWithUnknownProperty_ShouldFail()
    {
        var schema = new ActionParamSchema
        {
            Kind = ActionParamKind.Object,
            Properties = new Dictionary<string, ActionParamSchema>
            {
                ["name"] = new ActionParamSchema
                {
                    Kind = ActionParamKind.String,
                    Required = true
                }
            }
        };
        var parameters = TestJson.Parse("""{"name":"ok","unknown":1}""");

        var error = ActionParamValidator.Validate(schema, parameters);

        Assert.Equal("params.unknown is not allowed", error);
    }

    // 测试点：数组元素类型不匹配时，错误信息应包含索引路径。
    // 预期结果：返回 "params[0] must be integer"。
    [Fact]
    public void Validate_ArrayItemTypeMismatch_ShouldFailWithIndexPath()
    {
        var schema = new ActionParamSchema
        {
            Kind = ActionParamKind.Array,
            Items = new ActionParamSchema
            {
                Kind = ActionParamKind.Integer
            }
        };
        var parameters = TestJson.Parse("""["bad",2,3]""");

        var error = ActionParamValidator.Validate(schema, parameters);

        Assert.Equal("params[0] must be integer", error);
    }
}

