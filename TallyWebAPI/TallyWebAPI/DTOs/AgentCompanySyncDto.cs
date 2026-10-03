namespace TallyWebAPI.DTOs
{
    public class AgentCompanySyncDto
    {
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
    }

    public class AgentCompanySyncRequest
    {
        public List<AgentCompanySyncDto> Companies { get; set; } = new();
    }
}