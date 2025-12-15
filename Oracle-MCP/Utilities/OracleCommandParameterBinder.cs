using System.Data.Common;

namespace Oracle;

public static class OracleCommandParameterBinder 
{
    public static void AddParameter(DbCommand command, string name, object? value)
    {
        string parameterName = name.StartsWith(':') ? name[1..] : name;
        var parameter = command.CreateParameter();
        parameter.ParameterName = parameterName;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
