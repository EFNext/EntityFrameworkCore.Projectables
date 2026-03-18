using System.Linq.Expressions;
using System.Reflection;

namespace EntityFrameworkCore.Projectables.Services;

public interface IProjectionExpressionResolver
{
    LambdaExpression FindGeneratedExpression(MemberInfo projectableMemberInfo,
        ProjectableAttribute? projectableAttribute = null);
}