namespace TestDatabase.Extensions
{
    public static class StringExtension
    {
        public static string ValidateMessage(this string message) => message.Substring(0, message.Length - 2);
        public static string ExceptionMessage(this Exception ex) => ex.InnerException?.Message ?? ex.Message;
        public static string SPParamName(this string name) => name.Substring(3);
    }
}
