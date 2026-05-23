namespace BattleTaupe3D.Entities
{
    public class PlayEntity
    {
        public int PlayerId { get; set; } = 0;

        public int PartieId { get; set; } = 0;

        public int Score { get; set; } = 0;

        public bool IsAdmin { get; set; } = false;

        public bool IsWinner { get; set; } = false;
    }
}
