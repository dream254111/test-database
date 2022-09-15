namespace TestDatabase.Models
{
    public class Response<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }

        public Response()
        {
            Success = false;
            Message = string.Empty;
        }
    }
}
