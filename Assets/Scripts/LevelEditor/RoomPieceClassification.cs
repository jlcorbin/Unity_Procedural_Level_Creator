namespace LevelGen
{
    /// <summary>Whether a saved RoomPiece prefab is a room or a hallway.</summary>
    public enum PieceType
    {
        Room,
        Hall,
    }

    /// <summary>Sub-category for room pieces — determines the save folder and generator weighting.</summary>
    public enum RoomCategory
    {
        Starter,
        Boss,
        Small,
        Medium,
        Large,
        Special,
    }

    /// <summary>Sub-category for hall pieces — determines the save folder and generator weighting.</summary>
    public enum HallCategory
    {
        Small,
        Medium,
        Large,
        Special,
    }
}
