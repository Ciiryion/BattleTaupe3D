
namespace BattleTaupe3D.Entities
{
    public class PlayerEntity
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public DateTime Creation { get; set; } = DateTime.Now;

        public DateTime Birth { get; set; }

        public string Location { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}
