namespace ReadmeSample.Entities;

public class User
{
    public int Id { get; set; }
    public required string UserName { get; set; }
    public required string EmailAddress { get; set; }

    public ICollection<Order> Orders { get; set; } = [];
}
