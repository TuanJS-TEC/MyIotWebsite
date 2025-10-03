namespace MyIotWebsite.Models
{
    public class PaginatedResponse<T>
    {
        public List<T> Data { get; set; }
        public int PageNumber { get; set; } 
        public int TotalPages { get; set; } 
    }
}