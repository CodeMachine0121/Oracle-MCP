namespace Oracle;

public class OracleErrorFormatter 
{
    public static string SanitizeExceptionMessage(Exception ex)
    {
        return ex.GetBaseException().Message;
    }
}
