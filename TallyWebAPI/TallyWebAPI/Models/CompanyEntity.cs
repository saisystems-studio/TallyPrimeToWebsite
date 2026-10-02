namespace TallyWebAPI.Models
{
    public class CompanyEntity
    {
        public int Id { get; set; }

        public string TallyGuid { get; set; } = "";

        public string Name { get; set; } = "";

        public string? FormalName { get; set; }

        public string? State { get; set; }

        public string? Country { get; set; }

        public string? Pincode { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string? Gstin { get; set; }

        public string? GstRegistrationType { get; set; }

        public string? StartingFrom { get; set; }

        public string? BooksFrom { get; set; }

        public DateTime LastSyncedAt { get; set; }
    }
}