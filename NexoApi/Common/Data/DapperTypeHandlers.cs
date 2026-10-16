using System.Data;
using Dapper;

namespace NexoApi.Common.Data;

public class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
{
    public override TimeOnly Parse(object value) => TimeOnly.FromTimeSpan((TimeSpan)value);
    public override void SetValue(IDbDataParameter parameter, TimeOnly value)
    {
        parameter.DbType = DbType.Time;
        parameter.Value = value.ToTimeSpan();
    }
}
