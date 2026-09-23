namespace TallyWebAPI.DTOs
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;

        public int Id { get; set; }

        public string Username { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
    }
}