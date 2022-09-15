namespace TestDatabase.Models
{
    public class Validate
    {
        public bool IsMissing { get; set; }
        public string MissingError { get; set; }
        public bool IsInvalid { get; set; }
        public string InvalidError { get; set; }

        public Validate()
        {
            IsMissing = false;
            IsInvalid = false;

            MissingError = "Missing Parameter: ";
            InvalidError = "Invalid Parameter: ";
        }
    }
}
