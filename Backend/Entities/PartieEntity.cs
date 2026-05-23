namespace BattleTaupe3D.Entities
{
    public class PartieEntity
    {
        public int Id { get; set; }

        public DateTime StartDate { get; set; } = DateTime.Now;

        public DateTime EndDate { get; set; } = DateTime.Now;

        public int Dimension { get; set; }

        public int MaxTime { get; set; }

        public int GameTime { get; set; }

        public int GameId { get; set; }
    }
}
